using System.Buffers.Binary;
using System.Text;

namespace Orion.World.Provider.LevelDb;

internal static class LevelDbChunkKeys
{
    // Orion Phase 07 minimal keys (not vanilla Bedrock layout yet).
    private const byte TagOrionChunk = 0x4F; // 'O'

    public static byte[] BuildChunkKey(string dimensionId, int chunkX, int chunkZ)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        byte[] dimBytes = Encoding.UTF8.GetBytes(dimensionId);
        if (dimBytes.Length > byte.MaxValue)
        {
            throw new ArgumentException("Dimension id is too long for LevelDB key.", nameof(dimensionId));
        }

        byte[] key = new byte[1 + 1 + dimBytes.Length + 8];
        key[0] = TagOrionChunk;
        key[1] = (byte)dimBytes.Length;
        dimBytes.CopyTo(key.AsSpan(2));
        int offset = 2 + dimBytes.Length;
        BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(offset), chunkX);
        BinaryPrimitives.WriteInt32LittleEndian(key.AsSpan(offset + 4), chunkZ);
        return key;
    }
}
