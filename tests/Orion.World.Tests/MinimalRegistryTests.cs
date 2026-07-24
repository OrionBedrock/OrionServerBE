using Orion.Config;
using Orion.Network;
using Orion.Player;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.Region;
using Orion.Registries;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class MinimalRegistryTests
{
    [Fact]
    public void CreateMinimal_ContainsSevenRoadmapIds()
    {
        ServerRegistries registries = ServerRegistries.CreateMinimal();
        foreach (string id in ServerRegistries.MinimalContentIds)
        {
            Assert.True(
                registries.Blocks.Contains(id) || registries.Items.Contains(id),
                $"Missing '{id}'");
        }

        Assert.True(registries.Blocks.Contains(ServerRegistries.Dirt));
        Assert.True(registries.Blocks.Contains(ServerRegistries.GrassBlock));
        Assert.True(registries.Items.Contains(ServerRegistries.WoodenSword));
        Assert.False(registries.Blocks.Contains(ServerRegistries.WoodenSword));
    }

    [Fact]
    public void BlockNetworkId_DirtIsStable()
    {
        int first = BlockNetworkId.ForIdentifier(ServerRegistries.Dirt);
        int second = BlockNetworkId.ForIdentifier(ServerRegistries.Dirt);
        Assert.Equal(first, second);
        Assert.NotEqual(0, first);
        Assert.NotEqual(BlockNetworkId.Air, first);
    }

    [Fact]
    public void ToBlockEntries_HasSixBlocksWithoutAir()
    {
        ServerRegistries registries = ServerRegistries.CreateMinimal();
        List<BlockEntry> blocks = registries.ToBlockEntries();
        Assert.Equal(6, blocks.Count);
        Assert.Contains(blocks, b => b.Name == ServerRegistries.Dirt);
        Assert.DoesNotContain(blocks, b => b.Name == ServerRegistries.Air);
    }

    [Fact]
    public void ToItemEntries_IncludesWoodenSword()
    {
        ServerRegistries registries = ServerRegistries.CreateMinimal();
        List<ItemEntry> items = registries.ToItemEntries();
        Assert.Contains(items, i => i.Name == ServerRegistries.WoodenSword);
        Assert.True(items.Count >= 7);
    }

    [Fact]
    public void BuildStartGame_UsesRegistryBlocks()
    {
        ServerRegistries registries = ServerRegistries.CreateMinimal();
        var config = new OrionConfig();
        config.Server.WorldDefaultSettings.Dimensions.Add(new DimensionConfig
        {
            Identifier = "overworld",
            SpawnPosition = [0, 64, 0],
        });
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(0));
        using var provider = new InMemoryWorldProvider();
        using var world = World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        var entity = new EntityHandle(1, world.GetDimension("overworld"), 0, 0, "default");
        var session = new ConnectionSession(new StubNet());
        var player = new Player.Player(entity, session, regionizer, 0, 64, 0);

        StartGamePacket start = PlayerSpawnPipeline.BuildStartGame(config, player, registries);
        Assert.True(start.Blocks.Count >= 6);
        Assert.Contains(start.Blocks, b => b.Name == ServerRegistries.GrassBlock);
    }

    private sealed class StubNet : RakNet.NetworkConnection
    {
        protected override void SendMessage(ReadOnlySpan<byte> payload)
        {
        }
    }
}
