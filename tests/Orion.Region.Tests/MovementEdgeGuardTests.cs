using Orion.Config;
using Orion.Region;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.Region.Tests;

public sealed class MovementEdgeGuardTests
{
    [Fact]
    public void EdgeSkip_WhenDestinationLeavesRegion()
    {
        using EntityFixture fixture = CreateFixture(
            chunkX: 0,
            chunkZ: 0,
            mergeRadiusSections: 0,
            extraChunks: [(1, 0)]);
        EntityHandle entity = fixture.Entity;
        ChunkRegion? origin = fixture.Regionizer.GetRegionAt(0, 0);
        ChunkRegion? other = fixture.Regionizer.GetRegionAt(1, 0);
        Assert.NotNull(origin);
        Assert.NotNull(other);
        Assert.NotSame(origin, other);

        double beforeX = entity.X;
        double beforeZ = entity.Z;
        int beforeCx = entity.ChunkX;

        using (origin.TryMarkTickingWithOwnership())
        {
            bool moved = entity.TryMove(fixture.Regionizer, 1 * 16 + 0.5, 64, 0.5);
            Assert.False(moved);
        }

        Assert.Equal(beforeX, entity.X);
        Assert.Equal(beforeZ, entity.Z);
        Assert.Equal(beforeCx, entity.ChunkX);
        Assert.Equal(0, entity.ChunkZ);
    }

    [Fact]
    public void MergeRecovery_AllowsMoveAfterMerge()
    {
        using EntityFixture fixture = CreateFixture(
            chunkX: 0,
            chunkZ: 0,
            mergeRadiusSections: 1,
            extraChunks: []);
        EntityHandle entity = fixture.Entity;
        Regionizer regionizer = fixture.Regionizer;
        ChunkRegion a = regionizer.GetRegionAt(0, 0)!;

        using (a.TryMarkTickingWithOwnership())
        {
            ChunkRegion b = regionizer.AddChunk(1, 0);
            Assert.NotSame(a, b);
            Assert.Same(a, b.MergeIntoLater);

            bool skipped = entity.TryMove(regionizer, 1 * 16 + 0.5, 64, 0.5);
            Assert.False(skipped);
            Assert.Equal(0, entity.ChunkX);
        }

        // Ownership dispose leaves region Ticking; regionizer applies deferred merge here.
        regionizer.MarkNotTicking(a);

        Assert.Equal(1, regionizer.RegionCount);
        ChunkRegion? merged = regionizer.GetRegionAt(1, 0);
        Assert.NotNull(merged);
        Assert.Same(regionizer.GetRegionAt(0, 0), merged);

        using (merged.TryMarkTickingWithOwnership())
        {
            bool moved = entity.TryMove(regionizer, 1 * 16 + 0.5, 70, 0.5);
            Assert.True(moved);
            Assert.Equal(1, entity.ChunkX);
            Assert.Equal(1 * 16 + 0.5, entity.X);
            Assert.Equal(70, entity.Y);
        }
    }

    [Fact]
    public void SameRegion_MoveSucceeds()
    {
        using EntityFixture fixture = CreateFixture(
            chunkX: 0,
            chunkZ: 0,
            mergeRadiusSections: 1,
            extraChunks: [(1, 0)]);
        EntityHandle entity = fixture.Entity;
        ChunkRegion? region = fixture.Regionizer.GetRegionAt(0, 0);
        Assert.NotNull(region);
        Assert.Same(region, fixture.Regionizer.GetRegionAt(1, 0));

        using (region.TryMarkTickingWithOwnership())
        {
            Assert.True(entity.TryMove(fixture.Regionizer, 4.5, 65, 8.5));
            Assert.Equal(4.5, entity.X);
            Assert.Equal(65, entity.Y);
            Assert.Equal(8.5, entity.Z);
            Assert.Equal(0, entity.ChunkX);

            Assert.True(entity.TryMove(fixture.Regionizer, 1 * 16 + 0.5, 66, 0.5));
            Assert.Equal(1, entity.ChunkX);
            Assert.Equal(1 * 16 + 0.5, entity.X);
        }
    }

    private static EntityFixture CreateFixture(
        int chunkX,
        int chunkZ,
        int mergeRadiusSections,
        (int X, int Z)[] extraChunks)
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections));
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
        return new EntityFixture(world, provider, regionizer, entity);
    }

    private sealed class EntityFixture : IDisposable
    {
        private readonly InMemoryWorldProvider _provider;

        public EntityFixture(
            Orion.World.World world,
            InMemoryWorldProvider provider,
            Regionizer regionizer,
            EntityHandle entity)
        {
            World = world;
            _provider = provider;
            Regionizer = regionizer;
            Entity = entity;
        }

        public Orion.World.World World { get; }

        public Regionizer Regionizer { get; }

        public EntityHandle Entity { get; }

        public void Dispose()
        {
            World.Dispose();
            _provider.Dispose();
        }
    }
}
