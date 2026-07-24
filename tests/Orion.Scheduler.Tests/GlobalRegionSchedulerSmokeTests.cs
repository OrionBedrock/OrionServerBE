using Orion.Region;
using Orion.Scheduler;
using Xunit;

namespace Orion.Scheduler.Tests;

public sealed class GlobalRegionSchedulerSmokeTests
{
    [Fact]
    public void Run_NextTick_OnGlobalThread()
    {
        var global = new GlobalRegion();
        var scheduler = new GlobalRegionScheduler(global);
        var ran = 0;
        var onGlobal = false;

        ScheduledTask task = scheduler.Run(() =>
        {
            ran++;
            onGlobal = global.IsCurrentThread;
            global.EnsureCurrentThread();
        });

        global.RunTick(() =>
        {
            scheduler.Tick();
            global.Drain();
        });

        Assert.Equal(1, ran);
        Assert.True(onGlobal);
        Assert.Equal(ScheduledTaskState.Finished, task.State);
    }

    [Fact]
    public void CancelTasks_PreventsRun()
    {
        var global = new GlobalRegion();
        var scheduler = new GlobalRegionScheduler(global);
        var ran = 0;

        scheduler.RunDelayed(() => ran++, delayTicks: 0);
        scheduler.CancelTasks();

        global.RunTick(() =>
        {
            scheduler.Tick();
            global.Drain();
        });

        Assert.Equal(0, ran);
    }

    [Fact]
    public void RunDelayed_RespectsTicks()
    {
        var global = new GlobalRegion();
        var scheduler = new GlobalRegionScheduler(global);
        var ran = 0;

        scheduler.RunDelayed(() => ran++, delayTicks: 1);

        global.RunTick(() => scheduler.Tick());
        Assert.Equal(0, ran);

        global.RunTick(() => scheduler.Tick());
        Assert.Equal(1, ran);
    }
}
