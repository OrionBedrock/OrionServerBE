using Orion.Config;
using Orion.Region;
using Orion.Runtime;
using Xunit;

namespace Orion.Region.Tests;

public sealed class ThreadPoolBudgetTests
{
    [Fact]
    public void Resolve_AutoRegionTick_LeavesHeadroom()
    {
        var config = new ThreadPoolsConfig
        {
            RegionTick = new ThreadLimitConfig { Max = 0 },
            Raknet = new ThreadLimitConfig { Max = 2 },
            ChunkIo = new ThreadLimitConfig { Max = 2 },
            ChunkWorkers = new ThreadLimitConfig { Max = 2 },
            IoPersistence = new ThreadLimitConfig { Max = 2 },
            AsyncScheduler = new ThreadLimitConfig { Max = 2 },
        };

        var budget = ThreadPoolBudget.Resolve(config, processorCount: 16);

        Assert.Equal(16, budget.ProcessorCount);
        Assert.Equal(4, budget.ReservedHeadroom); // ceil(16 * 0.2)
        Assert.Equal(2, budget.Raknet);
        // 16 - (2+2+2+2+2 + 4) = 2
        Assert.Equal(2, budget.RegionTick);
    }

    [Fact]
    public void Resolve_FixedRegionTick_UsesConfiguredValue()
    {
        var config = new ThreadPoolsConfig
        {
            RegionTick = new ThreadLimitConfig { Max = 4 },
            Raknet = new ThreadLimitConfig { Max = 1 },
            ChunkIo = new ThreadLimitConfig { Max = 1 },
            ChunkWorkers = new ThreadLimitConfig { Max = 1 },
            IoPersistence = new ThreadLimitConfig { Max = 1 },
            AsyncScheduler = new ThreadLimitConfig { Max = 1 },
        };

        var budget = ThreadPoolBudget.Resolve(config, processorCount: 8);
        Assert.Equal(4, budget.RegionTick);
    }

    [Fact]
    public void Resolve_AutoOnSmallMachine_StillGivesOneTickThread()
    {
        var config = new ThreadPoolsConfig
        {
            RegionTick = new ThreadLimitConfig { Max = 0 },
            Raknet = new ThreadLimitConfig { Max = 2 },
            ChunkIo = new ThreadLimitConfig { Max = 2 },
            ChunkWorkers = new ThreadLimitConfig { Max = 2 },
            IoPersistence = new ThreadLimitConfig { Max = 2 },
            AsyncScheduler = new ThreadLimitConfig { Max = 2 },
        };

        var budget = ThreadPoolBudget.Resolve(config, processorCount: 4);
        Assert.Equal(1, budget.RegionTick);
    }
}

public sealed class RegionTickSchedulerTests
{
    [Fact]
    public async Task Scheduler_OnRegionTickPool_ApproachesTwentyTps()
    {
        var budget = ThreadPoolBudget.Resolve(new ThreadPoolsConfig
        {
            RegionTick = new ThreadLimitConfig { Max = 1 },
            Raknet = new ThreadLimitConfig { Max = 1 },
            ChunkIo = new ThreadLimitConfig { Max = 1 },
            ChunkWorkers = new ThreadLimitConfig { Max = 1 },
            IoPersistence = new ThreadLimitConfig { Max = 1 },
            AsyncScheduler = new ThreadLimitConfig { Max = 1 },
        }, processorCount: 8);

        using var pools = new OrionThreadPools(budget);
        using var scheduler = new RegionTickScheduler(pools, "EDF");
        var region = new GlobalRegion();
        using var cts = new CancellationTokenSource();

        scheduler.Start(region, static () => { }, ticksPerSecond: 20, cts.Token);
        Assert.Equal("EDF", scheduler.SchedulerPolicy);

        await Task.Delay(TimeSpan.FromSeconds(1));
        await cts.CancelAsync();
        scheduler.Stop();

        // Allow the worker to observe cancellation.
        await Task.Delay(100);
        Assert.InRange(region.TickCount, 18, 22);
    }
}
