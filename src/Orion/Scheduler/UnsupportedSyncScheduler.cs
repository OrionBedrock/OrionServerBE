namespace Orion.Scheduler;

/// <summary>
/// Folia check: legacy Bukkit-style sync "main thread" scheduling is unsupported.
/// </summary>
public static class UnsupportedSyncScheduler
{
    public static void RunTask(Action action)
    {
        _ = action;
        throw new NotSupportedException(
            "Sync main-thread scheduling is unsupported. Use RegionScheduler, EntityScheduler, GlobalRegionScheduler, or AsyncScheduler.");
    }

    public static void RunTaskLater(Action action, long delayTicks)
    {
        _ = action;
        _ = delayTicks;
        throw new NotSupportedException(
            "Sync main-thread scheduling is unsupported. Use RegionScheduler, EntityScheduler, GlobalRegionScheduler, or AsyncScheduler.");
    }

    public static void RunTaskTimer(Action action, long delayTicks, long periodTicks)
    {
        _ = action;
        _ = delayTicks;
        _ = periodTicks;
        throw new NotSupportedException(
            "Sync main-thread scheduling is unsupported. Use RegionScheduler, EntityScheduler, GlobalRegionScheduler, or AsyncScheduler.");
    }
}
