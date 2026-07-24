using Orion.Config;
using Orion.Region;
using Orion.Runtime;
using Orion.Scheduler;
using Orion.World;
using Orion.World.Chunk;
using Orion.World.Generation;
using Orion.World.Provider;
using Orion.World.Tickets;
using Xunit;

namespace Orion.World.Tests;

public sealed class VoidGeneratorTests
{
    [Fact]
    public void VoidGenerator_MarksColumnGeneratedAndEmpty()
    {
        var generator = new VoidGenerator();
        var chunk = new ChunkColumn(0, 0);
        generator.Generate(chunk);

        Assert.Equal(VoidGenerator.Id, generator.Identifier);
        Assert.True(chunk.IsGenerated);
        Assert.True(chunk.IsDirty);
        Assert.Null(chunk.GetSubChunk(0));
    }

    [Fact]
    public void TicketOnNewChunk_AppliesVoidViaPipeline()
    {
        using var provider = new InMemoryWorldProvider();
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
        var regionScheduler = new RegionScheduler(regionizer);
        var pipeline = new ChunkLoadPipeline(regionizer, regionScheduler, new VoidGenerator());
        using World world = World.CreateFromConfig(DefaultSettings(), regionizer, provider, pipeline);

        Dimension overworld = world.GetDimension("overworld");
        using SimulationTicket ticket = overworld.AcquireTicket(4, 5);

        Assert.NotNull(regionizer.GetRegionAt(4, 5));
        ChunkColumn? loaded = overworld.Tickets.GetLoadedChunk("overworld", 4, 5);
        Assert.NotNull(loaded);
        Assert.True(loaded.IsGenerated);
        Assert.False(pipeline.IsInflight("overworld", 4, 5));
    }

    [Fact]
    public void TicketOnNewChunk_WithThreadPool_AppliesVoid()
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
        using var provider = new InMemoryWorldProvider();
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
        var regionScheduler = new RegionScheduler(regionizer);
        var pipeline = new ChunkLoadPipeline(regionizer, regionScheduler, new VoidGenerator(), pools);
        using World world = World.CreateFromConfig(DefaultSettings(), regionizer, provider, pipeline);

        Dimension overworld = world.GetDimension("overworld");
        using SimulationTicket ticket = overworld.AcquireTicket(7, 8);

        Assert.True(SpinWait.SpinUntil(
            () =>
            {
                ChunkColumn? c = overworld.Tickets.GetLoadedChunk("overworld", 7, 8);
                return c is { IsGenerated: true };
            },
            TimeSpan.FromSeconds(5)));

        Assert.NotNull(regionizer.GetRegionAt(7, 8));
    }

    private static WorldDefaultSettingsConfig DefaultSettings() => new()
    {
        Identifier = "default",
        Seed = 1,
        Dimensions =
        [
            new DimensionConfig
            {
                Identifier = "overworld",
                Generator = "void",
            },
        ],
    };
}
