using System.Collections.Concurrent;
using Orion.World.Chunk;

namespace Orion.World.Provider;

public sealed class InMemoryWorldProvider : IWorldProvider
{
    private readonly ConcurrentDictionary<(string Dim, int X, int Z), byte[]> _store = new();
    private readonly ConcurrentDictionary<string, byte[]> _players = new(StringComparer.Ordinal);

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

    public bool TryLoadPlayerBlob(string xuid, out byte[]? blob)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xuid);
        if (_players.TryGetValue(xuid, out byte[]? payload) && payload.Length > 0)
        {
            blob = payload;
            return true;
        }

        blob = null;
        return false;
    }

    public void SavePlayerBlob(string xuid, ReadOnlySpan<byte> blob)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xuid);
        _players[xuid] = blob.ToArray();
    }

    public void DeletePlayerBlob(string xuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xuid);
        _players.TryRemove(xuid, out _);
    }

    public void Dispose()
    {
        _store.Clear();
        _players.Clear();
    }
}
