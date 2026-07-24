using BinaryWriter = Basalt.Binary.BinaryWriter;

namespace Orion.World.Chunk;

/// <summary>
/// Minimal Bedrock network payload for an empty (void/air) chunk column.
/// </summary>
public static class VoidLevelChunkEncoder
{
    private static readonly byte[] CachedPayload = BuildPayload();

    /// <summary>
    /// SubChunkCount for a void load. Matches Basalt empty-column send count (0).
    /// Unload also uses 0 but with an empty payload.
    /// </summary>
    public const uint VoidSubChunkCount = 0;

    public static byte[] EncodePayload() => CachedPayload;

    public static byte[] EncodeUnloadPayload() => [];

    private static byte[] BuildPayload()
    {
        // Empty column network payload: border/heightmap marker only (Basalt Serialize path when send count is 0).
        byte[] buffer = new byte[16];
        int offset = 0;
        var writer = new BinaryWriter(buffer, ref offset);
        writer.WriteUInt8(0);
        return buffer.AsSpan(0, offset).ToArray();
    }
}
