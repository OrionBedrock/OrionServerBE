using Orion.Config;
using Orion.Region;
using Orion.Runtime;
using Orion.Scheduler;
using Xunit;

namespace Orion.Scheduler.Tests;

public sealed class AsyncSchedulerSmokeTests
{
    [Fact]
    public async Task RunDelayed_ExecutesOnAsyncPool()
    {
        var budget = ThreadPoolBudget.Resolve(new ThreadPoolsConfig
        {
            RegionTick = new ThreadLimitConfig { Max = 1 },
            Raknet = new ThreadLimitConfig { Max = 1 },
            ChunkIo = new ThreadLimitConfig { Max = 1 },
            ChunkWorkers = new ThreadLimitConfig { Max = 1 },
            IoPersistence = new ThreadLimitConfig { Max = 1 },
            AsyncScheduler = new ThreadLimitConfig { Max = 1 },
        }, processorCount: 4);

        using var pools = new OrionThreadPools(budget);
        using var asyncScheduler = new AsyncScheduler(pools);
        var ran = 0;

        ScheduledTask task = asyncScheduler.RunDelayed(() => Interlocked.Increment(ref ran), TimeSpan.FromMilliseconds(50));

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (Volatile.Read(ref ran) == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, ran);
        Assert.Equal(ScheduledTaskState.Finished, task.State);
    }

    [Fact]
    public async Task AsyncCallback_ReentersGlobalScheduler()
    {
        var budget = ThreadPoolBudget.Resolve(new ThreadPoolsConfig
        {
            RegionTick = new ThreadLimitConfig { Max = 1 },
            Raknet = new ThreadLimitConfig { Max = 1 },
            ChunkIo = new ThreadLimitConfig { Max = 1 },
            ChunkWorkers = new ThreadLimitConfig { Max = 1 },
            IoPersistence = new ThreadLimitConfig { Max = 1 },
            AsyncScheduler = new ThreadLimitConfig { Max = 1 },
        }, processorCount: 4);

        using var pools = new OrionThreadPools(budget);
        using var asyncScheduler = new AsyncScheduler(pools);
        var global = new GlobalRegion();
        var globalScheduler = new GlobalRegionScheduler(global);
        var reentered = 0;

        asyncScheduler.RunNow(() =>
        {
            globalScheduler.Run(() => Interlocked.Increment(ref reentered));
        });

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            global.RunTick(() =>
            {
                globalScheduler.Tick();
                global.Drain();
            });

            if (Volatile.Read(ref reentered) > 0)
            {
                break;
            }

            await Task.Delay(20);
        }

        Assert.Equal(1, Volatile.Read(ref reentered));
    }

    [Fact]
    public void UnsupportedSync_Throws()
    {
        Assert.Throws<NotSupportedException>(() => UnsupportedSyncScheduler.RunTask(static () => { }));
        Assert.Throws<NotSupportedException>(() => UnsupportedSyncScheduler.RunTaskLater(static () => { }, 1));
        Assert.Throws<NotSupportedException>(() => UnsupportedSyncScheduler.RunTaskTimer(static () => { }, 1, 1));
    }
}
