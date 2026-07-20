using Orion.Api.Traits;
using Orion.Block;
using Orion.Block.Traits;
using Orion.PluginContracts.Registry;
using Orion.Plugins;
using Orion.Plugins.Registry;
using Orion.Protocol.Registry;

namespace Orion.Game.Tests;

[Collection("PluginHost")]
public sealed class TraitRegistryTests
{
    public TraitRegistryTests()
    {
        PluginHost.ResetForTests();
        CuratedItemCatalog.ResetForTests();
        BlockRegistry.ResetForTests();
    }

    [Fact]
    public void BlockTraits_Register_ViaContentRegistries_BeforeFreeze()
    {
        IContentRegistries registries = PluginHost.Registries.ForPlugin("test:traits");
        registries.Blocks.Register(new BlockRegistration(
            "orion:test_trait_block",
            DefaultStateHash: 0x0E57_7001,
            Solid: true,
            Hardness: 1.5f,
            Tags: ["orion:test"]));

        registries.BlockTraits.Register(typeof(TestBlockTrait), "test:traits");

        BlockRegistry.EnsureLoaded();
        PluginHost.NotifyCatalogLoaded();

        Assert.Contains("test_block_trait", BlockTraitRegistry.RegisteredTraits.Keys);
        BlockType? type = BlockType.Get("orion:test_trait_block");
        Assert.NotNull(type);
        Assert.Equal(1.5f, type!.Hardness);
        Assert.Contains("orion:test", type.Tags);
    }

    [Fact]
    public void BlockTraits_Register_AfterFreeze_Throws()
    {
        BlockRegistry.EnsureLoaded();
        PluginHost.NotifyCatalogLoaded();

        IContentRegistries registries = PluginHost.Registries.ForPlugin("late");
        Assert.Throws<InvalidOperationException>(() =>
            registries.BlockTraits.Register(typeof(TestBlockTrait), "late"));
    }

    [Fact]
    public void TraitBases_Live_In_Orion_Api()
    {
        Assert.Equal("Orion.Api", typeof(BlockTraitBase).Assembly.GetName().Name);
        Assert.Equal("Orion.Api", typeof(ItemTraitBase).Assembly.GetName().Name);
        Assert.Equal("Orion.Api", typeof(EntityTraitBase).Assembly.GetName().Name);
        Assert.Equal("Orion.Api", typeof(PlayerTraitBase).Assembly.GetName().Name);
    }

    sealed class TestBlockTrait : BlockTrait
    {
        public static new string Identifier => "test_block_trait";
        public static new readonly string[] Types = ["orion:test_trait_block"];

        public TestBlockTrait(Block.Block block) : base(block)
        {
        }
    }
}
