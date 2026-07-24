using Orion.Config;
using Orion.Network;
using Orion.Network.Handlers;
using Orion.Protocol.Packets;
using Orion.Region;
using Orion.World.Chunk;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class RequestChunkRadiusTests
{
    [Fact]
    public void Negotiate_ClampsToViewDistanceAndSendsCircle()
    {
        var (context, session, player, transport) = Create(viewDistance: 8);

        RequestChunkRadiusHandler.Handle(
            context,
            session,
            new RequestChunkRadiusPacket { ChunkRadius = 64, MaxChunkRadius = 96 });

        Assert.Equal(8, player.ViewDistanceChebyshev);
        Assert.Single(transport.Frames);
        var packets = Decode(transport.Frames[0]);
        var updated = Assert.IsType<UpdateChunkRadiusPacket>(Assert.Single(packets));
        Assert.Equal(ChunkViewMath.SquareToCircle(8), updated.ChunkRadius);
        Assert.NotEqual(8, updated.ChunkRadius);
    }

    [Fact]
    public void Negotiate_RespectsClientMaxCircle()
    {
        var (context, session, player, _) = Create(viewDistance: 32);

        const byte clientMax = 8;
        RequestChunkRadiusHandler.Handle(
            context,
            session,
            new RequestChunkRadiusPacket { ChunkRadius = 32, MaxChunkRadius = clientMax });

        int expectedChebyshev = ChunkViewMath.MaxChebyshevForClientCircle(clientMax);
        Assert.Equal(expectedChebyshev, player.ViewDistanceChebyshev);
        Assert.True(ChunkViewMath.SquareToCircle(player.ViewDistanceChebyshev) <= clientMax);
    }

    private static (ServerContext Context, ConnectionSession Session, Player.Player Player, RecordingSend Transport) Create(int viewDistance)
    {
        var config = new OrionConfig
        {
            Server = new ServerRootConfig
            {
                WorldDefaultSettings = new WorldDefaultSettingsConfig
                {
                    Dimensions =
                    [
                        new DimensionConfig
                        {
                            Identifier = "overworld",
                            ViewDistance = viewDistance,
                            SpawnPosition = [0, 64, 0],
                        },
                    ],
                },
                Network = new NetworkConfig { CompressionMethod = 2, CompressionThreshold = 1 },
            },
        };

        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(0));
        var provider = new InMemoryWorldProvider();
        var world = Orion.World.World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        var entity = new EntityHandle(1, world.GetDimension("overworld"), 0, 0, "default");
        var sessions = new SessionManager();
        var session = sessions.Create(new StubNetConnection());
        session.State = SessionState.InGame;
        var player = new Player.Player(entity, session, regionizer, 0, 64, 0);
        session.Player = player;

        var transport = new RecordingSend();
        var sender = new PacketSender(config, transport);
        var context = new ServerContext(config, sessions, sender, new SessionPacketQueue(), new SessionWorkQueue());
        return (context, session, player, transport);
    }

    private static List<DataPacket> Decode(byte[] frame)
    {
        var packets = new List<DataPacket>();
        Assert.True(GamePacketCodec.TryDecode(frame, new byte[256 * 1024], packets));
        return packets;
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

    private sealed class StubNetConnection : RakNet.NetworkConnection
    {
        protected override void SendMessage(ReadOnlySpan<byte> payload)
        {
        }
    }
}
