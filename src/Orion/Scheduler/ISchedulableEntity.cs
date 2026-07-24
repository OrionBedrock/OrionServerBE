namespace Orion.Scheduler;

/// <summary>
/// Minimal schedulable entity handle for EntityScheduler (not a gameplay entity).
/// </summary>
public interface ISchedulableEntity
{
    long EntityId { get; }

    string WorldId { get; }

    int ChunkX { get; }

    int ChunkZ { get; }

    bool IsRemoved { get; }
}

public sealed class SchedulableEntityStub : ISchedulableEntity
{
    public SchedulableEntityStub(long entityId, int chunkX, int chunkZ, string worldId = "default")
    {
        EntityId = entityId;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        WorldId = worldId;
    }

    public long EntityId { get; }

    public string WorldId { get; }

    public int ChunkX { get; set; }

    public int ChunkZ { get; set; }

    public bool IsRemoved { get; private set; }

    public void Remove() => IsRemoved = true;
}
