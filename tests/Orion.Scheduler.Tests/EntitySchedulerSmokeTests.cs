using Orion.Region;
using Orion.Scheduler;
using Xunit;

namespace Orion.Scheduler.Tests;

public sealed class EntitySchedulerSmokeTests
{
    [Fact]
    public void Retire_MidDelay_InvokesRetiredNotRun()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(0, 0);
        var entity = new SchedulableEntityStub(1, 0, 0);
        var scheduler = new EntityScheduler(regionizer, entity);
        var ran = 0;
        var retired = 0;

        ScheduledTask? task = scheduler.RunDelayed(() => ran++, delayTicks: 2, retired: () => retired++);
        Assert.NotNull(task);

        scheduler.Retire();
        Assert.Equal(1, retired);
        Assert.Equal(0, ran);

        using (region.TryMarkTickingWithOwnership())
        {
            region.DrainSchedulerTasks();
            region.DrainSchedulerTasks();
        }

        Assert.Equal(0, ran);
        Assert.True(task!.IsCancelled);
    }

    [Fact]
    public void Run_WhenAlreadyRemoved_ReturnsNullAndRetires()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        regionizer.AddChunk(1, 1);
        var entity = new SchedulableEntityStub(2, 1, 1);
        entity.Remove();
        var scheduler = new EntityScheduler(regionizer, entity);
        var retired = 0;

        ScheduledTask? task = scheduler.Run(() => { }, retired: () => retired++);
        Assert.Null(task);
        Assert.Equal(1, retired);
    }

    [Fact]
    public void FixedRate_StopsAfterRetire()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(3, 3);
        var entity = new SchedulableEntityStub(3, 3, 3);
        var scheduler = new EntityScheduler(regionizer, entity);
        var ran = 0;

        ScheduledTask? task = scheduler.RunAtFixedRate(() => ran++, 0, 1);
        Assert.NotNull(task);

        using (region.TryMarkTickingWithOwnership())
        {
            region.DrainSchedulerTasks();
            Assert.Equal(1, ran);
            scheduler.Retire();
            region.DrainSchedulerTasks();
        }

        Assert.Equal(1, ran);
    }
}
