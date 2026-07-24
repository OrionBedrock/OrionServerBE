using Orion.Scheduler;
using Orion.World;

namespace Orion.Entity;

/// <summary>
/// Minimal gameplay entity handle (traits + chunk position). Full registries arrive later.
/// </summary>
public sealed class Entity : ISchedulableEntity
{
    private readonly object _sync = new();
    private bool _removed;

    public Entity(long entityId, Dimension dimension, int chunkX, int chunkZ, string worldId)
    {
        EntityId = entityId;
        Dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
        WorldId = string.IsNullOrWhiteSpace(worldId) ? "default" : worldId;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        Traits = new TraitBag(this);
    }

    public long EntityId { get; }

    public string WorldId { get; }

    public Dimension Dimension { get; }

    public int ChunkX { get; private set; }

    public int ChunkZ { get; private set; }

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
        }

        Traits.NotifyChunkPositionChanged(chunkX, chunkZ);
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
        }

        Traits.DetachAll();
    }
}
