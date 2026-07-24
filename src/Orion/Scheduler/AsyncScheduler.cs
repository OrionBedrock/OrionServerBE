using System.Collections.Concurrent;
using Orion.Runtime;

namespace Orion.Scheduler;

/// <summary>
/// Folia AsyncScheduler: off-tick real-time work. Re-enter world via region/global schedulers.
/// </summary>
public sealed class AsyncScheduler : IDisposable
{
    private readonly OrionThreadPools _pools;
    private readonly ConcurrentDictionary<ScheduledTask, byte> _tasks = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public AsyncScheduler(OrionThreadPools pools)
    {
        _pools = pools ?? throw new ArgumentNullException(nameof(pools));
    }

    public ScheduledTask RunNow(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var task = new ScheduledTask(action, delayTicks: 0, periodTicks: 0);
        Track(task);
        _pools.QueueAsyncScheduler(() => RunTask(task));
        return task;
    }

    public ScheduledTask RunDelayed(Action action, TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        var task = new ScheduledTask(action, delayTicks: 0, periodTicks: 0);
        Track(task);
        _ = Task.Run(async () =>
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, _cts.Token).ConfigureAwait(false);
                }

                if (task.IsCancelled || _cts.IsCancellationRequested)
                {
                    return;
                }

                _pools.QueueAsyncScheduler(() => RunTask(task));
            }
            catch (OperationCanceledException)
            {
            }
        }, _cts.Token);

        return task;
    }

    public ScheduledTask RunAtFixedRate(Action action, TimeSpan initialDelay, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var task = new ScheduledTask(action, delayTicks: 0, periodTicks: 1);
        Track(task);
        _ = Task.Run(async () =>
        {
            try
            {
                if (initialDelay > TimeSpan.Zero)
                {
                    await Task.Delay(initialDelay, _cts.Token).ConfigureAwait(false);
                }

                while (!task.IsCancelled && !_cts.IsCancellationRequested)
                {
                    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _pools.QueueAsyncScheduler(() =>
                    {
                        try
                        {
                            if (!task.IsCancelled)
                            {
                                RunTaskKeepRepeating(task);
                            }
                        }
                        finally
                        {
                            gate.TrySetResult();
                        }
                    });

                    await gate.Task.ConfigureAwait(false);
                    if (task.IsCancelled || _cts.IsCancellationRequested)
                    {
                        break;
                    }

                    await Task.Delay(period, _cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, _cts.Token);

        return task;
    }

    public void CancelTasks()
    {
        foreach (ScheduledTask task in _tasks.Keys)
        {
            task.Cancel();
        }

        _tasks.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        CancelTasks();
        _cts.Dispose();
    }

    private void Track(ScheduledTask task) => _tasks[task] = 0;

    private void RunTask(ScheduledTask task)
    {
        if (task.IsCancelled)
        {
            _tasks.TryRemove(task, out _);
            return;
        }

        // One-shot: reuse TickOnce semantics with delay 0 non-repeating.
        task.TickOnce();
        _tasks.TryRemove(task, out _);
    }

    private void RunTaskKeepRepeating(ScheduledTask task)
    {
        if (task.IsCancelled)
        {
            _tasks.TryRemove(task, out _);
            return;
        }

        task.TickOnce();
        if (task.IsCancelled || task.State == ScheduledTaskState.Finished)
        {
            _tasks.TryRemove(task, out _);
        }
    }
}
