using Orion.Region;

namespace Orion.Scheduler;

/// <summary>
/// Folia GlobalRegionScheduler: only runs on the global region tick.
/// </summary>
public sealed class GlobalRegionScheduler
{
    private readonly GlobalRegion _global;
    private readonly SchedulerTaskQueue _queue = new();

    public GlobalRegionScheduler(GlobalRegion global)
    {
        _global = global ?? throw new ArgumentNullException(nameof(global));
    }

    public GlobalRegion Global => _global;

    public void Execute(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _queue.Enqueue(() =>
        {
            _global.EnsureCurrentThread();
            action();
        }, delayTicks: 0);
    }

    public ScheduledTask Run(Action action)
        => RunDelayed(action, delayTicks: 0);

    public ScheduledTask RunDelayed(Action action, long delayTicks)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _queue.Enqueue(() =>
        {
            _global.EnsureCurrentThread();
            action();
        }, delayTicks);
    }

    public ScheduledTask RunAtFixedRate(Action action, long initialDelayTicks, long periodTicks)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (periodTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodTicks));
        }

        return _queue.Enqueue(() =>
        {
            _global.EnsureCurrentThread();
            action();
        }, initialDelayTicks, periodTicks);
    }

    public void CancelTasks() => _queue.CancelAll();

    /// <summary>
    /// Drain tick-delayed global tasks. Must run on the global tick thread.
    /// </summary>
    public void Tick()
    {
        _global.EnsureCurrentThread();
        _queue.DrainOneTick();
    }
}
