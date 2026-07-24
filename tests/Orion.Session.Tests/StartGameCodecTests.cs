using Orion.Config;
using Orion.Network;
using Orion.Player;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Region;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.Session.Tests;

public sealed class StartGameCodecTests
{
    [Fact]
    public void BuildStartGame_RoundTripsWithNotPresentCompression()
    {
        var config = new OrionConfig();
        config.Server.WorldDefaultSettings.Dimensions.Add(new DimensionConfig
        {
            Identifier = "overworld",
            SpawnPosition = [0, 64, 0],
        });
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(0));
        using var provider = new InMemoryWorldProvider();
        using var world = World.World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        var entity = new EntityHandle(1, world.GetDimension("overworld"), 0, 0, "default");
        var session = new ConnectionSession(new StubConnection());
        var player = new Orion.Player.Player(entity, session, regionizer, 0, 64, 0);
        StartGamePacket packet = PlayerSpawnPipeline.BuildStartGame(config, player);

        byte[] buf = new byte[2 * 1024 * 1024];
        int len = GamePacketCodec.Encode(packet, buf, CompressionMethod.NotPresent, 1);
        var packets = new List<DataPacket>();
        Assert.True(GamePacketCodec.TryDecode(buf.AsSpan(0, len), new byte[2 * 1024 * 1024], packets));
        Assert.IsType<StartGamePacket>(Assert.Single(packets));
    }
}
