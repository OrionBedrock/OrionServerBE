using System.Buffers;
using Orion.Config;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using RakNet;
using RakNet.Packets.Enums;

namespace Orion.Network;

public sealed class PacketSender
{
    private readonly OrionConfig _config;
    private readonly INetworkSend _send;
    private readonly byte[] _frameBuffer;

    public PacketSender(OrionConfig config, INetworkSend? send = null, int frameBufferSize = GamePacketCodec.DefaultScratchSize)
    {
        _config = config;
        _send = send ?? new RakNetNetworkSend();
        _frameBuffer = new byte[frameBufferSize];
    }

    public CompressionMethod ConfiguredCompression
        => GamePacketCodec.ResolveCompressionMethod(_config.Server.Network.CompressionMethod);

    public int CompressionThreshold => _config.Server.Network.CompressionThreshold;

    public void Send(
        NetworkConnection connection,
        DataPacket packet,
        CompressionMethod? compression = null,
        bool immediate = false)
    {
        Send(connection, [packet], compression, immediate);
    }

    public void Send(
        NetworkConnection connection,
        IReadOnlyList<DataPacket> packets,
        CompressionMethod? compression = null,
        bool immediate = false)
    {
        var method = compression ?? ConfiguredCompression;
        var threshold = CompressionThreshold;
        int length = GamePacketCodec.Encode(packets, _frameBuffer, method, threshold);
        _send.Send(connection, _frameBuffer.AsSpan(0, length), Reliability.ReliableOrdered, immediate);
    }

    public void Disconnect(NetworkConnection connection)
        => _send.Disconnect(connection);
}
