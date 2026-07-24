using Orion.World.Chunk;

namespace Orion.World.Generation;

public interface IChunkGenerator
{
    string Identifier { get; }

    void Generate(ChunkColumn chunk);
}
