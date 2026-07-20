using Orion.RakNet;
namespace Orion.Network.Handlers;

using System.Collections.Concurrent;
using System.Linq;
using Orion;
using Orion.Scheduling;
using Orion.Player;
using Orion.Entity.Traits;
using Orion.Entity.Traits.Types;
using Orion.Gameplay;
using Orion.Item;
using Orion.Player.Traits;
using Orion.Plugins;

using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.RakNet;
using Orion.World;

public static class PlayerAuthInput
{
    private const float MaxHorizontalMovePerTick = 2.0f;
    private const ulong MovementGraceTicks = 5UL;

    private static readonly ConcurrentDictionary<ulong, ulong> LastInputTickByRuntimeId = new();
    private static readonly ConcurrentDictionary<ulong, ulong> MovementGraceUntilTickByRuntimeId = new();
    private static readonly ConcurrentDictionary<ulong, PendingItemUse> PendingItemUses = new();

    private readonly record struct PendingItemUse(int Slot, int StackNetworkId, ulong FinishTick);

    public static void Handle(Server server, NetworkConnection connection, ReadOnlySpan<byte> packetBuffer)
    {
        PlayerAuthInputPacket packet = new();
        try
        {
            int offset = 0;
            BinaryReader reader = new(packetBuffer, ref offset);
            packet = (PlayerAuthInputPacket)Protocol.Io.Packet.Deserialize(reader);
        }
        catch (Exception exception)
        {
            Warn("PlayerAuthInput deserialize failed: {0}", exception);
            return;
        }

        try
        {
            if (!SessionLookup.TryGetPlayer(server, connection, out global::Orion.Player.Player? player))
            {
                return;
            }

            player.FlushClientWorldStateSyncIfPending();

#if DEBUG
            if (player.Dimension is Dimension authDimension
                && authDimension.UsesRegionThreading()
                && player.Dimension.World is global::Orion.World.World authWorld)
            {
                ThreadGuard.AssertSimulationThread(authDimension, authWorld);
            }
#endif

            bool movementRejected = MovedTooFar(player, packet, out ulong tickDelta);
            if (movementRejected)
            {
                ulong worldTick = player.Dimension?.World is Tickable wt ? wt.TickValue : 0UL;
                bool inGrace = MovementGraceUntilTickByRuntimeId.TryGetValue(player.RuntimeId, out ulong graceUntil)
                    && worldTick <= graceUntil;
                Warn(
                    "[Teleport:Move] rejected player={0} client=({1:0.##},{2:0.##},{3:0.##}) " +
                    "server=({4:0.##},{5:0.##},{6:0.##}) tickDelta={7} worldTick={8} grace={9} until={10} transfer={11}",
                    player.Username,
                    packet.Position.X,
                    packet.Position.Y,
                    packet.Position.Z,
                    player.Position.X,
                    player.Position.Y,
                    player.Position.Z,
                    tickDelta,
                    worldTick,
                    inGrace,
                    graceUntil,
                    player.Session?.TransferState.ToString() ?? "no-session");

                CorrectPlayerMovePredictionPacket correction = new()
                {
                    PredictionType = PredictionType.Player,
                    Position = player.Position,
                    PositionDelta = new Vec3f { X = 0f, Y = 0f, Z = 0f },
                    Rotation = new Vec2f { X = packet.Pitch, Y = packet.Yaw },
                    VehicleAngularVelocity = new OptionalValue<float> { HasValue = false },
                    OnGround = packet.InputData.HasFlag(PlayerAuthInputFlag.VerticalCollision),
                    InputTick = packet.Tick
                };

                if (player.Session is not null)
                {
                    player.Session.Send(correction);
                }
                else
                {
                    server.Network.SendPacket(connection, correction);
                }
            }
            else if (player.Session?.TransferState != TransferState.Transferring)
            {
                MovePlayer(server, player, packet);
                player.GetTrait<PlayerChunkRenderingTrait>()?.NotifyClientAtTeleportDestination();
            }
            else
            {
                Debug(
                    "[Teleport:Move] AuthInput ignored (Transferring) player={0} client=({1:0.##},{2:0.##},{3:0.##}) server=({4:0.##},{5:0.##},{6:0.##})",
                    player.Username,
                    packet.Position.X,
                    packet.Position.Y,
                    packet.Position.Z,
                    player.Position.X,
                    player.Position.Y,
                    player.Position.Z);
            }

            ProcessGameplayInput(server, connection, player, packet);
            LastInputTickByRuntimeId[player.RuntimeId] = packet.Tick;
        }
        catch (Exception exception)
        {
            Warn("PlayerAuthInput handler failed: {0}", exception);
        }
    }

    private static void StartUsingItem(global::Orion.Player.Player player)
    {
        IPlayerInventoryAccess? inventory = ResolveInventory(player);
        ItemStack? heldItem = inventory?.GetHeldItem() as ItemStack;
        if (inventory is null
            || heldItem is null
            || !PluginHost.Services.TryGet(out IPlayerItemUseHandler? handler)
            || handler is null
            || !handler.TryBeginUse(player, heldItem, out ulong durationTicks))
        {
            PendingItemUses.TryRemove(player.RuntimeId, out _);
            player.Flags.SetActorFlag(ActorFlag.UsingItem, false);
            return;
        }

        ulong currentTick = GetCurrentTick(player);
        PendingItemUses[player.RuntimeId] = new PendingItemUse(
            inventory.SelectedSlot,
            heldItem.NetworkStackId,
            currentTick + Math.Max(1UL, durationTicks));

        player.Flags.SetActorFlag(ActorFlag.UsingItem, true);
    }

    private static void TickPendingItemUse(global::Orion.Player.Player player)
    {
        if (!PendingItemUses.TryGetValue(player.RuntimeId, out PendingItemUse pending))
        {
            return;
        }

        IPlayerInventoryAccess? inventory = ResolveInventory(player);
        ItemStack? heldItem = inventory?.Container.GetItem(pending.Slot) as ItemStack;
        if (inventory is null || heldItem is null || heldItem.NetworkStackId != pending.StackNetworkId)
        {
            PendingItemUses.TryRemove(player.RuntimeId, out _);
            player.Flags.SetActorFlag(ActorFlag.UsingItem, false);
            return;
        }

        if (GetCurrentTick(player) < pending.FinishTick)
        {
            return;
        }

        PendingItemUses.TryRemove(player.RuntimeId, out _);
        player.Flags.SetActorFlag(ActorFlag.UsingItem, false);

        if (!PluginHost.Services.TryGet(out IPlayerItemUseHandler? handler) || handler is null)
        {
            return;
        }

        _ = handler.TryCompleteUse(player, heldItem, pending.Slot);
    }

    private static ulong GetCurrentTick(global::Orion.Player.Player player)
    {
        return player.Dimension?.World is Orion.World.Tickable tickable ? tickable.TickValue : 0UL;
    }

    private static ItemStackResponse ProcessItemStackRequest(global::Orion.Player.Player player, Protocol.Types.ItemStackRequest request)
    {
        List<StackResponseContainerInfo> containers = [];

        for (int i = 0; i < request.Actions.Count; i++)
        {
            if (request.Actions[i] is not MineBlockStackRequestAction mineBlock)
            {
                continue;
            }

            IPlayerInventoryAccess? inventory = ResolveInventory(player);
            ItemStack? item = inventory?.Container.GetItem(mineBlock.HotbarSlot) as ItemStack;

            containers.Add(new StackResponseContainerInfo
            {
                Container = new FullContainerName { ContainerId = 29 },
                SlotInfo =
                [
                    new StackResponseSlotInfo
                    {
                        Slot = (byte)mineBlock.HotbarSlot,
                        HotbarSlot = (byte)mineBlock.HotbarSlot,
                        Count = (byte)(item?.StackSize ?? 0),
                        StackNetworkId = item?.NetworkStackId ?? 0,
                        CustomName = string.Empty,
                        FilteredCustomName = string.Empty,
                        DurabilityCorrection = 0
                    }
                ]
            });
        }

        return new ItemStackResponse
        {
            Status = ItemStackResponseStatus.Ok,
            RequestId = request.RequestId,
            ContainerInfo = containers
        };
    }

    private static MineBlockStackRequestAction? GetMineBlockRequest(Protocol.Types.ItemStackRequest request)
    {
        for (int i = 0; i < request.Actions.Count; i++)
        {
            if (request.Actions[i] is MineBlockStackRequestAction mineBlock)
            {
                return mineBlock;
            }
        }

        return null;
    }

    public static void ResetMovementValidation(ulong runtimeId)
    {
        LastInputTickByRuntimeId.TryRemove(runtimeId, out _);
        MovementGraceUntilTickByRuntimeId.TryRemove(runtimeId, out _);
    }

    public static void BeginMovementGrace(ulong runtimeId, ulong currentTick)
    {
        MovementGraceUntilTickByRuntimeId[runtimeId] = currentTick + MovementGraceTicks;
    }

    /// <summary>
    /// Most recent PlayerAuthInput tick for this player (PlayerInputTick).
    /// MovePlayer / movement packets must use this — not the world tick.
    /// </summary>
    public static ulong GetLastInputTick(ulong runtimeId) =>
        LastInputTickByRuntimeId.TryGetValue(runtimeId, out ulong tick) ? tick : 0UL;

    /// <summary>
    /// After a server-driven teleport: keep the last input tick, open a short grace window.
    /// </summary>
    public static void OnServerTeleport(ulong runtimeId, ulong worldTick)
    {
        MovementGraceUntilTickByRuntimeId[runtimeId] = worldTick + MovementGraceTicks;
    }

    private static void ProcessGameplayInput(
        Server server,
        NetworkConnection connection,
        global::Orion.Player.Player player,
        PlayerAuthInputPacket packet)
    {
        TickPendingItemUse(player);

        if (packet.InputData.HasFlag(PlayerAuthInputFlag.PerformItemInteraction))
        {
            InventoryTransaction.HandleUseItemFromAuthInput(
                player,
                packet.ItemInteractionData,
                packet.InteractPitch,
                packet.InteractYaw);
        }

        MineBlockStackRequestAction? mineBlockRequest = null;
        if (packet.InputData.HasFlag(PlayerAuthInputFlag.PerformItemStackRequest))
        {
            mineBlockRequest = GetMineBlockRequest(packet.ItemStackRequest);
            bool onlyMineBlock = packet.ItemStackRequest.Actions.All(static action =>
                action is MineBlockStackRequestAction
                    or EmptyStackRequestAction
                    or CraftResultsDeprecatedStackRequestAction);

            CreativeInventoryLog.LogItemStackAction(
                player.Username,
                "auth-input",
                $"req={packet.ItemStackRequest.RequestId} actions={packet.ItemStackRequest.Actions.Count} mineOnly={onlyMineBlock}");

            ItemStackResponse response = onlyMineBlock
                ? ProcessItemStackRequest(player, packet.ItemStackRequest)
                : ItemStackRequest.Process(player, packet.ItemStackRequest);

            CreativeInventoryLog.LogItemStackResponse(player.Username, 1, [response]);

            ItemStackResponsePacket stackResponse = new()
            {
                Responses = [response]
            };

            if (player.Session is not null)
            {
                player.Session.Send(stackResponse);
            }
            else
            {
                server.Network.SendPacket(connection, stackResponse);
            }
        }

        if (packet.InputData.HasFlag(PlayerAuthInputFlag.PerformBlockActions))
        {
            foreach (PlayerBlockAction action in packet.BlockActions)
            {
                HandleBlockAction(player, action, packet.Tick);
            }
        }

        if (packet.InputData.HasFlag(PlayerAuthInputFlag.StartUsingItem))
        {
            StartUsingItem(player);
        }

        if (packet.InputData.HasFlag(PlayerAuthInputFlag.StartSprinting))
        {
            player.IsSprinting = true;
        }
        else if (packet.InputData.HasFlag(PlayerAuthInputFlag.StopSprinting))
        {
            player.IsSprinting = false;
        }

        if (packet.InputData.HasFlag(PlayerAuthInputFlag.StartSneaking))
        {
            player.IsSneaking = true;
        }
        else if (packet.InputData.HasFlag(PlayerAuthInputFlag.StopSneaking))
        {
            player.IsSneaking = false;
        }
        else if (mineBlockRequest is not null && player.LastActionBlockPosition.HasValue)
        {
            BlockPos position = player.LastActionBlockPosition.Value;
            Warn(
                "PlayerAuthInput mine fallback player={0} pos={1},{2},{3} face={4}",
                player.Username,
                position.X,
                position.Y,
                position.Z,
                player.LastActionFace);

            if (PluginHost.Services.TryGet(out IPlayerBlockBreakHandler? breakHandler) && breakHandler is not null)
            {
                breakHandler.OnPredictDestroy(
                    player,
                    ToApiBlockPos(position),
                    player.LastActionFace,
                    packet.Tick);
            }
        }
        else if (mineBlockRequest is not null)
        {
            Warn("PlayerAuthInput mine request had no block actions and no last PlayerAction target player={0}", player.Username);
        }
    }

    private static bool MovedTooFar(global::Orion.Player.Player player, PlayerAuthInputPacket packet, out ulong rawTickDelta)
    {
        float deltaX = packet.Position.X - player.Position.X;
        float deltaZ = packet.Position.Z - player.Position.Z;
        float movedDistanceSquared = deltaX * deltaX + deltaZ * deltaZ;

        ulong previousTick = LastInputTickByRuntimeId.GetOrAdd(player.RuntimeId, packet.Tick);
        rawTickDelta = packet.Tick > previousTick ? packet.Tick - previousTick : 1UL;

        float tickDelta = Math.Clamp((float)rawTickDelta, 1f, 20f);
        float allowedDistance = MaxHorizontalMovePerTick * tickDelta;

        ulong currentTick = GetCurrentTick(player);
        if (MovementGraceUntilTickByRuntimeId.TryGetValue(player.RuntimeId, out ulong graceUntil)
            && currentTick <= graceUntil)
        {
            allowedDistance *= 2f;
        }

        return movedDistanceSquared > allowedDistance * allowedDistance;
    }

    private static void MovePlayer(Server server, global::Orion.Player.Player player, PlayerAuthInputPacket packet)
    {
        Vec3f previousPosition = player.Position;

        MovementRotation fromRotation = new MovementRotation()
        {
            HeadYaw = player.HeadYaw,
            Pitch = player.Pitch,
            Yaw = player.Yaw,
        };

        MovementRotation toRotation = new MovementRotation()
        {
            HeadYaw = packet.Yaw,
            Pitch = packet.Pitch,
            Yaw = packet.Yaw,
        };

        player.Pitch = packet.Pitch;
        player.Yaw = packet.Yaw;
        player.HeadYaw = packet.Yaw;

        bool missingPosition =
            packet.Position.X == 0f &&
            packet.Position.Y == 0f &&
            packet.Position.Z == 0f;

        bool hasDelta =
            packet.Delta.X != 0f ||
            packet.Delta.Y != 0f ||
            packet.Delta.Z != 0f;

        player.Position = missingPosition && hasDelta
            ? new Vec3f
            {
                X = previousPosition.X + packet.Delta.X,
                Y = previousPosition.Y + packet.Delta.Y,
                Z = previousPosition.Z + packet.Delta.Z
            }
            : packet.Position;

        player.OnMove(new EntityMoveOptions(previousPosition, player.Position, fromRotation, toRotation));

        bool borderTransfer = AreaBorderTransfer.TryAfterMove(server, player, previousPosition);

        // Always broadcast the border step so peers do not lag one tick behind during handoff.
        if (player.Dimension is not null)
        {
            bool positionChanged = previousPosition.X != player.Position.X
                || previousPosition.Y != player.Position.Y
                || previousPosition.Z != player.Position.Z;
            bool rotationChanged = fromRotation.Pitch != toRotation.Pitch
                || fromRotation.Yaw != toRotation.Yaw
                || fromRotation.HeadYaw != toRotation.HeadYaw;

            if (positionChanged || rotationChanged)
            {
                int viewDistance = player.GetTrait<PlayerChunkRenderingTrait>()?.ViewDistance ?? 16;
                int simulationDistance = Math.Clamp(server.Properties.SimulationDistance, 0, 120);
                float broadcastRadius = Math.Max(viewDistance, simulationDistance) * 16f;

                player.Dimension.Broadcast(
                    new MoveActorDeltaPacket
                    {
                        EntityRuntimeId = player.RuntimeId,
                        Flags = (ushort)MoveDeltaFlags.All,
                        Position = player.Position,
                        Rotation = new Vec3f
                        {
                            X = toRotation.Pitch,
                            Y = toRotation.Yaw,
                            Z = toRotation.HeadYaw
                        }
                    },
                    new BroadcastOptions
                    {
                        Except = [player],
                        Center = player.Position,
                        Radius = broadcastRadius
                    });
            }
        }

        if (borderTransfer)
        {
            return;
        }
    }

    private static void HandleBlockAction(global::Orion.Player.Player player, PlayerBlockAction action, ulong tick)
    {
        if (!PluginHost.Services.TryGet(out IPlayerBlockBreakHandler? handler) || handler is null)
        {
            return;
        }

        switch (action.Action)
        {
            case PlayerActionType.StartDestroyBlock:
                handler.OnStartDestroy(player, ToApiBlockPos(action.BlockPos), action.Face, tick);
                break;

            case PlayerActionType.ContinueDestroyBlock:
                handler.OnContinueDestroy(player, ToApiBlockPos(action.BlockPos), action.Face, tick);
                break;

            case PlayerActionType.CrackBlock:
                handler.OnCrack(player, ToApiBlockPos(action.BlockPos), action.Face, tick);
                break;

            case PlayerActionType.AbortDestroyBlock:
            case PlayerActionType.StopDestroyBlock:
                handler.OnAbortDestroy(player, ToApiBlockPos(action.BlockPos), action.Face);
                break;

            case PlayerActionType.PredictDestroyBlock:
                handler.OnPredictDestroy(player, ToApiBlockPos(action.BlockPos), action.Face, tick);
                break;

            case PlayerActionType.CreativeDestroyBlock:
                handler.OnCreativeDestroy(player, ToApiBlockPos(action.BlockPos), action.Face);
                break;
        }
    }

    static Orion.Api.Math.BlockPos ToApiBlockPos(BlockPos position) =>
        new(position.X, position.Y, position.Z);

    private static IPlayerInventoryAccess? ResolveInventory(global::Orion.Player.Player player)
    {
        if (PluginHost.Services.TryGet(out IPlayerInventoryService? service)
            && service is not null
            && service.TryGetAccess(player, out IPlayerInventoryAccess? access))
        {
            return access;
        }

        return null;
    }
}










