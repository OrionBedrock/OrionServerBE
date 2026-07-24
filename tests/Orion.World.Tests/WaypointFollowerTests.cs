using Orion.Config;
using Orion.Pathfinding;
using Orion.Region;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class WaypointFollowerTests
{
    [Fact]
    public void SameRegion_AdvancesTowardWaypoints()
    {
        using EntityFixture fixture = CreateFixture(chunkX: 0, chunkZ: 0, mergeRadius: 1, extra: [(1, 0)]);
        EntityHandle entity = fixture.Entity;
        // Start near origin; waypoints stay in same merged region.
        entity.TryMove(fixture.Regionizer, 0.5, 64, 0.5); // may fail without ownership

        var follower = new WaypointFollower(
        [
            new PathPoint(4.0, 64, 0.5),
            new PathPoint(8.0, 64, 0.5),
        ]);

        using (fixture.Region.TryMarkTickingWithOwnership())
        {
            Assert.True(entity.TryMove(fixture.Regionizer, 0.5, 64, 0.5));
            for (int i = 0; i < 40 && !follower.IsCompleted; i++)
            {
                follower.Tick(entity, fixture.Regionizer, stepDistance: 2.0);
            }
        }

        Assert.True(follower.IsCompleted);
        Assert.True(entity.X >= 7.5);
    }

    [Fact]
    public void ForeignRegion_PausesWithoutAdvancing()
    {
        using EntityFixture fixture = CreateFixture(chunkX: 0, chunkZ: 0, mergeRadius: 0, extra: [(1, 0)]);
        EntityHandle entity = fixture.Entity;
        var follower = new WaypointFollower([new PathPoint(1 * 16 + 0.5, 64, 0.5)]);

        using (fixture.Region.TryMarkTickingWithOwnership())
        {
            // Stand just inside chunk 0 near the border; next step lands in foreign chunk 1.
            Assert.True(entity.TryMove(fixture.Regionizer, 15.5, 64, 0.5));
            double beforeX = entity.X;
            int beforeIndex = follower.Index;

            bool progressed = follower.Tick(entity, fixture.Regionizer, stepDistance: 4.0);
            Assert.False(progressed);
            Assert.Equal(beforeIndex, follower.Index);
            Assert.Equal(beforeX, entity.X);
        }

        Assert.False(follower.IsCompleted);
    }

    private static EntityFixture CreateFixture(int chunkX, int chunkZ, int mergeRadius, (int X, int Z)[] extra)
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadius));
        regionizer.AddChunk(chunkX, chunkZ);
        foreach ((int x, int z) in extra)
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
        ChunkRegion region = regionizer.GetRegionAt(chunkX, chunkZ)!;
        return new EntityFixture(world, provider, regionizer, region, entity);
    }

    private sealed class EntityFixture : IDisposable
    {
        private readonly InMemoryWorldProvider _provider;

        public EntityFixture(
            Orion.World.World world,
            InMemoryWorldProvider provider,
            Regionizer regionizer,
            ChunkRegion region,
            EntityHandle entity)
        {
            World = world;
            _provider = provider;
            Regionizer = regionizer;
            Region = region;
            Entity = entity;
        }

        public Orion.World.World World { get; }
        public Regionizer Regionizer { get; }
        public ChunkRegion Region { get; }
        public EntityHandle Entity { get; }

        public void Dispose()
        {
            World.Dispose();
            _provider.Dispose();
        }
    }
}
