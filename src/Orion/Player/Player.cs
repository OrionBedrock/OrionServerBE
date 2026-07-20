namespace Orion.Player;

using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Network;
using Orion.Network.Handlers;
using Orion.Scheduling;
using Orion.Config;
using Log = Orion.Logger.Logger;

using Orion.RakNet;
using Orion.Api.Containers;
using Orion.Protocol.Types;
using Orion.Protocol.Nbt;
using Orion.World;

using Basalt.Binary;
using Orion.Entity.Traits;
using Orion.Gameplay;
using Orion.Plugins;
using Orion.Entity.Traits.Types;
using Orion.Player.Traits;
using Orion.Api;
using Orion.Api.Items;
using Orion.Api.Network;
using Orion.Plugins.Api;
using ApiGamemode = Orion.Api.Gamemode;
using ApiHudVisibility = Orion.Api.HudVisibility;
using ApiHudElement = Orion.Api.HudElement;
using ApiVec3f = Orion.Api.Math.Vec3f;
using ProtocolGamemode = Orion.Protocol.Enums.Gamemode;
using ProtocolHudVisibility = Orion.Protocol.Enums.HudVisibility;
using ProtocolHudElement = Orion.Protocol.Enums.HudElement;
using CoreContainer = Orion.Api.Containers.IContainer;
using EntitySpawnOptions = Orion.Entity.Traits.Types.EntitySpawnOptions;
using BroadcastOptions = Orion.World.BroadcastOptions;

public sealed class Player : global::Orion.Entity.Entity, IAreaEntity, IPlayerWithSession, IPlayer
{
        Vec3f IAreaEntity.Position => Position;

public readonly string Username;
    public readonly string Xuid;
    public readonly Guid Uuid;
    private DeviceOS _deviceOS;
    private byte[]? _skin;
    public PlayerAbilities Abilities { get; } = new();
    public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ProtocolGamemode Gamemode { get; private set; } = ProtocolGamemode.Survival;
    public bool IsOperator { get; private set; }
    public bool Spawned { get; private set; }
    public float Pitch;
    public float Yaw;
    public float HeadYaw { get; set; }
    public BlockPos? BreakingBlock { get; set; }
    public BlockPos? LastActionBlockPosition { get; set; }
    public BlockPos? LastActionResultPosition { get; set; }
    public int LastActionFace { get; set; }
    public Dictionary<int, CoreContainer> openedContainers = [];

    /// <summary>Null for NPCs and fake players.</summary>
    public PlayerSession? Session { get; internal set; }

    public bool IsOnline => Session is not null;

    internal NetworkConnection? Connection => Session?.Connection;
    internal NetworkHandler? Network => Session?.Network;

    public DeviceOS DeviceOS
    {
        get => Session?.DeviceOS ?? _deviceOS;
        set
        {
            _deviceOS = value;
            if (Session is not null)
            {
                Session.DeviceOS = value;
            }
        }
    }

    public Player(string username, string xuid, Guid uuid, ulong? runtimeId = null) :
        base(EntityIdentifier.Player.ToIdentifierString(), runtimeId)
    {
        Username = username;
        Xuid = xuid;
        Uuid = uuid;

        Flags.SetActorFlag(ActorFlag.HasGravity, true);
        Flags.SetActorFlag(ActorFlag.Breathing, true);
        Flags.SetActorFlag(ActorFlag.CanShowName, true);
        Flags.SetActorFlag(ActorFlag.AlwaysShowName, true);
    }

    public ProtocolGamemode GetGamemode()
    {
        return Gamemode;
    }

    public void SetGamemode(ProtocolGamemode gamemode)
    {
        Gamemode = gamemode;

        UpdatePlayerGameTypePacket gamemodePacket = new()
        {
            GameType = gamemode,
            PlayerUniqueId = UniqueId,
            Tick = Dimension?.World is Tickable tickable ? tickable.TickValue : 0
        };
        Abilities.SetGamemode(gamemode);

        UpdateAbilitiesPacket abilitiesPacket = CreateAbilitiesPacket();

        Dimension?.Broadcast(gamemodePacket, new BroadcastOptions { Except = [this] });

        if (Session is not null)
        {
            // Basalt: only SetPlayerGameType + UpdateAbilities. Re-sending ItemRegistry /
            // CreativeContent mid-session crashes Bedrock (ClientDisconnection / codeword Block).
            SessionSendCoordinator.SendGamemodeChange(Session, Username, gamemode, abilitiesPacket);
        }
    }

    public void LoadGamemode(ProtocolGamemode gamemode)
    {
        Gamemode = gamemode;
        Abilities.SetGamemode(gamemode);
        if (IsOperator)
        {
            Abilities.SetOperator(true);
        }
    }

    public void SetOperator(bool isOperator, bool syncClient = true)
    {
        IsOperator = isOperator;
        Abilities.SetOperator(isOperator);
        if (isOperator)
        {
            AddPermission("basalt.op", syncClient: false);
        }
        else
        {
            RemovePermission("basalt.op", syncClient: false);
        }

        if (syncClient)
        {
            SyncPermissions();
        }
    }

    public void AddPermission(string permission, bool syncClient = true)
    {
        Permissions.Add(permission);
        if (syncClient)
        {
            SyncPermissions();
        }
    }

    public void RemovePermission(string permission, bool syncClient = true)
    {
        Permissions.Remove(permission);
        if (syncClient)
        {
            SyncPermissions();
        }
    }

    public void SyncPermissions()
    {
        if (Session is null)
        {
            return;
        }

        Session.Send(CreateAbilitiesPacket());

        if (Dimension?.World?.Server is global::Orion.Server server)
        {
            server.Commands.SendAvailableCommands(server, this);
        }
    }

    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission);
    }

    public new CompoundTag WriteToNbt()
    {
        CompoundTag root = base.WriteToNbt();
        root.Set("username", new StringTag { Value = Username });
        root.Set("xuid", new StringTag { Value = Xuid });
        root.Set("uuid", new StringTag { Value = Uuid.ToString() });
        root.Set("gamemode", new IntTag { Value = (int)Gamemode });
        root.Set("isOp", new ByteTag { Value = IsOperator ? (sbyte)1 : (sbyte)0 });
        return root;
    }

    public new void FromNBT(CompoundTag root)
    {
        base.FromNBT(root);

        if (root.Get<IntTag>("gamemode") is { } gamemodeTag)
        {
            LoadGamemode((ProtocolGamemode)gamemodeTag.Value);
        }

        IsOperator = (root.Get<ByteTag>("isOp")?.Value ?? 0) != 0;
        Abilities.SetOperator(IsOperator);
        if (IsOperator)
        {
            Permissions.Add("basalt.op");
        }
        else
        {
            Permissions.Remove("basalt.op");
        }
    }

    UpdateAbilitiesPacket CreateAbilitiesPacket()
    {
        return new UpdateAbilitiesPacket
        {
            EntityUniqueId = UniqueId,
            PlayerPermission = IsOperator ? PlayerPermissionLevel.Operator : PlayerPermissionLevel.Member,
            CommandPermission = IsOperator ? CommandPermissionLevel.Admin : CommandPermissionLevel.Any,
            Layers = [Abilities.ToLayer()]
        };
    }

    public void Send(params DataPacket[] packets)
    {
        Session?.Send(packets);
    }

    /// <summary>
    /// Show or hide HUD elements for this client (Bedrock SetHud /hud).
    /// </summary>
    public void SetHud(ProtocolHudVisibility visibility, params ProtocolHudElement[] elements)
    {
        if (elements.Length == 0 || Session is null)
        {
            return;
        }

        Send(new SetHudPacket
        {
            Elements = [.. elements],
            Visibility = visibility
        });
    }

    public bool DropItem(Item.ItemStack item)
    {
        if (Dimension is null || item.StackSize == 0 || item.Type == Item.ItemType.Air)
        {
            return false;
        }

        Vec3f feet = GetPosition();
        float yaw = MathF.PI / 180f * Yaw;
        float pitch = MathF.PI / 180f * Pitch;

        global::Orion.Entity.ItemEntity drop = new(item)
        {
            Position = new Vec3f
            {
                X = feet.X,
                Y = feet.Y + 1.15f,
                Z = feet.Z
            },
            Velocity = new Vec3f
            {
                X = (-MathF.Sin(yaw) * MathF.Cos(pitch)) / 3f,
                Y = ((-MathF.Sin(pitch)) / 2f) + 0.2f,
                Z = (MathF.Cos(yaw) * MathF.Cos(pitch)) / 3f
            }
        };

        ulong currentTick = Dimension.World is Tickable tickable ? tickable.TickValue : 0;
        drop.LockMergeUntil(currentTick + 40);
        drop.LockPickupUntil(currentTick + 40);
        drop.Spawn(Dimension, new EntitySpawnOptions(InitialSpawn: false));
        return true;
    }

    public ushort CollectItem(Item.ItemStack item)
    {
        if (!PluginHost.Services.TryGet(out IPlayerInventoryService? inventory) || inventory is null)
        {
            return 0;
        }

        return inventory.TryCollect(this, item, out ushort moved) ? moved : (ushort)0;
    }

    public void Disconnect(string reason = "")
    {
        Session?.Disconnect(reason);
    }

    public void SyncGamemodeToClient()
    {
        if (Session is null)
        {
            return;
        }

        Session.Send(new SetPlayerGameTypePacket { GameType = Gamemode });
        Session.Send(CreateAbilitiesPacket());
    }

    /// <summary>
    /// Pushes per-world entity state to the client after login or world transfer.
    /// </summary>
    public void SyncClientWorldState()
    {
        if (Session is null)
        {
            return;
        }

        SetSpawned(true);

        SyncGamemodeToClient();
        SyncPermissions();

        openedContainers.Clear();

        if (PluginHost.Services.TryGet(out IPlayerInventoryService? inventory) && inventory is not null)
        {
            _ = inventory.TrySyncToClient(this);
        }

        SendAttributes();
    }

    /// <summary>
    /// Defers inventory/gamemode sync until the client acks dimension change or closes stale containers.
    /// </summary>
    public void ScheduleClientWorldStateSync()
    {
        if (Session is null)
        {
            return;
        }

        ulong tick = Dimension?.World is Tickable tickable ? tickable.TickValue : 0;
        Session.PendingClientWorldStateSync = true;
        Session.ClientWorldStateSyncMinTick = tick + 5;
    }

    public void FlushClientWorldStateSyncIfPending(bool force = false)
    {
        if (Session?.PendingClientWorldStateSync != true)
        {
            return;
        }

        ulong tick = Dimension?.World is Tickable tickable ? tickable.TickValue : 0;
        if (!force && tick < Session.ClientWorldStateSyncMinTick)
        {
            return;
        }

        Session.PendingClientWorldStateSync = false;
        SyncClientWorldState();
    }

    /// <summary>
    /// Pushes the authoritative player inventory to the client (window 0).
    /// </summary>
    public void SyncInventoryToClient()
    {
        if (!Spawned)
        {
            return;
        }

        if (PluginHost.Services.TryGet(out IPlayerInventoryService? inventory) && inventory is not null)
        {
            _ = inventory.TrySyncToClient(this);
        }
    }

    public void SetSpawned(bool spawned)
    {
        Spawned = true;
    }

    public override void Spawn(Dimension dimension, EntitySpawnOptions options)
    {
        base.Spawn(dimension, options);
        SendAttributes();
    }

    public void Teleport(Vec3f position, Dimension? dimension = null, bool forceDimensionChange = false)
    {
        Dimension? previousDimension = Dimension;
        Dimension targetDimension = dimension ?? previousDimension ??
            throw new InvalidOperationException("Player must have a dimension to teleport without a target dimension.");

        Vec3f previousPosition = Position;
        bool changedDimension = previousDimension != targetDimension;
        bool changedDimensionType = previousDimension is not null && previousDimension.Type != targetDimension.Type;

        int? sourceArea = previousDimension?.UsesAreaThreading() == true
            ? previousDimension.ResolveAreaIndex(previousPosition.X, previousPosition.Z)
            : null;
        int? targetArea = targetDimension.UsesAreaThreading()
            ? targetDimension.ResolveAreaIndex(position.X, position.Z)
            : null;

        Info(
            LogCategory.Orion,
            "[Teleport] begin player={0} from=({1:0.##},{2:0.##},{3:0.##}) to=({4:0.##},{5:0.##},{6:0.##}) " +
            "dimChange={7} dimTypeChange={8} forceDim={9} area={10}->{11} transferState={12} tick={13}",
            Username,
            previousPosition.X,
            previousPosition.Y,
            previousPosition.Z,
            position.X,
            position.Y,
            position.Z,
            changedDimension,
            changedDimensionType,
            forceDimensionChange,
            sourceArea?.ToString() ?? "-",
            targetArea?.ToString() ?? "-",
            Session?.TransferState.ToString() ?? "no-session",
            targetDimension.World is Tickable t0 ? t0.TickValue : 0UL);

        Position = position;

        // Full client chunk reload only on dimension change; keep columns if destination already rendered.
        bool forceFullChunkReload = changedDimension || forceDimensionChange;

        OnTeleport(new EntityTeleportOptions(previousPosition, position, forceFullChunkReload));

        if (changedDimension)
        {
            previousDimension?.Broadcast(new RemoveActorPacket
            {
                EntityUniqueId = UniqueId
            }, new BroadcastOptions { Except = [this] });

            previousDimension?.RemoveEntity(this, complete: false);
            Dimension = targetDimension;
            targetDimension.AddEntity(this);
        }

        ulong worldTick = targetDimension.World is Tickable tickable ? tickable.TickValue : 0;
        ulong inputTick = PlayerAuthInput.GetLastInputTick(RuntimeId);

        if (changedDimensionType || forceDimensionChange)
        {
            Info(LogCategory.Orion, "[Teleport] player={0} send=ChangeDimension worldTick={1} inputTick={2}", Username, worldTick, inputTick);
            Send(new ChangeDimensionPacket
            {
                Dimension = targetDimension.Type,
                Position = position,
                Respawn = true,
                HasLoadingScreen = false
            });
        }

        // PlayerInputTick (not world tick) — Bedrock ignores teleports with the wrong tick under server-auth movement.
        Info(LogCategory.Orion, "[Teleport] player={0} send=MovePlayer({1}) inputTick={2} worldTick={3}",
            Username,
            changedDimension ? "Reset" : "Teleport",
            inputTick,
            worldTick);
        Send(new MovePlayerPacket
        {
            RuntimeId = RuntimeId,
            Position = position,
            Pitch = Pitch,
            Yaw = Yaw,
            HeadYaw = HeadYaw,
            Mode = changedDimension ? MoveMode.Reset : MoveMode.Teleport,
            OnGround = false,
            RiddenRuntimeId = 0,
            TeleportCause = TeleportCause.Command,
            TeleportSourceEntityType = 0,
            Tick = inputTick
        });

        if (changedDimension)
        {
            targetDimension.Broadcast(CreateActorDataPacket(worldTick), new BroadcastOptions { Except = [this] });
        }

        // Soft teleports keep already-rendered chunks; full reload arms a short hold before LevelChunks.
        SendAttributes();

        PlayerAuthInput.OnServerTeleport(RuntimeId, worldTick);
        AreaBorderTransfer.ResetTransferCooldown(RuntimeId);

        // Apply position + client packets first; only then move the entity between area shards.
        Server? server = targetDimension.World?.Server as Server ?? previousDimension?.World?.Server as Server;
        bool startedAreaTransfer = false;
        if (server is not null && !changedDimension && targetDimension.UsesAreaThreading())
        {
            startedAreaTransfer = AreaBorderTransfer.TryAfterTeleport(server, this, previousPosition);
        }

        Info(
            LogCategory.Orion,
            "[Teleport] end player={0} pos=({1:0.##},{2:0.##},{3:0.##}) owningArea={4} " +
            "areaTransfer={5} transferState={6} inputTick={7} chunkView={8}",
            Username,
            Position.X,
            Position.Y,
            Position.Z,
            OwningAreaIndex?.ToString() ?? "-",
            startedAreaTransfer,
            Session?.TransferState.ToString() ?? "no-session",
            inputTick,
            GetTrait<PlayerChunkRenderingTrait>()?.FormatDebugHudLine() ?? "none");
    }

    /// <summary>
    /// Re-syncs the client after a cross-worker world transfer (same flow essentials as SetLocalPlayerAsInitialized).
    /// Caller must set <see cref="PlayerSession.ActiveEntity"/> and <see cref="PlayerSession.TransferState"/> to Idle first.
    /// </summary>
    /// <param name="useDimensionChange">
    /// When true, sends ChangeDimension (required when dimension type changes). Same-type cross-world transfers
    /// use MovePlayer instead to avoid the client getting stuck on "building terrain".
    /// </param>
    public void ResyncAfterWorldTransfer(bool useDimensionChange)
    {
        Dimension targetDimension = Dimension ??
            throw new InvalidOperationException("Player must have a dimension to resync after transfer.");

        ulong tick = targetDimension.World is Tickable tickable ? tickable.TickValue : 0;

        Send(CreateActorDataPacket(tick));
        SendAttributes();

        Teleport(Position, targetDimension, forceDimensionChange: useDimensionChange);

        GetTrait<PlayerChunkRenderingTrait>()?.ForceReloadViewDistance();

        ScheduleClientWorldStateSync();
        PlayerAuthInput.ResetMovementValidation(RuntimeId);
        AreaBorderTransfer.ResetTransferCooldown(RuntimeId);
    }

    /// <summary>
    /// Client sync after cross-region handoff within the same dimension.
    /// Ownership moves on the server; client only refreshes publisher/presence (no MovePlayer teleport).
    /// </summary>
    public void ResyncAfterRegionHandoff()
    {
        GetTrait<PlayerChunkRenderingTrait>()?.AfterRegionHandoff();
    }

    public void RegisterOpenContainer(int windowId, CoreContainer container)
    {
        openedContainers[windowId] = container;
    }

    public bool TryGetOpenContainer(int windowId, out CoreContainer? container)
    {
        return openedContainers.TryGetValue(windowId, out container);
    }

    public CoreContainer? GetContainer(FullContainerName name)
    {
        if (PluginHost.Services.TryGet(out IPlayerInventoryService? inventory) && inventory is not null)
        {
        return inventory.ResolveContainer(this, new ContainerNameWire(name)) as CoreContainer;
        }

        return null;
    }


    public void SendAttributes()
    {
        if (Session is null)
        {
            return;
        }

        ulong tick = Dimension?.World is Tickable tickable ? tickable.TickValue : 0;

        UpdateAttributesPacket attributes = new()
        {
            RuntimeId = RuntimeId,
            Tick = tick,
            Attributes = Attributes.GetAll().ToList()
        };

        if (attributes.Attributes.Count > 0)
        {
            Session.Send(attributes);
        }

        AttributesDirty = false;
    }

    public PlayerListEntry CreatePlayerListEntry()
    {
        global::Orion.Protocol.Types.Skin skin = Session?.Skin ?? new();
        if (Session is null && _skin is not null && _skin.Length > 0)
        {
            int offset = 0;
            BinaryReader reader = new(_skin, ref offset);
            skin.Read(reader);
        }

        return new PlayerListEntry
        {
            Uuid = Uuid,
            EntityUniqueId = UniqueId,
            Username = Username,
            Xuid = Xuid,
            PlatformChatId = string.Empty,
            DeviceOS = DeviceOS,
            Skin = skin,
            Teacher = false,
            Host = false,
            SubClient = false,
            PlayerColor = 0
        };
    }

    public void SetSkin(global::Orion.Protocol.Types.Skin skin)
    {
        if (Session is not null)
        {
            Session.Skin = skin;
            return;
        }

        using BinaryStream stream = BinaryStream.Rent(2 * 1024 * 1024);
        BinaryWriter writer = stream;
        skin.Write(writer);
        _skin = writer.GetProcessedBytes().ToArray();
    }

    public override void SpawnTo(Player player, ulong tick)
    {
        ItemInstance heldItem = new();
        Item.ItemStack? held = PluginHost.Services.TryGet(out IPlayerInventoryService? invSvc) && invSvc is not null
            ? invSvc.GetHeldItem(this) as Item.ItemStack
            : null;
        if (held is not null)
        {
            heldItem.Stack = held.ToNetworkStack();
            heldItem.StackNetworkId = held.NetworkStackId;
        }

        // return;
        player.Send(new AddPlayerPacket
        {
            Uuid = Uuid,
            Username = Username,
            EntityRuntimeId = RuntimeId,
            PlatformChatId = string.Empty,
            Position = Position,
            Velocity = new Vec3f(),
            Pitch = Pitch,
            Yaw = Yaw,
            HeadYaw = HeadYaw,
            HeldItem = heldItem,
            GameType = (int)Gamemode,
            EntityMetadata = CreateActorDataPacket(tick).Metadata,
            EntityProperties = new EntityProperties(),
            AbilityData = new AbilityData
            {
                EntityUniqueId = UniqueId,
                Layers = [Abilities.ToLayer()]
            },
            EntityLinks = [],
            DeviceId = string.Empty,
            DeviceOS = DeviceOS
        });
    }

    public void SendMessage(
        string message
    )
    {
        Session?.SendMessage(message);
    }

    ApiGamemode IPlayer.Gamemode => (ApiGamemode)(int)Gamemode;

    string IPlayer.Username => Username;

    string IPlayer.Xuid => Xuid;

    Guid IPlayer.Uuid => Uuid;

    void IPlayer.SetGamemode(ApiGamemode gamemode) =>
        SetGamemode((ProtocolGamemode)(int)gamemode);

    void IPlayer.Teleport(ApiVec3f position, IDimension? dimension, bool forceDimensionChange)
    {
        Dimension? target = DimensionApi.TryUnwrap(dimension);
        Teleport(new Vec3f(position.X, position.Y, position.Z), target, forceDimensionChange);
    }

    void IPlayer.Send(params IOutboundPacket[] packets) =>
        Send(OutboundPacketAdapter.ToDataPackets(packets));

    void IPlayer.SetHud(ApiHudVisibility visibility, params ApiHudElement[] elements) =>
        SetHud((ProtocolHudVisibility)(int)visibility, Array.ConvertAll(elements, static e => (ProtocolHudElement)(int)e));

    bool IPlayer.DropItem(IItemStack item) =>
        item is Item.ItemStack stack && DropItem(stack);

    IReadOnlyDictionary<int, Orion.Api.Containers.IContainer> IPlayer.OpenedContainers => openedContainers;

    void IPlayer.RegisterOpenContainer(int windowId, Orion.Api.Containers.IContainer container) =>
        RegisterOpenContainer(windowId, container);

    bool IPlayer.TryGetOpenContainer(int windowId, out Orion.Api.Containers.IContainer? container) =>
        TryGetOpenContainer(windowId, out container);

    void IPlayer.UnregisterOpenContainer(int windowId) =>
        openedContainers.Remove(windowId);

    void IPlayer.FlushPendingClientSync(bool force) =>
        FlushClientWorldStateSyncIfPending(force: force);

}






