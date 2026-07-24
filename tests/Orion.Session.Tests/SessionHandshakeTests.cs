using Orion.Config;
using Orion.Network;
using Orion.Network.Handlers;
using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Packets;
using RakNet;
using RakNet.Packets.Enums;
using Xunit;

namespace Orion.Session.Tests;

public sealed class AuthPolicyTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public void AllowsOffline_MatchesFlags(bool onlineMode, bool allowOfflineDev, bool expected)
    {
        var orion = new OrionSectionConfig
        {
            OnlineMode = onlineMode,
            AllowOfflineDev = allowOfflineDev,
        };

        Assert.Equal(expected, AuthPolicy.AllowsOffline(orion));
    }

    [Fact]
    public void CreateOfflineGuid_IsStableForUsername()
    {
        Guid a = AuthPolicy.CreateOfflineGuid("Steve");
        Guid b = AuthPolicy.CreateOfflineGuid("steve");
        Guid c = AuthPolicy.CreateOfflineGuid("Alex");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}

public sealed class SessionHandshakeTests
{
    [Fact]
    public void RequestNetworkSettings_AcceptsMatchingProtocol()
    {
        var config = CreateConfig(onlineMode: true, allowOfflineDev: true);
        var (context, session, transport) = CreateHarness(config);

        RequestNetworkSettingsHandler.Handle(
            context,
            session,
            new RequestNetworkSettingsPacket(Constants.ProtocolVersion));

        Assert.Equal(SessionState.NetworkReady, session.State);
        Assert.True(transport.Frames.Count >= 1);
        Assert.Equal(0xFE, transport.Frames[0][0]);

        var packets = DecodeFrame(transport.Frames[0]);
        Assert.Contains(packets, p => p is NetworkSettingsPacket);
    }

    [Fact]
    public void RequestNetworkSettings_RejectsMismatchedProtocol()
    {
        var config = CreateConfig(onlineMode: true, allowOfflineDev: true);
        var (context, session, transport) = CreateHarness(config);

        RequestNetworkSettingsHandler.Handle(
            context,
            session,
            new RequestNetworkSettingsPacket(Constants.ProtocolVersion - 1));

        Assert.True(transport.Disconnected);
        var packets = DecodeFrame(transport.Frames[0]);
        Assert.Contains(packets, p => p is DisconnectPacket);
    }

    [Fact]
    public void Login_RejectsOfflineWhenFlagDisabled()
    {
        var config = CreateConfig(onlineMode: true, allowOfflineDev: false);
        var (context, session, transport) = CreateHarness(config);

        var login = new LoginPacket
        {
            Protocol = Constants.ProtocolVersion,
            Identity = """{"AuthenticationType":2,"Token":"","Certificate":""}""",
            Client = "{}",
        };

        LoginHandler.Handle(context, session, login);

        Assert.True(transport.Disconnected);
        var packets = DecodeFrame(transport.Frames[^1]);
        var disconnect = Assert.IsType<DisconnectPacket>(Assert.Single(packets));
        Assert.Contains("Offline", disconnect.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResourcePackClientResponse_SendsEmptyStack()
    {
        var config = CreateConfig(onlineMode: true, allowOfflineDev: true);
        var (context, session, transport) = CreateHarness(config);
        session.State = SessionState.PacksSent;

        ResourcePackClientResponseHandler.Handle(
            context,
            session,
            new ResourcePackClientResponsePacket
            {
                Response = ResourcePackResponse.AllPacksDownloaded,
            });

        var packets = DecodeFrame(transport.Frames[^1]);
        var stack = Assert.IsType<ResourcePackStackPacket>(Assert.Single(packets));
        Assert.Empty(stack.Packs);
        Assert.Equal(Constants.MinecraftVersion, stack.BaseGameVersion);
    }

    [Fact]
    public void ResourcePackCompleted_MarksHandshakeComplete()
    {
        var config = CreateConfig(onlineMode: true, allowOfflineDev: true);
        var (context, session, _) = CreateHarness(config);
        session.State = SessionState.PacksSent;

        ResourcePackClientResponseHandler.Handle(
            context,
            session,
            new ResourcePackClientResponsePacket
            {
                Response = ResourcePackResponse.Completed,
            });

        Assert.Equal(SessionState.HandshakeComplete, session.State);
    }

    [Fact]
    public void Frame_EncodeNetworkSettings_RoundTrips()
    {
        var original = new NetworkSettingsPacket
        {
            CompressionThreshold = 1,
            CompressionMethod = CompressionMethod.Zlib,
            ClientThrottle = false,
            ClientThrottleThreshold = 0,
            ClientThrottleScalar = 0f,
        };

        Span<byte> framed = stackalloc byte[256];
        int length = GamePacketCodec.Encode(original, framed, CompressionMethod.NotPresent, 1);
        Assert.Equal(0xFE, framed[0]);

        Span<byte> scratch = stackalloc byte[1024];
        var packets = new List<DataPacket>();
        Assert.True(GamePacketCodec.TryDecode(framed[..length], scratch, packets));
        var decoded = Assert.IsType<NetworkSettingsPacket>(Assert.Single(packets));
        Assert.Equal(original.CompressionMethod, decoded.CompressionMethod);
    }

    private static OrionConfig CreateConfig(bool onlineMode, bool allowOfflineDev) => new()
    {
        Server = new ServerRootConfig
        {
            Orion = new OrionSectionConfig
            {
                OnlineMode = onlineMode,
                AllowOfflineDev = allowOfflineDev,
            },
            Network = new NetworkConfig
            {
                CompressionMethod = 0,
                CompressionThreshold = 1,
            },
        },
    };

    private static (ServerContext Context, ConnectionSession Session, RecordingNetworkSend Transport) CreateHarness(OrionConfig config)
    {
        var connection = new StubConnection();
        var sessions = new SessionManager();
        var session = sessions.Create(connection);
        var transport = new RecordingNetworkSend();
        var sender = new PacketSender(config, transport);
        var context = new ServerContext(config, sessions, sender, new SessionPacketQueue());
        return (context, session, transport);
    }

    private static List<DataPacket> DecodeFrame(byte[] framed)
    {
        Span<byte> scratch = stackalloc byte[64 * 1024];
        var packets = new List<DataPacket>();
        Assert.True(GamePacketCodec.TryDecode(framed, scratch, packets));
        return packets;
    }
}

internal sealed class RecordingNetworkSend : INetworkSend
{
    public List<byte[]> Frames { get; } = [];
    public bool Disconnected { get; private set; }

    public void Send(NetworkConnection connection, ReadOnlySpan<byte> payload, Reliability reliability, bool immediate = false)
        => Frames.Add(payload.ToArray());

    public void Disconnect(NetworkConnection connection)
        => Disconnected = true;
}

internal sealed class StubConnection : NetworkConnection
{
    protected override void SendMessage(ReadOnlySpan<byte> payload)
    {
    }
}

