using System.Diagnostics;
using Orion.Region;

namespace Orion.Runtime;

/// <summary>
/// Schedules the global region tick on the RegionTick pool.
/// Stores Runtime.Regions.Scheduler policy (EDF); pacing uses deadline sleep (no work-stealing yet).
/// </summary>
public sealed class RegionTickScheduler : IDisposable
{
    private readonly OrionThreadPools _pools;
    private readonly string _schedulerPolicy;
    private CancellationTokenSource? _cts;
    private volatile bool _started;
    private bool _disposed;

    public RegionTickScheduler(OrionThreadPools pools, string schedulerPolicy)
    {
        _pools = pools ?? throw new ArgumentNullException(nameof(pools));
        _schedulerPolicy = string.IsNullOrWhiteSpace(schedulerPolicy) ? "EDF" : schedulerPolicy;
    }

    public string SchedulerPolicy => _schedulerPolicy;

    public bool IsRunning => _started && _cts is { IsCancellationRequested: false };

    public void Start(GlobalRegion region, Action tickBody, int ticksPerSecond, CancellationToken externalToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            throw new InvalidOperationException("Region tick scheduler already started.");
        }

        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(tickBody);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _started = true;
        var token = _cts.Token;
        long intervalNs = TickTiming.IntervalNanoseconds(ticksPerSecond);

        _pools.QueueRegionTick(() => RunLoop(region, tickBody, intervalNs, token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _started = false;
    }

    private static void RunLoop(GlobalRegion region, Action tickBody, long intervalNs, CancellationToken token)
    {
        long nextDeadline = Stopwatch.GetTimestamp();
        double ticksPerNanosecond = Stopwatch.Frequency / 1_000_000_000.0;

        while (!token.IsCancellationRequested)
        {
            long started = Stopwatch.GetTimestamp();
            try
            {
                region.RunTick(tickBody);
            }
            catch
            {
                // Keep ticking; detailed logging arrives later.
            }

            nextDeadline += (long)(intervalNs * ticksPerNanosecond);
            long now = Stopwatch.GetTimestamp();
            long remaining = nextDeadline - now;
            if (remaining > 0)
            {
                double remainingMs = remaining * 1000.0 / Stopwatch.Frequency;
                if (remainingMs >= 1)
                {
                    Thread.Sleep((int)remainingMs);
                }
                else
                {
                    Thread.SpinWait(50);
                }
            }
            else
            {
                // Behind schedule: resync deadline so one slow tick does not cascade forever.
                nextDeadline = Stopwatch.GetTimestamp();
                _ = started;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _cts?.Dispose();
        _cts = null;
    }
}
