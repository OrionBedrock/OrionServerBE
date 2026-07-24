using Orion.World.Chunk;

namespace Orion.World.Provider;

public interface IWorldProvider : IDisposable
{
    bool HasChunk(string dimensionId, int chunkX, int chunkZ);

    ChunkColumn? LoadChunk(string dimensionId, int chunkX, int chunkZ);

    void SaveChunk(string dimensionId, ChunkColumn chunk);

    void DeleteChunk(string dimensionId, int chunkX, int chunkZ);

    bool TryLoadPlayerBlob(string xuid, out byte[]? blob);

    void SavePlayerBlob(string xuid, ReadOnlySpan<byte> blob);

    void DeletePlayerBlob(string xuid);
}
