using Orion.Config;
using Orion.Network;
using Orion.Player;
using Orion.Player.Traits;
using Orion.Protocol.Packets;
using Orion.Region;
using Orion.World.Chunk;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class PlayerChunkStreamingTests
{
    [Fact]
    public void VoidEncoder_ProducesNonEmptyPayload()
    {
        byte[] payload = VoidLevelChunkEncoder.EncodePayload();
        Assert.NotEmpty(payload);
        Assert.Empty(VoidLevelChunkEncoder.EncodeUnloadPayload());
    }

    [Fact]
    public void StreamingTrait_Radius1_SendsPublisherAndChunks()
    {
        var config = new OrionConfig
        {
            Server = new ServerRootConfig
            {
                Network = new NetworkConfig { CompressionMethod = 2, CompressionThreshold = 1 },
                WorldDefaultSettings = new WorldDefaultSettingsConfig
                {
                    Dimensions = [new DimensionConfig { Identifier = "overworld", ViewDistance = 1 }],
                },
            },
        };

        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(0));
        using var provider = new InMemoryWorldProvider();
        using var world = Orion.World.World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        var entity = new EntityHandle(1, world.GetDimension("overworld"), 0, 0, "default");
        entity.Traits.GetOrAdd(e => new Orion.Entity.Traits.ChunkLoadingTrait(e)).Enable(1);

        var sessions = new SessionManager();
        var session = sessions.Create(new StubNet());
        session.State = SessionState.InGame;
        var player = new Player.Player(entity, session, regionizer, 0, 64, 0);
        player.SetViewDistanceChebyshev(1);
        session.Player = player;

        var transport = new RecordingSend();
        PacketSendGate.Bind(new PacketSender(config, transport));

        var streaming = player.Entity.Traits.GetOrAdd(_ => new PlayerChunkStreamingTrait(player));
        streaming.Start();

        // Drain region tick path.
        player.TickRegion();

        var packets = transport.Frames.SelectMany(f =>
        {
            var list = new List<DataPacket>();
            GamePacketCodec.TryDecode(f, new byte[256 * 1024], list);
            return list;
        }).ToList();

        Assert.Contains(packets, p => p is NetworkChunkPublisherUpdatePacket);
        int loads = packets.Count(p => p is LevelChunkPacket lc && lc.RawPayload.Length > 0);
        Assert.InRange(loads, 1, 9);
        Assert.True(streaming.LoadedCount <= 9);
        Assert.True(streaming.LoadedCount >= 1);

        streaming.ApplyViewDistance(0); // invalid via SetViewDistance - use Apply with 1 then shrink via radius 0 not allowed
        streaming.ApplyViewDistance(1);
        // Shrink by moving then applying radius that unloads - set radius via ApplyViewDistance(1) keeps; use ApplyViewDistance after changing center
        player.Entity.SetChunkPosition(10, 10);
        player.TickRegion();
        Assert.True(streaming.LoadedCount <= 9);
    }

    private sealed class RecordingSend : INetworkSend
    {
        public List<byte[]> Frames { get; } = [];

        public void Send(RakNet.NetworkConnection connection, ReadOnlySpan<byte> payload, RakNet.Packets.Enums.Reliability reliability, bool immediate = false)
            => Frames.Add(payload.ToArray());

        public void Disconnect(RakNet.NetworkConnection connection)
        {
        }
    }

    private sealed class StubNet : RakNet.NetworkConnection
    {
        protected override void SendMessage(ReadOnlySpan<byte> payload)
        {
        }
    }
}
