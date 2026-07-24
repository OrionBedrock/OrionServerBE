using Orion.Entity;
using Orion.Registries;
using Orion.Traits;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class TraitSurfaceTests
{
    private sealed class ProbeBlockTrait : BlockTrait
    {
        public int PlaceCount;
        public int BreakCount;

        public override void OnPlace() => PlaceCount++;

        public override void OnBreak() => BreakCount++;
    }

    private sealed class ProbeItemTrait : ItemTrait
    {
        public int UseCount;

        public override void OnUse() => UseCount++;
    }

    private sealed class ProbeEntityTrait : IEntityTrait
    {
        public void OnDetach()
        {
        }
    }

    [Fact]
    public void BlockTraitRegistry_RegisterAndTryGet()
    {
        ServerRegistries registries = ServerRegistries.CreateMinimal();
        var trait = new ProbeBlockTrait();
        registries.BlockTraits.Register(ServerRegistries.Dirt, trait);

        Assert.True(registries.BlockTraits.TryGet(ServerRegistries.Dirt, out IBlockTrait? found));
        Assert.Same(trait, found);
        found!.OnPlace();
        found.OnBreak();
        Assert.Equal(1, trait.PlaceCount);
        Assert.Equal(1, trait.BreakCount);
    }

    [Fact]
    public void ItemTraitRegistry_RegisterAndTryGet()
    {
        ServerRegistries registries = ServerRegistries.CreateMinimal();
        var trait = new ProbeItemTrait();
        registries.ItemTraits.Register(ServerRegistries.WoodenSword, trait);

        Assert.True(registries.ItemTraits.TryGet(ServerRegistries.WoodenSword, out IItemTrait? found));
        Assert.Same(trait, found);
        found!.OnUse();
        Assert.Equal(1, trait.UseCount);
    }

    [Fact]
    public void EntityTraitRegistry_RegisterFactory()
    {
        ServerRegistries registries = ServerRegistries.CreateMinimal();
        registries.EntityTraits.Register("probe", static _ => new ProbeEntityTrait());

        Assert.True(registries.EntityTraits.TryGet("probe", out EntityTraitFactory? factory));
        Assert.NotNull(factory);
    }

    [Fact]
    public void TraitBag_GetOrAdd_StillWorks()
    {
        var config = new Orion.Config.OrionConfig();
        config.Server.WorldDefaultSettings.Dimensions.Add(new Orion.Config.DimensionConfig
        {
            Identifier = "overworld",
            SpawnPosition = [0, 64, 0],
        });
        var regionizer = new Orion.Region.Regionizer(Orion.Region.RegionizerOptions.FromGridExponent(0));
        using var provider = new InMemoryWorldProvider();
        using var world = World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        var bagHost = new EntityHandle(1, world.GetDimension("overworld"), 0, 0, "default");
        ProbeEntityTrait trait = bagHost.Traits.GetOrAdd(_ => new ProbeEntityTrait());
        Assert.True(bagHost.Traits.TryGet(out ProbeEntityTrait? found));
        Assert.Same(trait, found);
    }
}
