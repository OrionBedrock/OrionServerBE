using Orion.Config;
using Orion.Region;
using Orion.World;
using Orion.World.Provider;
using Orion.World.Tickets;
using Xunit;

namespace Orion.World.Tests;

public sealed class SimulationTicketTests
{
    [Fact]
    public void AcquireTicket_AddsChunkToRegionizer()
    {
        using var provider = new InMemoryWorldProvider();
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
        using World world = World.CreateFromConfig(DefaultSettings(), regionizer, provider);

        Dimension overworld = world.GetDimension("overworld");
        using SimulationTicket ticket = overworld.AcquireTicket(3, -2);

        Assert.NotNull(regionizer.GetRegionAt(3, -2));
        Assert.NotNull(overworld.Tickets.GetLoadedChunk("overworld", 3, -2));
        Assert.Equal(1, overworld.Tickets.LoadedChunkCount);
        Assert.Equal(3, ticket.ChunkX);
        Assert.Equal(-2, ticket.ChunkZ);
    }

    [Fact]
    public void ReleaseTicket_UnloadsWhenRefcountZero()
    {
        using var provider = new InMemoryWorldProvider();
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
        using World world = World.CreateFromConfig(DefaultSettings(), regionizer, provider);
        Dimension overworld = world.GetDimension("overworld");

        SimulationTicket first = overworld.AcquireTicket(1, 1);
        SimulationTicket second = overworld.AcquireTicket(1, 1);
        Assert.Equal(1, overworld.Tickets.LoadedChunkCount);
        Assert.NotNull(regionizer.GetRegionAt(1, 1));

        first.Dispose();
        Assert.NotNull(regionizer.GetRegionAt(1, 1));
        Assert.Equal(1, overworld.Tickets.LoadedChunkCount);

        second.Dispose();
        Assert.Null(regionizer.GetRegionAt(1, 1));
        Assert.Equal(0, overworld.Tickets.LoadedChunkCount);
    }

    [Fact]
    public void InMemoryProvider_RoundTripsChunk()
    {
        using var provider = new InMemoryWorldProvider();
        var chunk = new Chunk.ChunkColumn(8, 9) { IsGenerated = true, IsDirty = true };
        provider.SaveChunk("overworld", chunk);

        Assert.True(provider.HasChunk("overworld", 8, 9));
        Chunk.ChunkColumn? loaded = provider.LoadChunk("overworld", 8, 9);
        Assert.NotNull(loaded);
        Assert.Equal(8, loaded.ChunkX);
        Assert.Equal(9, loaded.ChunkZ);
        Assert.True(loaded.IsGenerated);
        Assert.False(chunk.IsDirty);
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
