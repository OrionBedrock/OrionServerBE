using Orion.Region;
using Orion.Scheduler;
using Orion.World;

namespace Orion.Entity;

/// <summary>
/// Minimal gameplay entity handle (traits + world position).
/// </summary>
public sealed class Entity : ISchedulableEntity
{
    private readonly object _sync = new();
    private bool _removed;
    private bool _teleporting;

    public Entity(long entityId, Dimension dimension, int chunkX, int chunkZ, string worldId)
    {
        EntityId = entityId;
        Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
        WorldId = string.IsNullOrWhiteSpace(worldId) ? "default" : worldId;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        X = chunkX * 16.0 + 0.5;
        Y = 64.0;
        Z = chunkZ * 16.0 + 0.5;
        Traits = new TraitBag(this);
    }

    public long EntityId { get; }

    public string WorldId { get; }

    public Dimension Dimension { get; }

    public double X { get; private set; }

    public double Y { get; private set; }

    public double Z { get; private set; }

    public int ChunkX { get; private set; }

    public int ChunkZ { get; private set; }

    public bool IsTeleporting
    {
        get
        {
            lock (_sync)
            {
                return _teleporting;
            }
        }
    }

    public bool IsRemoved
    {
        get
        {
            lock (_sync)
            {
                return _removed;
            }
        }
    }

    public TraitBag Traits { get; }

    public void SetChunkPosition(int chunkX, int chunkZ)
    {
        lock (_sync)
        {
            if (_removed)
            {
                return;
            }

            if (ChunkX == chunkX && ChunkZ == chunkZ)
            {
                return;
            }

            ChunkX = chunkX;
            ChunkZ = chunkZ;
            X = chunkX * 16.0 + 0.5;
            Z = chunkZ * 16.0 + 0.5;
        }

        Traits.NotifyChunkPositionChanged(chunkX, chunkZ);
    }

    /// <summary>
    /// Continuous / same-tick move. Caller must be on the owning region tick thread.
    /// Returns false (edge skip) when the destination chunk is not owned by the current region.
    /// </summary>
    public bool TryMove(Regionizer regionizer, double x, double y, double z)
    {
        ArgumentNullException.ThrowIfNull(regionizer);

        int destCx = (int)Math.Floor(x) >> 4;
        int destCz = (int)Math.Floor(z) >> 4;

        lock (_sync)
        {
            if (_removed || _teleporting)
            {
                return false;
            }
        }

        if (!RegionOwnership.IsOwnedByCurrentRegion(regionizer, destCx, destCz))
        {
            return false;
        }

        int chunkX;
        int chunkZ;
        bool chunkChanged;
        lock (_sync)
        {
            if (_removed || _teleporting)
            {
                return false;
            }

            chunkX = destCx;
            chunkZ = destCz;
            chunkChanged = ChunkX != chunkX || ChunkZ != chunkZ;
            X = x;
            Y = y;
            Z = z;
            ChunkX = chunkX;
            ChunkZ = chunkZ;
        }

        if (chunkChanged)
        {
            Traits.NotifyChunkPositionChanged(chunkX, chunkZ);
        }

        return true;
    }

    /// <summary>
    /// Folia teleportAsync: transform on owning origin region → place on destination region.
    /// Origin and destination chunks must already have live regions.
    /// </summary>
    public async ValueTask<bool> TeleportAsync(
        RegionScheduler regions,
        double x,
        double y,
        double z,
        Action? retired = null)
    {
        ArgumentNullException.ThrowIfNull(regions);

        int originCx;
        int originCz;
        lock (_sync)
        {
            if (_removed)
            {
                retired?.Invoke();
                return false;
            }

            originCx = ChunkX;
            originCz = ChunkZ;
        }

        int destCx = (int)Math.Floor(x) >> 4;
        int destCz = (int)Math.Floor(z) >> 4;

        bool transformed = await regions.RunAsync(WorldId, originCx, originCz, () =>
        {
            lock (_sync)
            {
                if (_removed)
                {
                    return false;
                }

                _teleporting = true;
                X = x;
                Y = y;
                Z = z;
                return true;
            }
        }).ConfigureAwait(false);

        if (!transformed)
        {
            retired?.Invoke();
            return false;
        }

        bool placed;
        try
        {
            placed = await regions.RunAsync(WorldId, destCx, destCz, () => ApplyPlace(x, y, z)).ConfigureAwait(false);
        }
        catch
        {
            lock (_sync)
            {
                _teleporting = false;
            }

            throw;
        }

        if (!placed)
        {
            retired?.Invoke();
            return false;
        }

        return true;
    }

    internal bool ApplyPlace(double x, double y, double z)
    {
        int chunkX;
        int chunkZ;
        bool chunkChanged;
        lock (_sync)
        {
            if (_removed)
            {
                _teleporting = false;
                return false;
            }

            chunkX = (int)Math.Floor(x) >> 4;
            chunkZ = (int)Math.Floor(z) >> 4;
            chunkChanged = ChunkX != chunkX || ChunkZ != chunkZ;
            X = x;
            Y = y;
            Z = z;
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            _teleporting = false;
        }

        if (chunkChanged)
        {
            Traits.NotifyChunkPositionChanged(chunkX, chunkZ);
        }

        return true;
    }

    public void Remove()
    {
        lock (_sync)
        {
            if (_removed)
            {
                return;
            }

            _removed = true;
            _teleporting = false;
        }

        Traits.DetachAll();
    }
}
