namespace Orion.Scheduler;

public enum ScheduledTaskState
{
    Idle = 0,
    Running = 1,
    Finished = 2,
    Cancelled = 3,
}

/// <summary>
/// Folia-style scheduled task handle. Cancel is thread-safe.
/// </summary>
public sealed class ScheduledTask
{
    private readonly object _sync = new();
    private ScheduledTaskState _state = ScheduledTaskState.Idle;
    private Action? _action;
    private Action? _retired;
    private long _remainingDelayTicks;
    private long _periodTicks;
    private bool _repeating;

    internal ScheduledTask(Action action, long delayTicks, long periodTicks, Action? retired = null)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _retired = retired;
        _remainingDelayTicks = Math.Max(0, delayTicks);
        _periodTicks = Math.Max(0, periodTicks);
        _repeating = periodTicks > 0;
    }

    public ScheduledTaskState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public bool IsRepeatingTask
    {
        get
        {
            lock (_sync)
            {
                return _repeating;
            }
        }
    }

    public bool IsCancelled => State == ScheduledTaskState.Cancelled;

    public void Cancel()
    {
        lock (_sync)
        {
            if (_state is ScheduledTaskState.Finished or ScheduledTaskState.Cancelled)
            {
                return;
            }

            _state = ScheduledTaskState.Cancelled;
            _action = null;
            _retired = null;
        }
    }

    internal bool TryRetire()
    {
        Action? retired;
        lock (_sync)
        {
            if (_state is ScheduledTaskState.Finished or ScheduledTaskState.Cancelled)
            {
                return false;
            }

            retired = _retired;
            _state = ScheduledTaskState.Cancelled;
            _action = null;
            _retired = null;
        }

        retired?.Invoke();
        return retired is not null;
    }

    /// <summary>
    /// Advances one tick. Returns true if the task should remain queued.
    /// </summary>
    internal bool TickOnce()
    {
        Action? toRun;
        lock (_sync)
        {
            if (_state == ScheduledTaskState.Cancelled)
            {
                return false;
            }

            if (_remainingDelayTicks > 0)
            {
                _remainingDelayTicks--;
                return true;
            }

            toRun = _action;
            if (toRun is null)
            {
                _state = ScheduledTaskState.Finished;
                return false;
            }

            _state = ScheduledTaskState.Running;
        }

        try
        {
            toRun();
        }
        catch
        {
            lock (_sync)
            {
                if (_state != ScheduledTaskState.Cancelled)
                {
                    _state = ScheduledTaskState.Finished;
                    _action = null;
                }
            }

            throw;
        }

        lock (_sync)
        {
            if (_state == ScheduledTaskState.Cancelled)
            {
                return false;
            }

            if (_repeating && _periodTicks > 0 && _action is not null)
            {
                _remainingDelayTicks = Math.Max(0, _periodTicks - 1);
                _state = ScheduledTaskState.Idle;
                return true;
            }

            _state = ScheduledTaskState.Finished;
            _action = null;
            return false;
        }
    }
}
