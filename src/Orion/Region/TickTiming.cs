namespace Orion.Region;

/// <summary>
/// Tick interval helpers derived from configured TPS.
/// </summary>
public static class TickTiming
{
    public static int ClampTicksPerSecond(int ticksPerSecond)
        => ticksPerSecond <= 0 ? 20 : ticksPerSecond;

    public static TimeSpan Interval(int ticksPerSecond)
    {
        int tps = ClampTicksPerSecond(ticksPerSecond);
        return TimeSpan.FromSeconds(1.0 / tps);
    }

    public static long IntervalMilliseconds(int ticksPerSecond)
        => (long)Interval(ticksPerSecond).TotalMilliseconds;

    public static long IntervalNanoseconds(int ticksPerSecond)
        => (long)(1_000_000_000.0 / ClampTicksPerSecond(ticksPerSecond));
}
