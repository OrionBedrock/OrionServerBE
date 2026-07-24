using Orion.Region;
using Orion.Scheduler;
using Xunit;

namespace Orion.Scheduler.Tests;

public sealed class RegionSchedulerSmokeTests
{
    [Fact]
    public void RunDelayed_ExecutesAfterDrain()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(0, 0);
        var scheduler = new RegionScheduler(regionizer);
        var ran = 0;

        ScheduledTask task = scheduler.RunDelayed("default", 0, 0, () => ran++, delayTicks: 1);
        Assert.Equal(ScheduledTaskState.Idle, task.State);

        using (region.TryMarkTickingWithOwnership())
        {
            region.DrainSchedulerTasks(); // delay 1 -> 0
            Assert.Equal(0, ran);
            region.DrainSchedulerTasks(); // run
            Assert.Equal(1, ran);
        }

        Assert.Equal(ScheduledTaskState.Finished, task.State);
    }

    [Fact]
    public void Cancel_PreventsExecution()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(1, 1);
        var scheduler = new RegionScheduler(regionizer);
        var ran = 0;

        ScheduledTask task = scheduler.Run("default", 1, 1, () => ran++);
        task.Cancel();

        using (region.TryMarkTickingWithOwnership())
        {
            region.DrainSchedulerTasks();
        }

        Assert.Equal(0, ran);
        Assert.True(task.IsCancelled);
    }

    [Fact]
    public void RunAtFixedRate_RepeatsUntilCancelled()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(2, 2);
        var scheduler = new RegionScheduler(regionizer);
        var ran = 0;

        ScheduledTask task = scheduler.RunAtFixedRate("default", 2, 2, () => ran++, 0, periodTicks: 1);

        using (region.TryMarkTickingWithOwnership())
        {
            region.DrainSchedulerTasks();
            region.DrainSchedulerTasks();
            Assert.Equal(2, ran);
            task.Cancel();
            region.DrainSchedulerTasks();
        }

        Assert.Equal(2, ran);
    }

    [Fact]
    public void Schedule_WithoutRegion_Throws()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        var scheduler = new RegionScheduler(regionizer);
        Assert.Throws<InvalidOperationException>(() => scheduler.Run("default", 0, 0, static () => { }));
    }
}
