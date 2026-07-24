using System.Collections.Concurrent;
using Orion.World.Chunk;

namespace Orion.World.Provider;

public sealed class InMemoryWorldProvider : IWorldProvider
{
    private readonly ConcurrentDictionary<(string Dim, int X, int Z), byte[]> _store = new();

    public bool HasChunk(string dimensionId, int chunkX, int chunkZ)
        => _store.ContainsKey((dimensionId, chunkX, chunkZ));

    public ChunkColumn? LoadChunk(string dimensionId, int chunkX, int chunkZ)
    {
        if (!_store.TryGetValue((dimensionId, chunkX, chunkZ), out byte[]? payload))
        {
            return null;
        }

        return ChunkColumn.DecodeMinimal(payload);
    }

    public void SaveChunk(string dimensionId, ChunkColumn chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        _store[(dimensionId, chunk.ChunkX, chunk.ChunkZ)] = chunk.EncodeMinimal();
        chunk.ClearDirty();
    }

    public void DeleteChunk(string dimensionId, int chunkX, int chunkZ)
        => _store.TryRemove((dimensionId, chunkX, chunkZ), out _);

    public void Dispose()
    {
        _store.Clear();
    }
}
