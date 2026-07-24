using Orion.Config;
using Orion.Pathfinding;
using Orion.Region;
using Orion.World.Provider;
using Orion.World.Tickets;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class ShortAStarTests
{
    [Fact]
    public void FindsPath_OnLoadedOwnedChunks()
    {
        using PathFixture fixture = CreateFixture(mergeRadius: 1, loadChunks: [(0, 0), (1, 0)]);
        var probe = new LoadedOwnedWalkabilityProbe(fixture.Dimension, fixture.Regionizer);

        using (fixture.Region.TryMarkTickingWithOwnership())
        {
            bool ok = ShortAStar.TryFind(
                fixture.Dimension.Identifier,
                new PathPoint(0.5, 64, 0.5),
                new PathPoint(20.5, 64, 0.5),
                probe,
                out IReadOnlyList<PathPoint> path);

            Assert.True(ok);
            Assert.NotEmpty(path);
            Assert.True(path[^1].X >= 20.0);

            var follower = new WaypointFollower(path);
            for (int i = 0; i < 80 && !follower.IsCompleted; i++)
            {
                follower.Tick(fixture.Entity, fixture.Regionizer, stepDistance: 2.0);
            }

            Assert.True(follower.IsCompleted);
        }
    }

    [Fact]
    public void Fails_WhenNeighborChunkUnloaded()
    {
        using PathFixture fixture = CreateFixture(mergeRadius: 0, loadChunks: [(0, 0)]);
        // Chunk (1,0) is in regionizer for ownership edge but NOT ticket-loaded.
        fixture.Regionizer.AddChunk(1, 0);
        var probe = new LoadedOwnedWalkabilityProbe(fixture.Dimension, fixture.Regionizer);

        using (fixture.Region.TryMarkTickingWithOwnership())
        {
            // Goal in unloaded chunk — start walkable, goal not loaded.
            bool ok = ShortAStar.TryFind(
                fixture.Dimension.Identifier,
                new PathPoint(0.5, 64, 0.5),
                new PathPoint(20.5, 64, 0.5),
                probe,
                out IReadOnlyList<PathPoint> path);

            Assert.False(ok);
            Assert.Empty(path);
        }
    }

    [Fact]
    public void Fails_WhenNeighborChunkForeignOwned()
    {
        using PathFixture fixture = CreateFixture(mergeRadius: 0, loadChunks: [(0, 0), (1, 0)]);
        var probe = new LoadedOwnedWalkabilityProbe(fixture.Dimension, fixture.Regionizer);
        ChunkRegion? origin = fixture.Regionizer.GetRegionAt(0, 0);
        Assert.NotNull(origin);
        Assert.NotSame(origin, fixture.Regionizer.GetRegionAt(1, 0));

        using (origin.TryMarkTickingWithOwnership())
        {
            bool ok = ShortAStar.TryFind(
                fixture.Dimension.Identifier,
                new PathPoint(0.5, 64, 0.5),
                new PathPoint(20.5, 64, 0.5),
                probe,
                out _);

            Assert.False(ok);
        }
    }

    private static PathFixture CreateFixture(int mergeRadius, (int X, int Z)[] loadChunks)
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadius));
        var config = new OrionConfig();
        config.Server.WorldDefaultSettings.Dimensions.Add(new DimensionConfig
        {
            Identifier = "overworld",
            SpawnPosition = [0, 64, 0],
        });
        var provider = new InMemoryWorldProvider();
        var world = Orion.World.World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        Orion.World.Dimension dimension = world.GetDimension("overworld");

        var tickets = new List<SimulationTicket>();
        foreach ((int cx, int cz) in loadChunks)
        {
            tickets.Add(dimension.AcquireTicket(cx, cz));
        }

        var entity = new EntityHandle(1, dimension, 0, 0, world.Identifier);
        ChunkRegion region = regionizer.GetRegionAt(0, 0)!;
        return new PathFixture(world, provider, regionizer, region, dimension, entity, tickets);
    }

    private sealed class PathFixture : IDisposable
    {
        private readonly InMemoryWorldProvider _provider;
        private readonly List<SimulationTicket> _tickets;

        public PathFixture(
            Orion.World.World world,
            InMemoryWorldProvider provider,
            Regionizer regionizer,
            ChunkRegion region,
            Orion.World.Dimension dimension,
            EntityHandle entity,
            List<SimulationTicket> tickets)
        {
            World = world;
            _provider = provider;
            Regionizer = regionizer;
            Region = region;
            Dimension = dimension;
            Entity = entity;
            _tickets = tickets;
        }

        public Orion.World.World World { get; }
        public Regionizer Regionizer { get; }
        public ChunkRegion Region { get; }
        public Orion.World.Dimension Dimension { get; }
        public EntityHandle Entity { get; }

        public void Dispose()
        {
            foreach (SimulationTicket ticket in _tickets)
            {
                ticket.Dispose();
            }

            World.Dispose();
            _provider.Dispose();
        }
    }
}
