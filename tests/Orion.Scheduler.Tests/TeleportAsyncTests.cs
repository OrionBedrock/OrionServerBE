using Orion.Config;
using Orion.Region;
using Orion.Scheduler;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.Scheduler.Tests;

public sealed class TeleportAsyncTests
{
    [Fact]
    public async Task SameRegion_UpdatesPosition()
    {
        using EntityFixture fixture = CreateFixture(chunkX: 0, chunkZ: 0, extraChunks: []);
        EntityHandle entity = fixture.Entity;
        ChunkRegion? region = fixture.Regionizer.GetRegionAt(0, 0);
        Assert.NotNull(region);

        Task<bool> teleport = entity.TeleportAsync(fixture.Regions, 4.5, 70, 8.5).AsTask();
        await DrainUntilComplete(fixture.Regionizer, teleport);

        Assert.True(await teleport);
        Assert.Equal(4.5, entity.X);
        Assert.Equal(70, entity.Y);
        Assert.Equal(8.5, entity.Z);
        Assert.Equal(0, entity.ChunkX);
        Assert.Equal(0, entity.ChunkZ);
        Assert.False(entity.IsTeleporting);
        Assert.Same(region, fixture.Regionizer.GetRegionAt(0, 0));
    }

    [Fact]
    public async Task CrossRegion_PlacesOnDestinationOnly()
    {
        using EntityFixture fixture = CreateFixture(chunkX: 0, chunkZ: 0, extraChunks: [(10, 10)]);
        EntityHandle entity = fixture.Entity;
        ChunkRegion? origin = fixture.Regionizer.GetRegionAt(0, 0);
        ChunkRegion? dest = fixture.Regionizer.GetRegionAt(10, 10);
        Assert.NotNull(origin);
        Assert.NotNull(dest);
        Assert.NotSame(origin, dest);

        double x = 10 * 16 + 0.5;
        double z = 10 * 16 + 0.5;
        Task<bool> teleport = entity.TeleportAsync(fixture.Regions, x, 80, z).AsTask();
        await DrainUntilComplete(fixture.Regionizer, teleport);

        Assert.True(await teleport);
        Assert.Equal(10, entity.ChunkX);
        Assert.Equal(10, entity.ChunkZ);
        Assert.Equal(x, entity.X);
        Assert.Equal(z, entity.Z);
        Assert.Same(dest, fixture.Regionizer.GetRegionAt(entity.ChunkX, entity.ChunkZ));
        Assert.False(entity.IsTeleporting);
    }

    [Fact]
    public async Task RemoveMidFlight_ReturnsFalseAndRetires()
    {
        using EntityFixture fixture = CreateFixture(chunkX: 0, chunkZ: 0, extraChunks: [(20, 20)]);
        EntityHandle entity = fixture.Entity;

        var retired = 0;
        Task<bool> teleport = entity.TeleportAsync(
            fixture.Regions,
            20 * 16 + 0.5,
            64,
            20 * 16 + 0.5,
            () => Interlocked.Increment(ref retired)).AsTask();

        DrainRegionAt(fixture.Regionizer, 0, 0);
        Assert.True(entity.IsTeleporting);
        entity.Remove();

        await DrainUntilComplete(fixture.Regionizer, teleport);
        Assert.False(await teleport);
        Assert.Equal(1, retired);
        Assert.True(entity.IsRemoved);
        Assert.False(entity.IsTeleporting);
    }

    [Fact]
    public async Task DestinationWithoutRegion_FailsClean()
    {
        using EntityFixture fixture = CreateFixture(chunkX: 0, chunkZ: 0, extraChunks: []);
        EntityHandle entity = fixture.Entity;

        Task<bool> teleport = entity.TeleportAsync(fixture.Regions, 50 * 16 + 0.5, 64, 50 * 16 + 0.5).AsTask();

        Exception? fault = null;
        try
        {
            await DrainUntilComplete(fixture.Regionizer, teleport);
            await teleport;
        }
        catch (Exception ex)
        {
            fault = ex;
        }

        Assert.NotNull(fault);
        Assert.Contains("No region owns chunk", fault.Message, StringComparison.Ordinal);
        Assert.False(entity.IsTeleporting);
        Assert.False(entity.IsRemoved);
    }

    private static async Task DrainUntilComplete(Regionizer regionizer, Task task, int maxSpins = 200)
    {
        for (int i = 0; i < maxSpins && !task.IsCompleted; i++)
        {
            foreach (ChunkRegion region in regionizer.SnapshotRegions())
            {
                if (!region.IsAlive)
                {
                    continue;
                }

                using IDisposable? ownership = region.TryMarkTickingWithOwnership();
                if (ownership is null)
                {
                    continue;
                }

                try
                {
                    region.DrainSchedulerTasks();
                }
                finally
                {
                    region.MarkNotTicking();
                }
            }

            await Task.Yield();
        }

        Assert.True(task.IsCompleted, "Teleport task did not complete after draining regions.");
    }

    private static void DrainRegionAt(Regionizer regionizer, int chunkX, int chunkZ)
    {
        ChunkRegion? region = regionizer.GetRegionAt(chunkX, chunkZ);
        Assert.NotNull(region);
        using IDisposable? ownership = region.TryMarkTickingWithOwnership();
        Assert.NotNull(ownership);
        try
        {
            region.DrainSchedulerTasks();
        }
        finally
        {
            region.MarkNotTicking();
        }
    }

    private static EntityFixture CreateFixture(int chunkX, int chunkZ, (int X, int Z)[] extraChunks)
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        regionizer.AddChunk(chunkX, chunkZ);
        foreach ((int x, int z) in extraChunks)
        {
            regionizer.AddChunk(x, z);
        }

        var config = new OrionConfig();
        config.Server.WorldDefaultSettings.Dimensions.Add(new DimensionConfig
        {
            Identifier = "overworld",
            SpawnPosition = [chunkX * 16, 64, chunkZ * 16],
        });
        var provider = new InMemoryWorldProvider();
        var world = Orion.World.World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        var entity = new EntityHandle(1, world.GetDimension("overworld"), chunkX, chunkZ, world.Identifier);
        return new EntityFixture(world, provider, regionizer, new RegionScheduler(regionizer), entity);
    }

    private sealed class EntityFixture : IDisposable
    {
        private readonly InMemoryWorldProvider _provider;

        public EntityFixture(
            Orion.World.World world,
            InMemoryWorldProvider provider,
            Regionizer regionizer,
            RegionScheduler regions,
            EntityHandle entity)
        {
            World = world;
            _provider = provider;
            Regionizer = regionizer;
            Regions = regions;
            Entity = entity;
        }

        public Orion.World.World World { get; }

        public Regionizer Regionizer { get; }

        public RegionScheduler Regions { get; }

        public EntityHandle Entity { get; }

        public void Dispose()
        {
            World.Dispose();
            _provider.Dispose();
        }
    }
}
