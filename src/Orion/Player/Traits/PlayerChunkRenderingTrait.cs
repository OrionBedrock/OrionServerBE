namespace Orion.Player.Traits;

using Orion.Config;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.Block;
using Orion.Entity.Traits;
using Orion.Entity.Traits.Types;
using Orion.Scheduling;
using Orion.Api.Traits;
using Orion.World;
using Orion.World.Block;
using Orion.World.Coordinates;
using Log = Orion.Logger.Logger;

using ChunkColumn = Orion.World.Chunk.Chunk;
using Entity = Orion.Entity.Entity;

public sealed class PlayerChunkRenderingTrait : PlayerTrait, ISessionTickableTrait
{
    private const int ChunksPerTick = 64;

    public new static string Identifier => "chunk_rendering";
    public new static readonly EntityIdentifier[] Types = [EntityIdentifier.Player];

    private readonly Lock _lock = new();
    private readonly HashSet<long> _loadedChunks = [];
    private readonly HashSet<long> _requestedChunks = [];
    private readonly Queue<ChunkColumn> _readyChunks = [];
    private readonly Dictionary<ulong, long> _visibleEntityUniqueIds = [];

    private int _currentChunkX = int.MinValue;
    private int _currentChunkZ = int.MinValue;

    private int _publisherChunkX = int.MinValue;
    private int _publisherChunkZ = int.MinValue;

    private int? _lastPresenceArea;

    private int _scanRadius;
    private int _scanX;
    private int _scanZ;

    private bool _started;

    /// <summary>
    /// Session ticks remaining while waiting for the client to apply a server teleport
    /// before LevelChunks are sent (avoids far chunks being discarded at the old position).
    /// Also used as a max-wait fallback if AuthInput never catches up.
    /// </summary>
    private int _teleportHoldTicks;

    /// <summary>
    /// True until the first accepted AuthInput near the post-teleport server position (or timeout).
    /// </summary>
    private bool _awaitingTeleportChunkSync;

    public int ViewDistance { get; private set; } = 16;

    public PlayerChunkRenderingTrait(Entity entity) : base(entity)
    {
    }

    /// <summary>
    /// Compact one-line status for the debug tip HUD.
    /// </summary>
    public string FormatDebugHudLine()
    {
        int expected = ((ViewDistance * 2) + 1);
        expected *= expected;
        lock (_lock)
        {
            return
                $"view vd={ViewDistance} pub={ChunkViewMath.PublisherRadiusBlocks(ViewDistance)}b " +
                $"chunk=({_currentChunkX},{_currentChunkZ}) " +
                $"loaded={_loadedChunks.Count}/{expected} " +
                $"req={_requestedChunks.Count} ready={_readyChunks.Count} " +
                $"scan={_scanRadius}/{ViewDistance} started={_started}" +
                (_awaitingTeleportChunkSync ? $" hold={_teleportHoldTicks}" : "");
        }
    }

    public void SetViewDistance(int distance)
    {
        ViewDistance = Math.Clamp(distance, 1, 120);
    }

    public void ApplyViewDistance(int distance)
    {
        lock (_lock)
        {
            int viewDistance = Math.Clamp(distance, 1, 120);
            if (ViewDistance == viewDistance)
            {
                return;
            }

            ViewDistance = viewDistance;
            ResetScan();
            _requestedChunks.Clear();
            _readyChunks.Clear();

            if (!_started || Player.Dimension is null)
            {
                return;
            }

            UpdateTrackedChunkPosition();
            UnloadChunks(Player.Dimension, clearClient: true);
            SendPublisherUpdate();
            SyncRegionPresence();
        }
    }

    public void StartChunkLoad()
    {
        lock (_lock)
        {
            _teleportHoldTicks = 0;
            _awaitingTeleportChunkSync = false;
            _started = true;
            UpdateTrackedChunkPosition();
            UpdateSpatialPlayerIndex();
            if (Player.Dimension is not null)
            {
                UpdateSimulationChunks(Player.Dimension);
            }
            ResetScan();
            SendPublisherUpdate();
            SyncRegionPresence();
            Log.Info(
                LogCategory.Orion,
                "[Teleport:Chunks] StartChunkLoad player={0} chunk=({1},{2}) vd={3} pub={4}b expected={5}",
                Player.Username,
                _currentChunkX,
                _currentChunkZ,
                ViewDistance,
                ChunkViewMath.PublisherRadiusBlocks(ViewDistance),
                ((ViewDistance * 2) + 1) * ((ViewDistance * 2) + 1));
        }
    }

    public void FlushClientChunks()
    {
        lock (_lock)
        {
            if (Player.Dimension is null)
            {
                return;
            }

            UnloadChunks(Player.Dimension, clearClient: true, force: true);
        }
    }

    public void ForceReloadViewDistance()
    {
        lock (_lock)
        {
            Log.Info(
                LogCategory.Orion,
                "[Teleport:Chunks] ForceReloadViewDistance player={0} started={1} chunk=({2},{3}) loaded={4} vd={5} hold={6}",
                Player.Username,
                _started,
                _currentChunkX,
                _currentChunkZ,
                _loadedChunks.Count,
                ViewDistance,
                _teleportHoldTicks);

            if (Player.Dimension is null)
            {
                return;
            }

            UnloadChunks(Player.Dimension, clearClient: true, force: true);
            _loadedChunks.Clear();
            _requestedChunks.Clear();
            _readyChunks.Clear();
            ResetScan();
            UpdateTrackedChunkPosition();
            UpdateSpatialPlayerIndex();
            UpdateSimulationChunks(Player.Dimension);
            SendPublisherUpdate();
            SyncRegionPresence(force: true);
            _started = true;
            // Brief hold so NetworkChunkPublisherUpdate / MovePlayer land before LevelChunks.
            ArmTeleportChunkHold(minTicks: 2);
        }
    }

    /// <summary>
    /// Called when AuthInput movement is accepted after a server teleport — client is at the destination.
    /// </summary>
    public void NotifyClientAtTeleportDestination()
    {
        lock (_lock)
        {
            if (!_awaitingTeleportChunkSync)
            {
                return;
            }

            Log.Info(
                LogCategory.Orion,
                "[Teleport:Chunks] clientCaughtUp player={0} chunk=({1},{2}) holdLeft={3}",
                Player.Username,
                _currentChunkX,
                _currentChunkZ,
                _teleportHoldTicks);
            ReleaseTeleportChunkHold(reason: "clientCaughtUp");
        }
    }

    void ArmTeleportChunkHold(int minTicks)
    {
        _awaitingTeleportChunkSync = true;
        _teleportHoldTicks = Math.Max(_teleportHoldTicks, Math.Max(minTicks, 20));
    }

    void ReleaseTeleportChunkHold(string reason)
    {
        if (!_awaitingTeleportChunkSync && _teleportHoldTicks <= 0)
        {
            return;
        }

        _awaitingTeleportChunkSync = false;
        _teleportHoldTicks = 0;
        Log.Info(
            LogCategory.Orion,
            "[Teleport:Chunks] teleportHold released player={0} reason={1} chunk=({2},{3}) loaded={4}",
            Player.Username,
            reason,
            _currentChunkX,
            _currentChunkZ,
            _loadedChunks.Count);
        ResetScan();
        SendPublisherUpdate();
    }

    /// <summary>
    /// Updates publisher after region handoff without clearing loaded client chunks.
    /// </summary>
    public void AfterRegionHandoff()
    {
        lock (_lock)
        {
            if (!_started || Player.Dimension is null)
            {
                return;
            }

            Dimension dimension = Player.Dimension;

            UpdateTrackedChunkPosition();
            SendPublisherUpdate();

            _lastPresenceArea = null;
            SyncRegionPresence(force: true);
            UpdateVisibleEntities(dimension);
        }
    }

    internal void InvalidateVisibleEntity(ulong runtimeId)
    {
        lock (_lock)
        {
            _visibleEntityUniqueIds.Remove(runtimeId);
        }
    }

    internal void InvalidateVisibleEntityByUniqueId(long entityUniqueId)
    {
        lock (_lock)
        {
            List<ulong> runtimeIds = [];
            foreach ((ulong runtimeId, long uniqueId) in _visibleEntityUniqueIds)
            {
                if (uniqueId == entityUniqueId)
                {
                    runtimeIds.Add(runtimeId);
                }
            }

            for (int i = 0; i < runtimeIds.Count; i++)
            {
                _visibleEntityUniqueIds.Remove(runtimeIds[i]);
            }
        }
    }

    internal static void InvalidateVisibleEntityForRemove(
        Dimension dimension,
        long entityUniqueId,
        global::Orion.Entity.Entity[]? except)
    {
        global::Orion.Server? server = (dimension.World?.Server as global::Orion.Server);
        if (server is null)
        {
            return;
        }

        foreach (PlayerSession session in server.Sessions.Values)
        {
            if (session.ActiveEntity is not Player observer || observer.Dimension != dimension)
            {
                continue;
            }

            if (except is not null && Array.IndexOf(except, observer) >= 0)
            {
                continue;
            }

            observer.GetTrait<PlayerChunkRenderingTrait>()?.InvalidateVisibleEntityByUniqueId(entityUniqueId);
        }
    }

    internal static void InvalidateVisibleEntity(
        Dimension dimension,
        ulong runtimeId,
        global::Orion.Entity.Entity[]? except)
    {
        global::Orion.Server? server = dimension.World?.Server as global::Orion.Server;
        if (server is null)
        {
            return;
        }

        foreach (PlayerSession session in server.Sessions.Values)
        {
            if (session.ActiveEntity is not Player observer || observer.Dimension != dimension)
            {
                continue;
            }

            if (except is not null && Array.IndexOf(except, observer) >= 0)
            {
                continue;
            }

            observer.GetTrait<PlayerChunkRenderingTrait>()?.InvalidateVisibleEntity(runtimeId);
        }
    }

    internal bool IsEntityVisible(ulong runtimeId)
    {
        lock (_lock)
        {
            return _visibleEntityUniqueIds.ContainsKey(runtimeId);
        }
    }

    internal void TrackVisibleEntity(ulong runtimeId, long uniqueId)
    {
        lock (_lock)
        {
            _visibleEntityUniqueIds[runtimeId] = uniqueId;
        }
    }

    public override void OnSpawn(EntitySpawnOptions details)
    {
        UpdateTrackedChunkPosition();
        UpdateSpatialPlayerIndex();
    }

    public override void OnTeleport(EntityTeleportOptions details)
    {
        lock (_lock)
        {
            ChunkCoord fromChunk = ChunkCoord.FromBlock(details.From.X, details.From.Z);
            ChunkCoord toChunk = ChunkCoord.FromBlock(details.To.X, details.To.Z);
            bool destinationRendered = _started && _loadedChunks.Contains(toChunk.Hash);
            bool needsFullReload = details.ForceFullChunkReload || !destinationRendered;

            Log.Info(
                LogCategory.Orion,
                "[Teleport:Chunks] OnTeleport player={0} fromChunk=({1},{2}) toChunk=({3},{4}) " +
                "loadedBefore={5} started={6} vd={7} fullReload={8} destRendered={9} force={10}",
                Player.Username,
                fromChunk.X,
                fromChunk.Z,
                toChunk.X,
                toChunk.Z,
                _loadedChunks.Count,
                _started,
                ViewDistance,
                needsFullReload,
                destinationRendered,
                details.ForceFullChunkReload);

            if (!needsFullReload)
            {
                ApplySoftTeleportChunkUpdate(toChunk);
                return;
            }

            HideAllVisibleEntities();

            if (Player.Dimension is not null)
            {
                // Drop old client chunks; destination LevelChunks wait for teleport hold so the
                // client does not discard them while still at the previous position.
                UnloadChunks(Player.Dimension, clearClient: true, force: true);
            }

            _loadedChunks.Clear();
            _requestedChunks.Clear();
            _readyChunks.Clear();
            _visibleEntityUniqueIds.Clear();
            _publisherChunkX = int.MinValue;
            _publisherChunkZ = int.MinValue;
            _currentChunkX = int.MinValue;
            _currentChunkZ = int.MinValue;
            _lastPresenceArea = null;
            UpdateTrackedChunkPosition();
            UpdateSpatialPlayerIndex();
            ResetScan();
            if (Player.Dimension is not null)
            {
                UpdateSimulationChunks(Player.Dimension);
            }
            SendPublisherUpdate();
            SyncRegionPresence(force: true);
            _started = true;
            // Hold LevelChunks until AuthInput is accepted at the destination (max ~20 session ticks).
            ArmTeleportChunkHold(minTicks: 4);
        }
    }

    /// <summary>
    /// Same-area teleport into an already-rendered column: keep client chunks, only retarget streaming.
    /// </summary>
    void ApplySoftTeleportChunkUpdate(ChunkCoord toChunk)
    {
        if (Player.Dimension is null)
        {
            return;
        }

        bool chunkChanged = UpdateChunkPosition(toChunk.X, toChunk.Z);
        if (chunkChanged)
        {
            UpdateSpatialPlayerIndex(toChunk);
            SyncRegionPresence();
            UnloadChunks(Player.Dimension, clearClient: true);
            UpdateSimulationChunks(Player.Dimension);
        }

        if (toChunk.X != _publisherChunkX || toChunk.Z != _publisherChunkZ)
        {
            SendPublisherUpdate();
        }
    }

    public override void OnMove(EntityMoveOptions details)
    {
        if (!_started || !Player.IsAlive || Player.Dimension is null)
        {
            return;
        }

        lock (_lock)
        {
            ChunkCoord chunkCoord = ChunkCoord.FromBlock(details.To.X, details.To.Z);
            int chunkX = chunkCoord.X;
            int chunkZ = chunkCoord.Z;

            bool chunkChanged = UpdateChunkPosition(chunkX, chunkZ);
            if (chunkChanged)
            {
                UpdateSpatialPlayerIndex(chunkCoord);
                SyncRegionPresence();
                UnloadChunks(Player.Dimension, clearClient: true);
                UpdateSimulationChunks(Player.Dimension);
            }

            // Publisher must track every chunk change (Geyser updateChunkPosition).
            if (chunkX != _publisherChunkX || chunkZ != _publisherChunkZ)
            {
                SendPublisherUpdate();
            }
        }
    }

    public override void OnTick(TraitOnTickDetails details)
    {
        if ((Player.Dimension?.World?.Server as global::Orion.Server)?.Properties.SessionThreadingEnabled == true)
        {
            return;
        }

        RunChunkStreamingTick();
    }

    public override void OnDespawn(EntityDespawnOptions details)
    {
        Clear();
    }

    public override void OnRemove()
    {
        Clear();
    }

    public override EntityTrait Clone(Entity entity)
    {
        PlayerChunkRenderingTrait trait = new(entity);
        trait.SetViewDistance(ViewDistance);
        return trait;
    }

    private void SendChunks(Dimension dimension)
    {
        List<DataPacket> packets = [];
        List<(long Hash, int X, int Z)> sentChunks = [];

        SendReadyChunks(packets, sentChunks);
        RequestChunks(dimension);
        SendReadyChunks(packets, sentChunks);

        if (packets.Count == 0)
        {
            return;
        }

        Player.Send([.. packets]);

        foreach ((long hash, int x, int z) in sentChunks)
        {
            if (!_loadedChunks.Add(hash))
            {
                continue;
            }

            dimension.AddChunkViewer(x, z);
            SendChunkChestVisualUpdates(dimension, x, z);
        }

        // Routine SendChunks spam omitted — keep StartChunkLoad / ForceReload / OnTeleport / hold logs.
    }

    private void RequestChunks(Dimension dimension)
    {
        Span<(int X, int Z)> requests = stackalloc (int X, int Z)[ChunksPerTick];
        int requestCount = 0;

        while (requestCount < ChunksPerTick && NextChunkPosition(out int x, out int z))
        {
            long hash = new ChunkCoord(x, z).Hash;
            if (_loadedChunks.Contains(hash) || _requestedChunks.Contains(hash))
            {
                continue;
            }

            _requestedChunks.Add(hash);
            requests[requestCount++] = (x, z);
        }

        if (requestCount == 0)
        {
            return;
        }

        dimension.RequestChunks(requests[..requestCount], chunk =>
        {
            lock (_lock)
            {
                if (!_started || Player.Dimension != dimension || !_requestedChunks.Contains(chunk.Hash))
                {
                    return;
                }

                if (_loadedChunks.Contains(chunk.Hash) || !ChunkInRange(chunk.X, chunk.Z))
                {
                    _requestedChunks.Remove(chunk.Hash);
                    return;
                }

                _readyChunks.Enqueue(chunk);
            }
        });
    }

    private void SendReadyChunks(List<DataPacket> packets, List<(long Hash, int X, int Z)> sentChunks)
    {
        while (packets.Count < ChunksPerTick && _readyChunks.Count > 0)
        {
            ChunkColumn chunk = _readyChunks.Dequeue();
            _requestedChunks.Remove(chunk.Hash);

            if (_loadedChunks.Contains(chunk.Hash) || !ChunkInRange(chunk.X, chunk.Z))
            {
                continue;
            }

            byte[] payload;

            try
            {
                payload = ChunkColumn.Serialize(chunk);
            }
            catch (Exception exception)
            {
                Err($"Failed to serialize chunk {chunk.X}, {chunk.Z}: {exception.Message}");
                continue;
            }

            packets.Add(new LevelChunkPacket
            {
                ChunkX = chunk.X,
                ChunkZ = chunk.Z,
                Dimension = (int)chunk.Type,
                SubChunkCount = (uint)chunk.GetSubChunkSendCount(),
                CacheEnabled = false,
                RawPayload = payload
            });

            sentChunks.Add((chunk.Hash, chunk.X, chunk.Z));
        }
    }

    private void UnloadChunks(Dimension dimension, bool clearClient, bool force = false)
    {
        if (_loadedChunks.Count == 0)
        {
            return;
        }

        List<long> unloadedChunks = [];

        foreach (long hash in _loadedChunks)
        {
            ChunkCoord chunkCoord = ChunkCoord.FromHash(hash);
            int x = chunkCoord.X;
            int z = chunkCoord.Z;

            if (!force && ChunkInRange(x, z))
            {
                continue;
            }

            if (clearClient)
            {
                Player.Send(new LevelChunkPacket
                {
                    ChunkX = x,
                    ChunkZ = z,
                    Dimension = (int)dimension.Type,
                    SubChunkCount = 0,
                    CacheEnabled = false,
                    RawPayload = []
                });
            }

            dimension.RemoveChunkViewer(x, z);

            if (!dimension.HasChunkViewers(x, z))
            {
                dimension.UnloadChunk(x, z);
            }

            unloadedChunks.Add(hash);
        }

        for (int i = 0; i < unloadedChunks.Count; i++)
        {
            _loadedChunks.Remove(unloadedChunks[i]);
        }
    }

    private bool UpdateChunkPosition(int chunkX, int chunkZ)
    {
        if (chunkX == _currentChunkX && chunkZ == _currentChunkZ)
        {
            return false;
        }

        _currentChunkX = chunkX;
        _currentChunkZ = chunkZ;
        ResetScan();
        return true;
    }

    private void ResetScan()
    {
        _scanRadius = 0;
        _scanX = 0;
        _scanZ = 0;
    }

    private bool NextChunkPosition(out int x, out int z)
    {
        while (_scanRadius <= ViewDistance)
        {
            if (_scanRadius == 0)
            {
                _scanRadius = 1;
                _scanX = -1;
                _scanZ = -1;
                x = _currentChunkX;
                z = _currentChunkZ;
                return true;
            }

            while (_scanZ <= _scanRadius)
            {
                while (_scanX <= _scanRadius)
                {
                    int offsetX = _scanX++;
                    int offsetZ = _scanZ;

                    if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetZ)) != _scanRadius)
                    {
                        continue;
                    }

                    x = _currentChunkX + offsetX;
                    z = _currentChunkZ + offsetZ;
                    return true;
                }

                _scanX = -_scanRadius;
                _scanZ++;
            }

            _scanRadius++;
            _scanX = -_scanRadius;
            _scanZ = -_scanRadius;
        }

        x = 0;
        z = 0;
        return false;
    }

    private void Clear()
    {
        lock (_lock)
        {
            HideAllVisibleEntities();

            if (Player.Session is not null && Player.Dimension is not null)
            {
                Player.Dimension.GetSpatialIndex().RemovePlayer(Player.Session);
            }

            if (Player.Dimension is not null)
            {
                UnloadChunks(Player.Dimension, clearClient: false, force: true);
            }

            _loadedChunks.Clear();
            _requestedChunks.Clear();
            _readyChunks.Clear();
            _visibleEntityUniqueIds.Clear();
            _started = false;
            _currentChunkX = int.MinValue;
            _currentChunkZ = int.MinValue;
            _publisherChunkX = int.MinValue;
            _publisherChunkZ = int.MinValue;
            ResetScan();
        }
    }

    private void UpdateTrackedChunkPosition()
    {
        ChunkCoord chunk = ChunkCoord.FromBlock(Player.Position.X, Player.Position.Z);
        _currentChunkX = chunk.X;
        _currentChunkZ = chunk.Z;

        if (_publisherChunkX == int.MinValue)
        {
            _publisherChunkX = _currentChunkX;
            _publisherChunkZ = _currentChunkZ;
        }
    }

    private void UpdateSpatialPlayerIndex()
    {
        ChunkCoord chunk = new(_currentChunkX, _currentChunkZ);
        UpdateSpatialPlayerIndex(chunk);
    }

    private void UpdateSpatialPlayerIndex(ChunkCoord chunk)
    {
        if (Player.Session is null || Player.Dimension is null)
        {
            return;
        }

        Player.Dimension.GetSpatialIndex().SetPlayerChunk(Player.Session, chunk);
    }

    private void UpdateSimulationChunks(Dimension dimension)
    {
        int simulationDistance = Math.Clamp((dimension.World?.Server as global::Orion.Server)?.Properties.SimulationDistance ?? 4, 0, 120);

        for (int dx = -simulationDistance; dx <= simulationDistance; dx++)
        {
            for (int dz = -simulationDistance; dz <= simulationDistance; dz++)
            {
                int x = _currentChunkX + dx;
                int z = _currentChunkZ + dz;
                ChunkColumn? chunk = dimension.GetChunk(x, z);
                if (chunk is not null)
                {
                    chunk.Simulated = true;
                }
            }
        }
    }

    private void SendPublisherUpdate()
    {
        NetworkChunkPublisherUpdatePacket packet = CreateChunkPublisherPacket();
        Player.Send(packet);
        _publisherChunkX = _currentChunkX;
        _publisherChunkZ = _currentChunkZ;
    }

    private NetworkChunkPublisherUpdatePacket CreateChunkPublisherPacket()
    {
        // SavedChunks is only for ClientSideGeneration (docs). Orion has CSG off — always empty.
        // Radius matches Geyser: squareToCircle(viewDistance) << 4.
        return new NetworkChunkPublisherUpdatePacket
        {
            CoordinateX = (int)MathF.Floor(Player.Position.X),
            CoordinateY = (int)MathF.Floor(Player.Position.Y),
            CoordinateZ = (int)MathF.Floor(Player.Position.Z),
            Radius = ChunkViewMath.PublisherRadiusBlocks(ViewDistance),
            SavedChunks = []
        };
    }

    private bool ChunkInRange(int x, int z)
    {
        int dx = x - _currentChunkX;
        int dz = z - _currentChunkZ;
        return Math.Max(Math.Abs(dx), Math.Abs(dz)) <= ViewDistance;
    }

    private void UpdateVisibleEntities(Dimension dimension)
    {
        ulong tick = dimension.World is Tickable tickable ? tickable.TickValue : 0;
        HashSet<ulong> currentVisible = [];

        foreach (Entity entity in dimension.GetEntities())
        {
            if (ReferenceEquals(entity, Player))
            {
                continue;
            }

            if (!entity.IsAlive || entity.PendingDespawn || entity.Dimension != dimension)
            {
                continue;
            }

            ChunkCoord entityChunk = ChunkCoord.FromBlock(entity.Position.X, entity.Position.Z);
            long hash = entityChunk.Hash;

            if (!_loadedChunks.Contains(hash))
            {
                continue;
            }

            currentVisible.Add(entity.RuntimeId);

            if (_visibleEntityUniqueIds.ContainsKey(entity.RuntimeId))
            {
                continue;
            }

            entity.SpawnTo(Player, tick);
            _visibleEntityUniqueIds[entity.RuntimeId] = entity.UniqueId;
        }

        if (_visibleEntityUniqueIds.Count == 0)
        {
            return;
        }

        List<ulong> hidden = [];
        foreach ((ulong runtimeId, long uniqueId) in _visibleEntityUniqueIds)
        {
            if (currentVisible.Contains(runtimeId))
            {
                continue;
            }

            // Entity is between shards during cross-worker handoff — keep peer visibility.
            if (CrossAreaTransferHandler.IsTransferInFlight(runtimeId))
            {
                continue;
            }

            Player.Send(new RemoveActorPacket
            {
                EntityUniqueId = uniqueId
            });

            hidden.Add(runtimeId);
        }

        for (int i = 0; i < hidden.Count; i++)
        {
            _visibleEntityUniqueIds.Remove(hidden[i]);
        }
    }

    private void HideAllVisibleEntities()
    {
        foreach ((_, long uniqueId) in _visibleEntityUniqueIds)
        {
            Player.Send(new RemoveActorPacket
            {
                EntityUniqueId = uniqueId
            });
        }
    }

    private void SendChunkChestVisualUpdates(Dimension dimension, int chunkX, int chunkZ)
    {
        ChunkColumn? chunk = dimension.GetChunk(chunkX, chunkZ);
        if (chunk is null)
        {
            return;
        }

        foreach (BlockLevelStorage storage in chunk.GetAllBlockStorages())
        {
            BlockPos position = storage.GetPosition();
            var block = dimension.GetBlock(position.X, position.Y, position.Z);
            block?.OnRender(Player, position.X, position.Y, position.Z);
        }
    }

    private void SyncRegionPresence(bool force = false)
    {
        if (Player.Dimension is not Dimension dimension)
        {
            return;
        }

        global::Orion.Server? server = (dimension.World?.Server as global::Orion.Server);
        if (server is null)
        {
            return;
        }

        int centerRegion = dimension.ShardManager.ResolveShard((int)Player.Position.X >> 4, (int)Player.Position.Z >> 4).AreaIndex;
        if (!force && _lastPresenceArea == centerRegion)
        {
            return;
        }

        _lastPresenceArea = centerRegion;
        int simulationDistance = Math.Clamp(server.Properties.SimulationDistance, 0, 120);
        AreaPlayerPresence.SyncViewHalo(server, dimension, Player, ViewDistance, simulationDistance);
    }

    public void OnSessionTick()
    {
        RunChunkStreamingTick();
    }

    void RunChunkStreamingTick()
    {
        if (!_started || !Player.IsAlive || Player.Dimension is null)
        {
            return;
        }

        lock (_lock)
        {
            if (_awaitingTeleportChunkSync || _teleportHoldTicks > 0)
            {
                if (_teleportHoldTicks > 0)
                {
                    _teleportHoldTicks--;
                }

                if (_teleportHoldTicks <= 0)
                {
                    ReleaseTeleportChunkHold(reason: "timeout");
                }
                else
                {
                    return;
                }
            }

            Dimension dimension = Player.Dimension;
            ChunkCoord playerChunk = ChunkCoord.FromBlock(Player.Position.X, Player.Position.Z);
            int chunkX = playerChunk.X;
            int chunkZ = playerChunk.Z;

            bool chunkChanged = UpdateChunkPosition(chunkX, chunkZ);
            UpdateSpatialPlayerIndex(playerChunk);
            if (chunkChanged)
            {
                SyncRegionPresence();
            }

            // Publisher before LevelChunks when the view center moves (Geyser order).
            if (chunkX != _publisherChunkX || chunkZ != _publisherChunkZ)
            {
                SendPublisherUpdate();
            }

            UnloadChunks(dimension, clearClient: true);
            UpdateSimulationChunks(dimension);
            SendChunks(dimension);
            UpdateVisibleEntities(dimension);
        }
    }
}
