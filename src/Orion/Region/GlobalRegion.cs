using System.Collections.Concurrent;

namespace Orion.Region;

/// <summary>
/// Folia-style global region: owns cross-cutting work not bound to a chunk region.
/// Precursor to GlobalRegionScheduler (Phase 06); not a plugin-facing API yet.
/// </summary>
public sealed class GlobalRegion
{
    private readonly ConcurrentQueue<Action> _queue = new();
    private int _tickThreadId = -1;

    public long TickCount { get; private set; }

    public bool IsCurrentThread
        => _tickThreadId >= 0 && Environment.CurrentManagedThreadId == _tickThreadId;

    public void EnsureCurrentThread()
    {
        if (!IsCurrentThread)
        {
            throw new InvalidOperationException("Operation requires the global region tick thread.");
        }
    }

    /// <summary>
    /// Enqueue work to run on the next global drain (any thread may call).
    /// </summary>
    public void RunOnGlobal(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        _queue.Enqueue(work);
    }

    public void Drain()
    {
        EnsureCurrentThread();
        while (_queue.TryDequeue(out Action? work))
        {
            work();
        }
    }

    /// <summary>
    /// Marks this thread as the global tick thread for the duration of <paramref name="tickBody"/>.
    /// </summary>
    public void RunTick(Action tickBody)
    {
        ArgumentNullException.ThrowIfNull(tickBody);
        int previous = Interlocked.Exchange(ref _tickThreadId, Environment.CurrentManagedThreadId);
        try
        {
            tickBody();
            TickCount++;
        }
        finally
        {
            Interlocked.Exchange(ref _tickThreadId, previous);
        }
    }
}
