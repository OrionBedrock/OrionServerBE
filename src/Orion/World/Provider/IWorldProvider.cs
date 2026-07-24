using Orion.World.Chunk;

namespace Orion.World.Provider;

public interface IWorldProvider : IDisposable
{
    bool HasChunk(string dimensionId, int chunkX, int chunkZ);

    ChunkColumn? LoadChunk(string dimensionId, int chunkX, int chunkZ);

    void SaveChunk(string dimensionId, ChunkColumn chunk);

    void DeleteChunk(string dimensionId, int chunkX, int chunkZ);
}
