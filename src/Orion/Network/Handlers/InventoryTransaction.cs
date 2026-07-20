namespace Orion.Network.Handlers;

using Orion;
using Orion.Containers;
using Orion.Scheduling;
using Orion.Gameplay;
using Orion.Item;
using Orion.Item.Traits.Types;
using Orion.Plugins;

using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.RakNet;
using Orion.World;

/// <summary>
/// Inventory transactions: Normal/entity stay inventory-gated; UseItem place/air dispatch to VanillaBuilding.
/// </summary>
public static class InventoryTransaction
{
    private const uint UseItemActionClickBlock = 0;
    private const uint UseItemActionClickAir = 1;

    public static void Handle(Server server, NetworkConnection connection, ReadOnlySpan<byte> packetBuffer)
    {
        InventoryTransactionPacket packet = new();
        int offset = 0;
        BinaryReader reader = new(packetBuffer, ref offset);
        packet = (InventoryTransactionPacket)Protocol.Io.Packet.Deserialize(reader);

        if (!SessionLookup.TryGetPlayer(server, connection, out global::Orion.Player.Player? player))
        {
            return;
        }

#if DEBUG
        if (player.Dimension is Dimension inventoryDimension
            && player.Dimension.World is global::Orion.World.World inventoryWorld)
        {
            ThreadGuard.AssertSimulationThread(inventoryDimension, inventoryWorld);
        }
#endif

        switch (packet.TransactionData)
        {
            case NormalInventoryTransactionData:
                if (!TryGetInventory(player, out IPlayerInventoryAccess? inventory) || inventory is null)
                {
                    return;
                }

                HandleInventoryActions(player, inventory, packet.Actions, packet.LegacySetItemSlots);
                break;

            case UseItemInventoryTransactionData useItem:
                DispatchUseItem(player, useItem);
                break;

            case UseItemOnEntityInventoryTransactionData useItemOnEntity:
                if (!TryGetInventory(player, out IPlayerInventoryAccess? entityInventory) || entityInventory is null)
                {
                    return;
                }

                HandleUseItemOnEntity(player, entityInventory, useItemOnEntity);
                break;

            case ReleaseItemInventoryTransactionData:
                break;

            case MismatchInventoryTransactionData:
                break;
        }
    }

    public static void HandleUseItemFromAuthInput(global::Orion.Player.Player player, UseItemTransactionData data, float pitch, float yaw)
    {
        player.Pitch = pitch;
        player.Yaw = yaw;
        player.HeadYaw = yaw;

        UseItemInventoryTransactionData transaction = new()
        {
            ActionType = unchecked((int)data.ActionType),
            TriggerType = unchecked((byte)data.TriggerType),
            BlockPosition = data.BlockPosition,
            BlockFace = unchecked((byte)data.BlockFace),
            HotBarSlot = data.HotBarSlot,
            HeldItem = data.HeldItem,
            Position = data.Position,
            ClickedPosition = data.ClickedPosition,
            BlockRuntimeId = data.BlockRuntimeId,
            ClientPrediction = data.ClientPrediction,
            ClientCooldownState = data.ClientCooldownState
        };

        bool missingBlockPosition =
            transaction.BlockPosition.X == 0 &&
            transaction.BlockPosition.Y == 0 &&
            transaction.BlockPosition.Z == 0 &&
            transaction.BlockRuntimeId == 0;

        if (missingBlockPosition && FindBlockFromView(player, pitch, yaw, out BlockPos viewedBlock, out int viewedFace))
        {
            transaction.BlockPosition = viewedBlock;
            transaction.BlockFace = (byte)viewedFace;
        }
        else if (missingBlockPosition)
        {
            transaction.BlockPosition = new BlockPos
            {
                X = (int)MathF.Floor(player.Position.X),
                Y = (int)MathF.Floor(player.Position.Y - 1f),
                Z = (int)MathF.Floor(player.Position.Z)
            };

            if (transaction.BlockFace is < 0 or > 5)
            {
                transaction.BlockFace = 1;
            }
        }

        DispatchUseItem(player, transaction);
    }

    private static void DispatchUseItem(global::Orion.Player.Player player, UseItemInventoryTransactionData transaction)
    {
        if (!PluginHost.Services.TryGet(out IPlayerBlockUseHandler? handler) || handler is null)
        {
            return;
        }

        const byte useItemTriggerInitial = 1;
        const byte useItemTriggerRepeat = 2;
        const byte useItemClientPredictionPlace = 1;

        if (transaction.TriggerType == useItemTriggerRepeat)
        {
            return;
        }

        if (transaction.TriggerType == useItemTriggerInitial
            && transaction.ClientPrediction != useItemClientPredictionPlace
            && player.Gamemode != Gamemode.Creative
            && transaction.ActionType == UseItemActionClickBlock)
        {
            return;
        }

        Orion.Api.Math.BlockPos blockPos = new(
            transaction.BlockPosition.X,
            transaction.BlockPosition.Y,
            transaction.BlockPosition.Z);
        Orion.Api.Math.BlockPos placePos = GetPlacedBlockPosition(blockPos, transaction.BlockFace);
        ItemStack? held = null;
        if (PluginHost.Services.TryGet(out IPlayerInventoryService? inventory)
            && inventory is not null
            && inventory.TryGetAccess(player, out IPlayerInventoryAccess? access)
            && access is not null)
        {
            access.SetHeldSlot(transaction.HotBarSlot);
            held = access.GetHeldItem() as ItemStack;
        }

        if (transaction.ActionType == UseItemActionClickAir)
        {
            _ = handler.TryUseOnAir(player, held);
            return;
        }

        if (transaction.ActionType == UseItemActionClickBlock)
        {
            _ = handler.TryUseOnBlock(player, blockPos, transaction.BlockFace, placePos, held);
        }
    }

    static Orion.Api.Math.BlockPos GetPlacedBlockPosition(Orion.Api.Math.BlockPos position, int face) =>
        face switch
        {
            0 => new Orion.Api.Math.BlockPos(position.X, position.Y - 1, position.Z),
            1 => new Orion.Api.Math.BlockPos(position.X, position.Y + 1, position.Z),
            2 => new Orion.Api.Math.BlockPos(position.X, position.Y, position.Z - 1),
            3 => new Orion.Api.Math.BlockPos(position.X, position.Y, position.Z + 1),
            4 => new Orion.Api.Math.BlockPos(position.X - 1, position.Y, position.Z),
            5 => new Orion.Api.Math.BlockPos(position.X + 1, position.Y, position.Z),
            _ => position
        };

    private static bool TryGetInventory(global::Orion.Player.Player player, out IPlayerInventoryAccess? inventory)
    {
        inventory = null;
        return PluginHost.Services.TryGet(out IPlayerInventoryService? inventoryService)
            && inventoryService is not null
            && inventoryService.TryGetAccess(player, out inventory)
            && inventory is not null;
    }

    private static void HandleInventoryActions(
        global::Orion.Player.Player player,
        IPlayerInventoryAccess inventory,
        List<InventoryAction> actions,
        List<LegacySetItemSlot> legacySetItemSlots)
    {
        foreach (InventoryAction action in actions)
        {
            if (action.SourceType == (uint)InventoryActionSourceType.World)
            {
                SpawnWorldDrop(player, action);
                inventory.Container.Update();
                continue;
            }

            if (action.SourceType != (uint)InventoryActionSourceType.Container)
            {
                continue;
            }

            IContainer? container = null;
            if (action.WindowId == (inventory.Container.Identifier ?? 0))
            {
                container = inventory.Container as IContainer;
            }
            else if (player.TryGetOpenContainer(action.WindowId, out IContainer? opened))
            {
                container = opened;
            }

            if (container is null)
            {
                continue;
            }

            int slot = (int)action.InventorySlot;
            if (slot < 0 || slot >= container.GetSize())
            {
                continue;
            }

            if (action.NewItem.NetworkId == 0 || action.NewItem.Count == 0)
            {
                container.ClearSlot(slot);
            }
            else
            {
                try
                {
                    ItemStack item = ItemStack.FromNetworkStack(action.NewItem);
                    container.SetItem(slot, item);
                }
                catch
                {
                }
            }
        }

        _ = legacySetItemSlots;
    }

    private static void SpawnWorldDrop(global::Orion.Player.Player player, InventoryAction action)
    {
        if (action.OldItem.NetworkId == 0 || action.OldItem.Count == 0)
        {
            return;
        }

        ItemStack? item;
        try
        {
            item = ItemStack.FromNetworkStack(action.OldItem);
        }
        catch
        {
            return;
        }

        player.DropItem(item);
    }

    private static void HandleUseItemOnEntity(
        global::Orion.Player.Player player,
        IPlayerInventoryAccess inventory,
        UseItemOnEntityInventoryTransactionData transaction)
    {
        if (player.Dimension is null)
        {
            return;
        }

        ItemStack? heldItem = GetHeldItem(inventory, transaction.HotBarSlot);

        Orion.Entity.Entity? target = null;

        foreach (Orion.Entity.Entity entity in player.Dimension.GetEntities())
        {
            if (entity.RuntimeId == transaction.TargetEntityRuntimeId)
            {
                target = entity;
                break;
            }
        }

        if (target is null)
        {
            return;
        }

        switch (transaction.ActionType)
        {
            case 0:
                if (heldItem is null)
                {
                    return;
                }

                heldItem.OnUseOnEntity(new ItemUseOnEntityDetails(
                    player,
                    target,
                    transaction.HotBarSlot,
                    transaction.Position,
                    transaction.ClickedPosition));
                break;

            case 1:
                if (heldItem is not null)
                {
                    heldItem.OnUseAttack(new ItemUseAttackDetails(
                        player,
                        target,
                        transaction.HotBarSlot,
                        transaction.Position,
                        transaction.ClickedPosition));
                }

                if (!ReferenceEquals(target, player)
                    && target.IsAlive
                    && PluginHost.Services.TryGet(out IEntityHealthService? health)
                    && health is not null)
                {
                    _ = health.TryApplyDamage(target, 1f, player, (int)ActorDamageCause.EntityAttack);
                }
                break;
        }
    }

    private static ItemStack? GetHeldItem(IPlayerInventoryAccess inventory, int hotBarSlot)
    {
        if (hotBarSlot is < 0 or >= 9)
        {
            hotBarSlot = 0;
        }

        inventory.SetHeldSlot(hotBarSlot);

        ItemStack? heldItem = inventory.GetHeldItem() as ItemStack;
        return heldItem is null || heldItem.StackSize == 0 ? null : heldItem;
    }

    private static bool FindBlockFromView(global::Orion.Player.Player player, float pitchDegrees, float yawDegrees, out BlockPos blockPosition, out int face)
    {
        blockPosition = default;
        face = 1;

        if (player.Dimension is null)
        {
            return false;
        }

        float yaw = MathF.PI / 180f * yawDegrees;
        float pitch = MathF.PI / 180f * pitchDegrees;

        float directionX = -MathF.Sin(yaw) * MathF.Cos(pitch);
        float directionY = -MathF.Sin(pitch);
        float directionZ = MathF.Cos(yaw) * MathF.Cos(pitch);

        float startX = player.Position.X;
        float startY = player.Position.Y + 1.62f;
        float startZ = player.Position.Z;

        int previousX = (int)MathF.Floor(startX);
        int previousY = (int)MathF.Floor(startY);
        int previousZ = (int)MathF.Floor(startZ);

        const float maxDistance = 6f;
        const float step = 0.1f;

        for (float distance = step; distance <= maxDistance; distance += step)
        {
            float rayX = startX + directionX * distance;
            float rayY = startY + directionY * distance;
            float rayZ = startZ + directionZ * distance;

            int blockX = (int)MathF.Floor(rayX);
            int blockY = (int)MathF.Floor(rayY);
            int blockZ = (int)MathF.Floor(rayZ);

            Orion.Block.BlockPermutation block =
                player.Dimension.GetGameplayPermutation(blockX, blockY, blockZ);

            if (block.Type.Identifier != "minecraft:air")
            {
                blockPosition = new BlockPos
                {
                    X = blockX,
                    Y = blockY,
                    Z = blockZ
                };

                int deltaX = previousX - blockX;
                int deltaY = previousY - blockY;
                int deltaZ = previousZ - blockZ;

                face = (deltaX, deltaY, deltaZ) switch
                {
                    (1, 0, 0) => 5,
                    (-1, 0, 0) => 4,
                    (0, 1, 0) => 1,
                    (0, -1, 0) => 0,
                    (0, 0, 1) => 3,
                    (0, 0, -1) => 2,
                    _ => 1
                };

                return true;
            }

            previousX = blockX;
            previousY = blockY;
            previousZ = blockZ;
        }

        return false;
    }
}
