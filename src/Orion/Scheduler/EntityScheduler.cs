using Orion.Region;

namespace Orion.Scheduler;

/// <summary>
/// Folia EntityScheduler: tasks follow an entity; retired runs if the entity is removed.
/// </summary>
public sealed class EntityScheduler
{
    private readonly Regionizer _regionizer;
    private readonly ISchedulableEntity _entity;
    private readonly List<ScheduledTask> _tasks = new();
    private readonly object _sync = new();

    public EntityScheduler(Regionizer regionizer, ISchedulableEntity entity)
    {
        _regionizer = regionizer ?? throw new ArgumentNullException(nameof(regionizer));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public ISchedulableEntity Entity => _entity;

    public bool Execute(Action run, Action? retired = null, long delayTicks = 0)
    {
        ScheduledTask? task = RunDelayed(run, delayTicks, retired);
        return task is not null;
    }

    public ScheduledTask? Run(Action run, Action? retired = null)
        => RunDelayed(run, delayTicks: 0, retired);

    public ScheduledTask? RunDelayed(Action run, long delayTicks, Action? retired = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (_entity.IsRemoved)
        {
            retired?.Invoke();
            return null;
        }

        ChunkRegion region = RequireRegion();
        ScheduledTask task = region.SchedulerTasks.Enqueue(
            () => ExecuteWrapped(run, retired),
            delayTicks,
            periodTicks: 0,
            retired: () => retired?.Invoke());

        Track(task);
        return task;
    }

    public ScheduledTask? RunAtFixedRate(Action run, long initialDelayTicks, long periodTicks, Action? retired = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (periodTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodTicks));
        }

        if (_entity.IsRemoved)
        {
            retired?.Invoke();
            return null;
        }

        ChunkRegion region = RequireRegion();
        ScheduledTask task = region.SchedulerTasks.Enqueue(
            () => ExecuteWrapped(run, retired),
            initialDelayTicks,
            periodTicks,
            retired: () => retired?.Invoke());

        Track(task);
        return task;
    }

    /// <summary>
    /// Marks the entity removed and retires pending tasks (never runs action + retired together).
    /// </summary>
    public void Retire()
    {
        if (_entity is SchedulableEntityStub stub)
        {
            stub.Remove();
        }

        ScheduledTask[] snapshot;
        lock (_sync)
        {
            snapshot = _tasks.ToArray();
            _tasks.Clear();
        }

        foreach (ScheduledTask task in snapshot)
        {
            task.TryRetire();
        }
    }

    private void ExecuteWrapped(Action run, Action? retired)
    {
        if (_entity.IsRemoved)
        {
            retired?.Invoke();
            return;
        }

        run();
    }

    private ChunkRegion RequireRegion()
    {
        ChunkRegion? region = _regionizer.GetRegionAt(_entity.ChunkX, _entity.ChunkZ);
        if (region is null || !region.IsAlive)
        {
            throw new InvalidOperationException(
                $"No region owns entity {_entity.EntityId} at ({_entity.ChunkX},{_entity.ChunkZ}).");
        }

        return region;
    }

    private void Track(ScheduledTask task)
    {
        lock (_sync)
        {
            _tasks.Add(task);
        }
    }
}
