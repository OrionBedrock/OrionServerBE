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

    [Fact]
    public async Task Login_OnlinePath_RejectsInvalidJwt()
    {
        var config = CreateConfig(onlineMode: true, allowOfflineDev: false);
        var (context, session, transport) = CreateHarness(config);

        // RS256-shaped token that is not offline (aud/alg look online) but fails authority verify.
        var login = new LoginPacket
        {
            Protocol = Constants.ProtocolVersion,
            Identity = """{"AuthenticationType":0,"Token":"eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6InRlc3QifQ.eyJhdWQiOiJhcGk6Ly9hdXRoLW1pbmVjcmFmdC1zZXJ2aWNlcy9tdWx0aXBsYXllciIsImlzcyI6Imh0dHBzOi9leGFtcGxlLmludmFsaWQiLCJleHAiOjQ4MDAwMDAwMDAsInhuYW1lIjoiU3RldmUiLCJ4aWQiOiIxIiwiaWRlbnRpdHkiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAifQ.sig"}""",
            Client = FakeClientJwt(),
        };

        LoginHandler.Handle(context, session, login);
        Assert.Equal(SessionState.Authenticating, session.State);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && transport.Disconnected == false && context.Work.Count == 0)
        {
            await Task.Delay(50);
        }

        context.Work.Drain();

        Assert.True(transport.Disconnected);
        Assert.NotEmpty(transport.Frames);
        var packets = DecodeFrame(transport.Frames[^1]);
        var disconnect = Assert.IsType<DisconnectPacket>(Assert.Single(packets));
        Assert.Equal("Authentication failed.", disconnect.Message);
    }

    [Fact]
    public void Login_CompleteLogin_KicksDuplicateUsername()
    {
        var config = CreateConfig(onlineMode: false, allowOfflineDev: true);
        var transport = new RecordingNetworkSend();
        var sender = new PacketSender(config, transport);
        var sessions = new SessionManager();
        var work = new SessionWorkQueue();
        var context = new ServerContext(config, sessions, sender, new SessionPacketQueue(), work);

        var first = sessions.Create(new StubConnection());
        var second = sessions.Create(new StubConnection());

        var identity = new Orion.Protocol.Login.VerifiedIdentity("pk", "Steve", "xuid-1", Guid.NewGuid().ToString());
        var login = new LoginPacket
        {
            Protocol = Constants.ProtocolVersion,
            Identity = "{}",
            Client = FakeClientJwt(),
        };

        LoginHandler.CompleteLogin(context, first, login, identity, offline: true);
        Assert.Equal("Steve", first.Username);
        Assert.Equal(SessionState.PacksSent, first.State);

        LoginHandler.CompleteLogin(context, second, login, identity, offline: true);

        Assert.True(transport.Disconnected);
        Assert.False(sessions.TryGet(first.Connection, out _));
        Assert.Equal(SessionState.PacksSent, second.State);
        Assert.Equal("Steve", second.Username);
    }

    private static string FakeClientJwt()
    {
        static string B64(string json)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        string header = B64("""{"alg":"none"}""");
        string payload = B64("""{"SelfSignedId":"00000000-0000-0000-0000-000000000001","DeviceModel":"test"}""");
        return $"{header}.{payload}.e30";
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
        var context = new ServerContext(config, sessions, sender, new SessionPacketQueue(), new SessionWorkQueue());
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

