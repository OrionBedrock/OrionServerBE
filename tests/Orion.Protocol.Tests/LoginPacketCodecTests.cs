using Orion.Network;
using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Packets;
using Xunit;
using BinaryReader = Basalt.Binary.BinaryReader;
using BinaryWriter = Basalt.Binary.BinaryWriter;

namespace Orion.Protocol.Tests;

public sealed class LoginPacketCodecTests
{
    [Fact]
    public void RequestNetworkSettings_RoundTrips()
    {
        var original = new RequestNetworkSettingsPacket(Io.Constants.ProtocolVersion);
        var decoded = RoundTrip(original);

        Assert.Equal(original.Protocol, decoded.Protocol);
    }

    [Fact]
    public void NetworkSettings_RoundTrips()
    {
        var original = new NetworkSettingsPacket
        {
            CompressionThreshold = 1,
            CompressionMethod = CompressionMethod.Zlib,
            ClientThrottle = false,
            ClientThrottleThreshold = 0,
            ClientThrottleScalar = 0f,
        };

        var decoded = RoundTrip(original);

        Assert.Equal(original.CompressionThreshold, decoded.CompressionThreshold);
        Assert.Equal(original.CompressionMethod, decoded.CompressionMethod);
        Assert.Equal(original.ClientThrottle, decoded.ClientThrottle);
        Assert.Equal(original.ClientThrottleThreshold, decoded.ClientThrottleThreshold);
        Assert.Equal(original.ClientThrottleScalar, decoded.ClientThrottleScalar);
    }

    [Fact]
    public void Login_Body_RoundTrips()
    {
        var original = new LoginPacket
        {
            Protocol = Io.Constants.ProtocolVersion,
            Identity = "{\"chain\":[]}",
            Client = "{\"SkinId\":\"test\"}",
        };

        var decoded = RoundTrip(original);

        Assert.Equal(original.Protocol, decoded.Protocol);
        Assert.Equal(original.Identity, decoded.Identity);
        Assert.Equal(original.Client, decoded.Client);
    }

    [Fact]
    public void PlayStatus_RoundTrips()
    {
        var original = new PlayStatusPacket(PlayStatus.LoginSuccess);
        var decoded = RoundTrip(original);

        Assert.Equal(original.Status, decoded.Status);
    }

    [Fact]
    public void Disconnect_RoundTrips()
    {
        var original = new DisconnectPacket
        {
            Reason = DisconnectReason.Disconnected,
            HideDisconnectionScreen = false,
            Message = "bye",
            FilteredMessage = "bye",
        };

        var decoded = RoundTrip(original);

        Assert.Equal(original.Reason, decoded.Reason);
        Assert.Equal(original.HideDisconnectionScreen, decoded.HideDisconnectionScreen);
        Assert.Equal(original.Message, decoded.Message);
        Assert.Equal(original.FilteredMessage, decoded.FilteredMessage);
    }

    [Fact]
    public void Decode_MasksSenderSubclientBits()
    {
        var original = new PlayStatusPacket(PlayStatus.LoginSuccess);
        Span<byte> buffer = stackalloc byte[64];
        int offset = 0;
        BinaryWriter writer = new(buffer, ref offset);
        writer.WriteVarUInt((uint)PacketId.PlayStatus | 0xC00);
        original.Serialize(writer);

        var decoded = Assert.IsType<PlayStatusPacket>(GamePacketCodec.DecodePacket(buffer[..offset]));
        Assert.Equal(PlayStatus.LoginSuccess, decoded.Status);
    }

    [Fact]
    public void Frame_Unframe_Deserialize_RoundTrips()
    {
        var original = new RequestNetworkSettingsPacket(Io.Constants.ProtocolVersion);
        Span<byte> framed = stackalloc byte[256];
        int framedLength = GamePacketCodec.Encode(
            original,
            framed,
            CompressionMethod.Zlib,
            compressionThreshold: 1);

        Assert.True(framedLength > 1);
        Assert.Equal(0xFE, framed[0]);

        Span<byte> scratch = stackalloc byte[1024];
        var packets = new List<DataPacket>();
        Assert.True(GamePacketCodec.TryDecode(framed[..framedLength], scratch, packets));
        Assert.Single(packets);

        var decoded = Assert.IsType<RequestNetworkSettingsPacket>(packets[0]);
        Assert.Equal(original.Protocol, decoded.Protocol);
    }

    private static T RoundTrip<T>(T packet) where T : DataPacket
    {
        Span<byte> buffer = stackalloc byte[4096];
        int offset = 0;
        BinaryWriter writer = new(buffer, ref offset);
        Packet.Serialize(packet, writer);

        int readOffset = 0;
        BinaryReader reader = new(buffer[..offset], ref readOffset);
        return Assert.IsType<T>(Packet.Deserialize(reader));
    }
}
