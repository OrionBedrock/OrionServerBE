using System.Collections.Concurrent;
using Orion.Config;
using Orion.Network;
using Orion.Region;
using Orion.World;
using EntityHandle = Orion.Entity.Entity;
using RakNet;

namespace Orion.Player;

public sealed class PlayerManager
{
    private readonly ConcurrentDictionary<NetworkConnection, Player> _byConnection = new();
    private readonly ConcurrentDictionary<long, Player> _byId = new();
    private long _nextEntityId = 1;

    public IEnumerable<Player> All => _byId.Values;

    public int Count => _byId.Count;

    public Player Create(
        ConnectionSession session,
        Orion.World.World world,
        Regionizer regionizer,
        DimensionConfig dimensionConfig)
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

        player.Remove();
    }

    public void TickAllRegions()
    {
        foreach (Player player in _byId.Values)
        {
            player.TickRegion();
        }
    }
}
