using Orion.Block;
using Orion.Commands;
using Orion.PluginContracts;
using Orion.PluginContracts.Registry;
using Orion.Plugins;
using Orion.Plugins.Registry;
using Orion.Protocol.Packets;
using Orion.Protocol.Registry;
using Orion.World.Generation;
using Orion.World.Chunk;
using Orion.Protocol.Enums;

namespace Orion.Game.Tests;

[Collection("PluginHost")]
public sealed class ContentRegistriesTests
{
    public ContentRegistriesTests()
    {
        PluginHost.ResetForTests();
        CuratedItemCatalog.ResetForTests();
        GeneratorFactory.ResetForTests();
        BlockRegistry.ResetForTests();
    }

    [Fact]
    public void CreativeTabs_ViaFacade_MatchesDirectCatalogRegistration()
    {
        IContentRegistries registries = PluginHost.Registries.ForPlugin("orion:creative-fillers");
        registries.CreativeTabs.AddEntry("orion:creative-fillers", 1, "minecraft:cobblestone");
        registries.CreativeTabs.AddEntry("orion:creative-fillers", 3, "minecraft:wooden_sword");
        registries.CreativeTabs.AddEntry("orion:creative-fillers", 4, "minecraft:stick");

        byte[] payload = CuratedItemCatalog.GetCreativeContentPayload();
        int offset = 0;
        Basalt.Binary.BinaryReader reader = new(payload, ref offset);
        CreativeContentPacket packet = new();
        packet.Deserialize(reader);

        Assert.Equal(6, packet.Items.Count);
        Assert.Contains("orion:creative-fillers", CuratedItemCatalog.GetLoadedCreativePlugins());
        Assert.False(CuratedItemCatalog.NonNatureCreativeTabsEmpty);
    }

    [Fact]
    public void CreativeTabs_NatureCategory_Rejected()
    {
        IContentRegistries registries = PluginHost.Registries.ForPlugin("p1");
        registries.CreativeTabs.AddEntry("p1", 2, "minecraft:cobblestone");

        _ = CuratedItemCatalog.GetCreativeContentPayload();
        Assert.DoesNotContain(
            CuratedItemCatalog.GetCreativeMenuItems(),
            i => i.Identifier == "minecraft:cobblestone");
    }

    [Fact]
    public void CreativeTabs_DuplicateIdentifier_SecondPluginRejected()
    {
        PluginHost.Registries.ForPlugin("owner").CreativeTabs.AddEntry("owner", 1, "minecraft:cobblestone");
        PluginHost.Registries.ForPlugin("other").CreativeTabs.AddEntry("other", 1, "minecraft:cobblestone");

        _ = CuratedItemCatalog.GetCreativeContentPayload();
        Assert.Contains("owner", CuratedItemCatalog.GetLoadedCreativePlugins());
        Assert.DoesNotContain("other", CuratedItemCatalog.GetLoadedCreativePlugins());
    }

    [Fact]
    public void CreativeTabs_AfterFreeze_Throws()
    {
        _ = CuratedItemCatalog.GetCreativeContentPayload();
        PluginHost.NotifyCatalogLoaded();

        Assert.Throws<InvalidOperationException>(() =>
            PluginHost.Registries.ForPlugin("late").CreativeTabs.AddEntry("late", 1, "minecraft:stick"));
    }

    [Fact]
    public void Commands_RegisterOnEnable_AppearsInRegistry()
    {
        PingCommandPlugin plugin = new();
        PluginHost.RegisterLoadedForTests(
            plugin,
            new PluginManifest
            {
                Id = "cmd.ping",
                Version = new Version(1, 0, 0),
                Main = "PingCommandPlugin"
            });

        Server server = new();
        PluginHost.EnableAll(server);

        Command registered = server.Commands.Get("ping");
        Assert.Equal("ping", registered.Name);

        CommandResult result = server.Commands.Execute(server, "ping hello");
        Assert.True(result.Success);
        Assert.True(plugin.Executed);

        PluginHost.DisableAll();
    }

    [Fact]
    public void Generators_Register_ResolvesInFactory()
    {
        PluginHost.Registries.ForPlugin("gen").Generators.Register("testflat", typeof(TestFlatGenerator));
        Generator generator = GeneratorFactory.Create("testflat");
        Assert.IsType<TestFlatGenerator>(generator);

        PluginHost.NotifyWorldBootstrapped();
        Assert.Throws<InvalidOperationException>(() =>
            PluginHost.Registries.ForPlugin("gen2").Generators.Register("other", typeof(TestFlatGenerator)));
    }

    [Fact]
    public void Blocks_Register_BeforeEnsureLoaded_Succeeds_ThenFreeze()
    {
        PluginHost.Registries.ForPlugin("blocks").Blocks.Register(
            new BlockRegistration("minecraft:test_plugin_block", DefaultStateHash: 123456, Solid: true));

        BlockRegistry.EnsureLoaded();
        PluginHost.NotifyCatalogLoaded();

        Assert.Throws<InvalidOperationException>(() =>
            PluginHost.Registries.ForPlugin("blocks").Blocks.Register(
                new BlockRegistration("minecraft:late_block", 1)));
    }

    sealed class PingCommandPlugin : IOrionPlugin
    {
        public string Id => "cmd.ping";
        public Version Version => new(1, 0, 0);
        public bool Executed { get; private set; }

        public void Load(IPluginLoadContext context)
        {
        }

        public void OnEnable(IPluginContext context)
        {
            context.Registries.Commands.Register(new PingCommand(this));
        }

        public void OnWorldInitialize(IWorldInitContext context)
        {
        }

        public void OnDisable(IPluginContext context)
        {
        }

        sealed class PingCommand(PingCommandPlugin owner) : IPluginCommand
        {
            public string Name => "ping";
            public string Description => "Ping";
            public IReadOnlyList<string> Aliases => [];

            public void Execute(IPluginCommandContext context)
            {
                owner.Executed = true;
                context.Reply("pong");
            }
        }
    }

    sealed class TestFlatGenerator : Generator
    {
        public override string Identifier => "testflat";

        public override Chunk Generate(DimensionType dimensionType, int x, int z) =>
            new(x, z, dimensionType);
    }
}
