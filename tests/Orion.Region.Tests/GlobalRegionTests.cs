using Orion.Region;
using Xunit;

namespace Orion.Region.Tests;

public sealed class GlobalRegionTests
{
    [Fact]
    public void EnsureCurrentThread_ThrowsOutsideTick()
    {
        var region = new GlobalRegion();
        Assert.Throws<InvalidOperationException>(() => region.EnsureCurrentThread());
        Assert.Throws<InvalidOperationException>(() => region.Drain());
    }

    [Fact]
    public void RunTick_SetsCurrentThreadAndIncrementsCount()
    {
        var region = new GlobalRegion();
        var ran = false;

        region.RunTick(() =>
        {
            Assert.True(region.IsCurrentThread);
            region.EnsureCurrentThread();
            ran = true;
        });

        Assert.True(ran);
        Assert.Equal(1, region.TickCount);
        Assert.False(region.IsCurrentThread);
    }

    [Fact]
    public void RunOnGlobal_DrainsOnTick()
    {
        var region = new GlobalRegion();
        var executed = 0;
        region.RunOnGlobal(() => executed++);

        region.RunTick(() => region.Drain());

        Assert.Equal(1, executed);
        Assert.Equal(1, region.TickCount);
    }

    [Fact]
    public async Task IdleTickLoop_ApproachesConfiguredTps()
    {
        const int tps = 20;
        var region = new GlobalRegion();
        using var cts = new CancellationTokenSource();
        var interval = TickTiming.Interval(tps);

        var loop = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                var started = Environment.TickCount64;
                region.RunTick(static () => { });
                var remaining = interval.TotalMilliseconds - (Environment.TickCount64 - started);
                if (remaining > 0)
                {
                    Thread.Sleep((int)remaining);
                }
            }
        }, CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(1));
        await cts.CancelAsync();
        await loop;

        Assert.InRange(region.TickCount, 18, 22);
    }
}

public sealed class TickTimingTests
{
    [Fact]
    public void Interval_ForTwentyTps_IsFiftyMilliseconds()
    {
        Assert.Equal(50, TickTiming.IntervalMilliseconds(20));
        Assert.Equal(50_000_000, TickTiming.IntervalNanoseconds(20));
    }

    [Fact]
    public void Clamp_NonPositive_DefaultsToTwenty()
    {
        Assert.Equal(20, TickTiming.ClampTicksPerSecond(0));
        Assert.Equal(20, TickTiming.ClampTicksPerSecond(-1));
    }
}
