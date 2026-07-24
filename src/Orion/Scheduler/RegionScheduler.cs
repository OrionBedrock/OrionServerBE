using Orion.Region;

namespace Orion.Scheduler;

/// <summary>
/// Folia RegionScheduler: schedule work for the region owning (world, chunkX, chunkZ).
/// Not for following entities — use EntityScheduler.
/// </summary>
public sealed class RegionScheduler
{
    private readonly Regionizer _regionizer;

    public RegionScheduler(Regionizer regionizer)
    {
        _regionizer = regionizer ?? throw new ArgumentNullException(nameof(regionizer));
    }

    public void Execute(string worldId, int chunkX, int chunkZ, Action action)
    {
        _ = worldId;
        ArgumentNullException.ThrowIfNull(action);
        ChunkRegion region = RequireRegion(chunkX, chunkZ);
        region.SchedulerTasks.Enqueue(action, delayTicks: 0);
    }

    public ScheduledTask Run(string worldId, int chunkX, int chunkZ, Action action)
        => RunDelayed(worldId, chunkX, chunkZ, action, delayTicks: 0);

    public ScheduledTask RunDelayed(string worldId, int chunkX, int chunkZ, Action action, long delayTicks)
    {
        _ = worldId;
        ArgumentNullException.ThrowIfNull(action);
        ChunkRegion region = RequireRegion(chunkX, chunkZ);
        return region.SchedulerTasks.Enqueue(action, delayTicks);
    }

    public ScheduledTask RunAtFixedRate(
        string worldId,
        int chunkX,
        int chunkZ,
        Action action,
        long initialDelayTicks,
        long periodTicks)
    {
        _ = worldId;
        ArgumentNullException.ThrowIfNull(action);
        if (periodTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodTicks), "Period must be positive.");
        }

        ChunkRegion region = RequireRegion(chunkX, chunkZ);
        return region.SchedulerTasks.Enqueue(action, initialDelayTicks, periodTicks);
    }

    private ChunkRegion RequireRegion(int chunkX, int chunkZ)
    {
        ChunkRegion? region = _regionizer.GetRegionAt(chunkX, chunkZ);
        if (region is null || !region.IsAlive)
        {
            throw new InvalidOperationException(
                $"No region owns chunk ({chunkX},{chunkZ}). Load the chunk into the regionizer before scheduling.");
        }

        return region;
    }
}
