using System.Collections.Concurrent;

namespace Orion.Scheduler;

/// <summary>
/// Tick-based task mailbox drained on the owning region/global tick thread.
/// </summary>
public sealed class SchedulerTaskQueue
{
    private readonly ConcurrentQueue<ScheduledTask> _tasks = new();

    public int Count => _tasks.Count;

    public ScheduledTask Enqueue(Action action, long delayTicks = 0, long periodTicks = 0, Action? retired = null)
    {
        var task = new ScheduledTask(action, delayTicks, periodTicks, retired);
        _tasks.Enqueue(task);
        return task;
    }

    public void DrainOneTick()
    {
        int count = _tasks.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_tasks.TryDequeue(out ScheduledTask? task))
            {
                break;
            }

            if (task.TickOnce())
            {
                _tasks.Enqueue(task);
            }
        }
    }

    public void CancelAll()
    {
        int count = _tasks.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_tasks.TryDequeue(out ScheduledTask? task))
            {
                break;
            }

            task.Cancel();
        }
    }

    public void RetireAll()
    {
        int count = _tasks.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_tasks.TryDequeue(out ScheduledTask? task))
            {
                break;
            }

            task.TryRetire();
        }
    }
}
