using System.Collections.Concurrent;
using Orion.Config;
using Orion.Network;
using Orion.Permissions;
using Orion.Region;
using Orion.World;
using Orion.World.Persistence;
using Orion.World.Provider;
using EntityHandle = Orion.Entity.Entity;
using RakNet;

namespace Orion.Player;

public sealed class PlayerManager
{
    private readonly ConcurrentDictionary<NetworkConnection, Player> _byConnection = new();
    private readonly ConcurrentDictionary<long, Player> _byId = new();
    private long _nextEntityId = 1;
    private IWorldProvider? _provider;
    private PlayerPersistence? _persistence;

    public IEnumerable<Player> All => _byId.Values;

    public int Count => _byId.Count;

    public void BindPersistence(IWorldProvider provider, PlayerPersistence? persistence)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _persistence = persistence;
    }

    public Player Create(
        ConnectionSession session,
        Orion.World.World world,
        Regionizer regionizer,
        DimensionConfig dimensionConfig,
        PermissionService? permissions = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(regionizer);
        ArgumentNullException.ThrowIfNull(dimensionConfig);

        if (_byConnection.ContainsKey(session.Connection))
        {
            throw new InvalidOperationException("Session already has a player.");
        }

        Dimension dimension = world.GetDimension(dimensionConfig.Identifier);
        int[] spawn = dimensionConfig.SpawnPosition is { Length: >= 3 }
            ? dimensionConfig.SpawnPosition
            : [0, 64, 0];

        float x = spawn[0];
        float y = spawn[1];
        float z = spawn[2];
        int chunkX = (int)Math.Floor(x / 16f);
        int chunkZ = (int)Math.Floor(z / 16f);

        long id = Interlocked.Increment(ref _nextEntityId);
        var entity = new EntityHandle(id, dimension, chunkX, chunkZ, world.Identifier);
        var player = new Player(entity, session, regionizer, x, y, z);
        player.EnableSimulationTickets(Math.Max(0, dimensionConfig.SimulationDistance));

        PermissionService service = permissions ?? PermissionService.CreateEmpty();
        player.ApplyPermissions(service.Resolve(session.Username, session.Xuid));

        LoadPlayerData(player);

        _byConnection[session.Connection] = player;
        _byId[id] = player;
        session.Player = player;
        return player;
    }

    public bool TryGet(NetworkConnection connection, out Player? player)
        => _byConnection.TryGetValue(connection, out player);

    public bool TryGet(ConnectionSession session, out Player? player)
        => TryGet(session.Connection, out player);

    public void Remove(NetworkConnection connection)
    {
        if (!_byConnection.TryRemove(connection, out Player? player))
        {
            return;
        }

        _byId.TryRemove(player.UniqueId, out _);
        if (ReferenceEquals(player.Session.Player, player))
        {
            player.Session.Player = null;
        }

        ScheduleSave(player);
        player.Remove();
    }

    public void FlushDirtyPlayers()
    {
        foreach (Player player in _byId.Values)
        {
            ScheduleSave(player);
        }

        _persistence?.Flush(TimeSpan.FromSeconds(10));
    }

    public void TickAllRegions()
    {
        foreach (Player player in _byId.Values)
        {
            player.TickRegion();
        }
    }

    private void LoadPlayerData(Player player)
    {
        IWorldProvider? provider = _provider ?? player.Dimension.Provider;
        string? xuid = ResolveXuid(player);
        if (xuid is null || !provider.TryLoadPlayerBlob(xuid, out byte[]? blob) || blob is null)
        {
            return;
        }

        player.Data.LoadFromSnapshot(blob);
    }

    private void ScheduleSave(Player player)
    {
        if (_persistence is null || !player.Data.IsDirty)
        {
            return;
        }

        string? xuid = ResolveXuid(player);
        if (xuid is null)
        {
            return;
        }

        _persistence.ScheduleSave(xuid, player.Data);
    }

    private static string? ResolveXuid(Player player)
    {
        if (!string.IsNullOrWhiteSpace(player.Session.Xuid))
        {
            return player.Session.Xuid;
        }

        return null;
    }
}
