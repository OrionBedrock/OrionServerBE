using System.Buffers.Binary;

namespace Orion.World.Chunk;

/// <summary>
/// Minimal 16x16x16 subchunk placeholder (air). Full palettes arrive with registries (Phase 11).
/// </summary>
public sealed class SubChunk
{
    public const int Size = 16;

    public SubChunk(int localY)
    {
        LocalY = localY;
    }

    public int LocalY { get; }

    public bool IsEmpty { get; set; } = true;
}

/// <summary>
/// Column chunk held in a dimension while simulation tickets keep it loaded.
/// </summary>
public sealed class ChunkColumn
{
    public const int MinSubChunkY = -4;
    public const int MaxSubChunkY = 19;

    private readonly SubChunk?[] _subChunks = new SubChunk?[MaxSubChunkY - MinSubChunkY + 1];

    public ChunkColumn(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }

    public int ChunkX { get; }

    public int ChunkZ { get; }

    public bool IsLoaded { get; set; }

    public bool IsDirty { get; set; }

    public bool IsGenerated { get; set; }

    public SubChunk GetOrCreateSubChunk(int localY)
    {
        if (localY < MinSubChunkY || localY > MaxSubChunkY)
        {
            throw new ArgumentOutOfRangeException(nameof(localY));
        }

        int index = localY - MinSubChunkY;
        return _subChunks[index] ??= new SubChunk(localY);
    }

    public SubChunk? GetSubChunk(int localY)
    {
        if (localY < MinSubChunkY || localY > MaxSubChunkY)
        {
            return null;
        }

        return _subChunks[localY - MinSubChunkY];
    }

    public void MarkDirty() => IsDirty = true;

    public void ClearDirty() => IsDirty = false;

    /// <summary>
    /// Compact serialization for providers (coords + generated flag). Full block data later.
    /// </summary>
    public byte[] EncodeMinimal()
    {
        var buffer = new byte[9];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), ChunkX);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), ChunkZ);
        buffer[8] = (byte)(IsGenerated ? 1 : 0);
        return buffer;
    }

    public static ChunkColumn DecodeMinimal(ReadOnlySpan<byte> data)
    {
        if (data.Length < 9)
        {
            throw new ArgumentException("Chunk payload too short.", nameof(data));
        }

        int x = BinaryPrimitives.ReadInt32LittleEndian(data);
        int z = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        return new ChunkColumn(x, z)
        {
            IsGenerated = data[8] != 0,
            IsLoaded = true,
        };
    }
}
