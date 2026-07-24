using Orion.World;
using Orion.World.Tickets;

namespace Orion.Entity.Traits;

/// <summary>
/// Simulation ticket source with a mutable Chebyshev radius. Does not stream LevelChunk to clients.
/// </summary>
public sealed class ChunkLoadingTrait : IEntityTrait, IChunkPositionAware
{
    private readonly Entity _entity;
    private readonly object _sync = new();
    private readonly Dictionary<(int X, int Z), SimulationTicket> _tickets = new();
    private bool _enabled;
    private int _radiusChunks;
    private bool _detached;

    public ChunkLoadingTrait(Entity entity)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public bool IsEnabled
    {
        get
        {
            lock (_sync)
            {
                return _enabled;
            }
        }
    }

    public int RadiusChunks
    {
        get
        {
            lock (_sync)
            {
                return _radiusChunks;
            }
        }
    }

    public int HeldTicketCount
    {
        get
        {
            lock (_sync)
            {
                return _tickets.Count;
            }
        }
    }

    public void Enable(int radiusChunks)
    {
        if (radiusChunks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusChunks), "Radius must be non-negative.");
        }

        lock (_sync)
        {
            EnsureAttached();
            _enabled = true;
            _radiusChunks = radiusChunks;
            ResyncUnlocked();
        }
    }

    public void SetRadius(int radiusChunks)
    {
        if (radiusChunks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusChunks), "Radius must be non-negative.");
        }

        lock (_sync)
        {
            EnsureAttached();
            if (!_enabled)
            {
                throw new InvalidOperationException("ChunkLoadingTrait is not enabled.");
            }

            _radiusChunks = radiusChunks;
            ResyncUnlocked();
        }
    }

    public void Disable()
    {
        lock (_sync)
        {
            if (!_enabled && _tickets.Count == 0)
            {
                return;
            }

            _enabled = false;
            ReleaseAllUnlocked();
        }
    }

    public void OnChunkPositionChanged(int chunkX, int chunkZ)
    {
        _ = chunkX;
        _ = chunkZ;
        lock (_sync)
        {
            if (!_enabled || _detached)
            {
                return;
            }

            ResyncUnlocked();
        }
    }

    public void OnDetach()
    {
        lock (_sync)
        {
            _detached = true;
            _enabled = false;
            ReleaseAllUnlocked();
        }
    }

    private void ResyncUnlocked()
    {
        Dimension dimension = _entity.Dimension;
        int centerX = _entity.ChunkX;
        int centerZ = _entity.ChunkZ;
        int radius = _radiusChunks;

        var desired = new HashSet<(int X, int Z)>();
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                // Chebyshev: max(|dx|,|dz|) <= radius (square).
                desired.Add((centerX + dx, centerZ + dz));
            }
        }

        foreach ((int X, int Z) key in _tickets.Keys.ToArray())
        {
            if (desired.Contains(key))
            {
                continue;
            }

            if (_tickets.Remove(key, out SimulationTicket? ticket))
            {
                ticket.Dispose();
            }
        }

        foreach ((int X, int Z) key in desired)
        {
            if (_tickets.ContainsKey(key))
            {
                continue;
            }

            _tickets[key] = dimension.AcquireTicket(key.X, key.Z);
        }
    }

    private void ReleaseAllUnlocked()
    {
        foreach (SimulationTicket ticket in _tickets.Values)
        {
            ticket.Dispose();
        }

        _tickets.Clear();
    }

    private void EnsureAttached()
    {
        if (_detached || _entity.IsRemoved)
        {
            throw new InvalidOperationException("Cannot use ChunkLoadingTrait on a removed entity.");
        }
    }
}
