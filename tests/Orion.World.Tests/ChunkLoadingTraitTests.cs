using Orion.Config;
using Orion.Entity.Traits;
using Orion.Region;
using Orion.World;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class ChunkLoadingTraitTests
{
    [Fact]
    public void Enable_LoadsChebyshevRadiusIntoRegionizer()
    {
        using Harness h = CreateHarness();
        EntityHandle entity = h.CreateEntity(chunkX: 0, chunkZ: 0);
        ChunkLoadingTrait trait = entity.Traits.GetOrAdd(e => new ChunkLoadingTrait(e));

        trait.Enable(radiusChunks: 1);

        Assert.True(trait.IsEnabled);
        Assert.Equal(1, trait.RadiusChunks);
        Assert.Equal(9, trait.HeldTicketCount); // 3x3
        Assert.NotNull(h.Regionizer.GetRegionAt(0, 0));
        Assert.NotNull(h.Regionizer.GetRegionAt(1, 1));
        Assert.NotNull(h.Regionizer.GetRegionAt(-1, -1));
        Assert.Null(h.Regionizer.GetRegionAt(2, 0));
    }

    [Fact]
    public void SetRadius_ShrinksAndGrowsTickets()
    {
        using Harness h = CreateHarness();
        EntityHandle entity = h.CreateEntity(0, 0);
        ChunkLoadingTrait trait = entity.Traits.GetOrAdd(e => new ChunkLoadingTrait(e));
        trait.Enable(2);
        Assert.Equal(25, trait.HeldTicketCount); // 5x5

        trait.SetRadius(0);
        Assert.Equal(1, trait.HeldTicketCount);
        Assert.NotNull(h.Regionizer.GetRegionAt(0, 0));
        Assert.Null(h.Regionizer.GetRegionAt(1, 0));

        trait.SetRadius(1);
        Assert.Equal(9, trait.HeldTicketCount);
        Assert.NotNull(h.Regionizer.GetRegionAt(1, 0));
    }

    [Fact]
    public void Disable_UnloadsWhenNothingElseReferences()
    {
        using Harness h = CreateHarness();
        EntityHandle entity = h.CreateEntity(5, 5);
        ChunkLoadingTrait trait = entity.Traits.GetOrAdd(e => new ChunkLoadingTrait(e));
        trait.Enable(1);
        Assert.NotNull(h.Regionizer.GetRegionAt(5, 5));

        trait.Disable();
        Assert.False(trait.IsEnabled);
        Assert.Equal(0, trait.HeldTicketCount);
        Assert.Null(h.Regionizer.GetRegionAt(5, 5));
        Assert.Null(h.Regionizer.GetRegionAt(6, 6));
    }

    [Fact]
    public void OverlappingEntities_SharedChunkSurvivesOneDisable()
    {
        using Harness h = CreateHarness();
        EntityHandle a = h.CreateEntity(0, 0);
        EntityHandle b = h.CreateEntity(1, 0);
        ChunkLoadingTrait ta = a.Traits.GetOrAdd(e => new ChunkLoadingTrait(e));
        ChunkLoadingTrait tb = b.Traits.GetOrAdd(e => new ChunkLoadingTrait(e));

        ta.Enable(1);
        tb.Enable(1);
        Assert.NotNull(h.Regionizer.GetRegionAt(0, 0));

        ta.Disable();
        Assert.NotNull(h.Regionizer.GetRegionAt(0, 0));
        Assert.NotNull(h.Regionizer.GetRegionAt(1, 0));

        tb.Disable();
        Assert.Null(h.Regionizer.GetRegionAt(0, 0));
        Assert.Null(h.Regionizer.GetRegionAt(1, 0));
    }

    [Fact]
    public void SetChunkPosition_ResyncsTickets()
    {
        using Harness h = CreateHarness();
        EntityHandle entity = h.CreateEntity(0, 0);
        ChunkLoadingTrait trait = entity.Traits.GetOrAdd(e => new ChunkLoadingTrait(e));
        trait.Enable(0);
        Assert.NotNull(h.Regionizer.GetRegionAt(0, 0));

        entity.SetChunkPosition(3, 4);
        Assert.Null(h.Regionizer.GetRegionAt(0, 0));
        Assert.NotNull(h.Regionizer.GetRegionAt(3, 4));
        Assert.Equal(1, trait.HeldTicketCount);
    }

    [Fact]
    public void Remove_ReleasesTickets()
    {
        using Harness h = CreateHarness();
        EntityHandle entity = h.CreateEntity(2, 2);
        ChunkLoadingTrait trait = entity.Traits.GetOrAdd(e => new ChunkLoadingTrait(e));
        trait.Enable(1);
        Assert.NotNull(h.Regionizer.GetRegionAt(2, 2));

        entity.Remove();
        Assert.True(entity.IsRemoved);
        Assert.Null(h.Regionizer.GetRegionAt(2, 2));
        Assert.Throws<InvalidOperationException>(() => trait.Enable(1));
    }

    private static Harness CreateHarness()
    {
        // Exponent 0 → one chunk per section so GetRegionAt mirrors ticket unload.
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(0));
        var provider = new InMemoryWorldProvider();
        World world = World.CreateFromConfig(
            new WorldDefaultSettingsConfig
            {
                Identifier = "default",
                Dimensions = [new DimensionConfig { Identifier = "overworld", Generator = "void" }],
            },
            regionizer,
            provider);
        return new Harness(world, regionizer, provider);
    }

    private sealed class Harness : IDisposable
    {
        private long _nextId = 1;

        public Harness(World world, Regionizer regionizer, InMemoryWorldProvider provider)
        {
            World = world;
            Regionizer = regionizer;
            Provider = provider;
        }

        public World World { get; }

        public Regionizer Regionizer { get; }

        public InMemoryWorldProvider Provider { get; }

        public EntityHandle CreateEntity(int chunkX, int chunkZ)
        {
            Dimension dim = World.GetDimension("overworld");
            return new EntityHandle(_nextId++, dim, chunkX, chunkZ, World.Identifier);
        }

        public void Dispose() => World.Dispose();
    }
}
