using Orion.World.Chunk;

namespace Orion.World.Generation;

/// <summary>
/// Default generator: empty air column (no terrain).
/// </summary>
public sealed class VoidGenerator : IChunkGenerator
{
    public const string Id = "void";

    public string Identifier => Id;

    public void Generate(ChunkColumn chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        // Void: no subchunks allocated; column is air.
        chunk.IsGenerated = true;
        chunk.MarkDirty();
    }
}
