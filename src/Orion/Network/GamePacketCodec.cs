using System.Buffers;
using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Packets;
using BinaryReader = Basalt.Binary.BinaryReader;
using BinaryWriter = Basalt.Binary.BinaryWriter;

namespace Orion.Network;

/// <summary>
/// Thin codec over Orion.Protocol framing and packet serialization.
/// Folia check: decode on the I/O thread only produces DTOs; enqueue to a region happens in Phase 03.
/// </summary>
public static class GamePacketCodec
{
    public const int DefaultScratchSize = 2 * 1024 * 1024;

    public static CompressionMethod ResolveCompressionMethod(int configured) => configured switch
    {
        1 => CompressionMethod.Snappy,
        2 => CompressionMethod.NotPresent,
        0xFF => CompressionMethod.None,
        _ => CompressionMethod.Zlib,
    };

    /// <summary>
    /// Unframes a 0xFE payload, splits the batch, and deserializes each game packet (header masked with 0x3FF).
    /// </summary>
    public static bool TryDecode(
        ReadOnlySpan<byte> framedPayload,
        Span<byte> scratch,
        ICollection<DataPacket> destination)
    {
        int length = Packet.Unframe(framedPayload, scratch, out _);
        if (length <= 0)
        {
            return false;
        }

        ReadOnlySpan<byte> batch = scratch[..length];
        int offset = 0;
        BinaryReader frameReader = new(batch, ref offset);
        int decoded = 0;

        while (frameReader.Remaining > 0)
        {
            int packetLength = checked((int)frameReader.ReadVarUInt());
            if (packetLength <= 0 || packetLength > frameReader.Remaining)
            {
                break;
            }

            ReadOnlySpan<byte> packetBuffer = frameReader.ReadBytes(packetLength);
            if (packetBuffer.Length == 0)
            {
                continue;
            }

            destination.Add(DecodePacket(packetBuffer));
            decoded++;
        }

        return decoded > 0;
    }

    public static DataPacket DecodePacket(ReadOnlySpan<byte> packetBuffer)
    {
        int offset = 0;
        BinaryReader packetReader = new(packetBuffer, ref offset);
        return Packet.Deserialize(packetReader);
    }

    /// <summary>
    /// Serializes one packet into a length-prefixed batch and frames it with the configured compression.
    /// </summary>
    public static int Encode(
        DataPacket packet,
        Span<byte> destination,
        CompressionMethod compression,
        int compressionThreshold)
    {
        byte[] packetBuffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
        byte[] batchBuffer = ArrayPool<byte>.Shared.Rent(256 * 1024);

        try
        {
            int packetOffset = 0;
            BinaryWriter packetWriter = new(packetBuffer, ref packetOffset);
            Packet.Serialize(packet, packetWriter);
            ReadOnlySpan<byte> packetBytes = packetWriter.GetProcessedBytes();

            int batchOffset = 0;
            BinaryWriter batchWriter = new(batchBuffer, ref batchOffset);
            batchWriter.WriteVarUInt((uint)packetBytes.Length);
            batchWriter.WriteBytes(packetBytes);

            return Packet.Frame(batchWriter.GetProcessedBytes(), destination, compression, compressionThreshold);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(packetBuffer);
            ArrayPool<byte>.Shared.Return(batchBuffer);
        }
    }

    public static int Encode(
        DataPacket packet,
        Span<byte> destination,
        int compressionMethod,
        int compressionThreshold)
        => Encode(packet, destination, ResolveCompressionMethod(compressionMethod), compressionThreshold);
}
