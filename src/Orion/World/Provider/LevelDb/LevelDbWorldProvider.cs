using LevelDB;
using Orion.World.Chunk;

namespace Orion.World.Provider.LevelDb;

/// <summary>
/// LevelDB-backed world provider. Tick never fsyncs — writes run on IoPersistence.
/// </summary>
public sealed class LevelDbWorldProvider : IWorldProvider
{
    private readonly DB _database;
    private readonly object _sync = new();
    private bool _disposed;

    public LevelDbWorldProvider(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(DatabasePath);
        var options = new Options { CreateIfMissing = true };
        _database = new DB(options, DatabasePath);
    }

    public string DatabasePath { get; }

    public bool HasChunk(string dimensionId, int chunkX, int chunkZ)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte[] key = LevelDbChunkKeys.BuildChunkKey(dimensionId, chunkX, chunkZ);
        lock (_sync)
        {
            byte[]? value = _database.Get(key);
            return value is { Length: > 0 };
        }
    }

    public ChunkColumn? LoadChunk(string dimensionId, int chunkX, int chunkZ)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte[] key = LevelDbChunkKeys.BuildChunkKey(dimensionId, chunkX, chunkZ);
        byte[]? payload;
        lock (_sync)
        {
            payload = _database.Get(key);
        }

        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        return ChunkColumn.DecodeMinimal(payload);
    }

    public void SaveChunk(string dimensionId, ChunkColumn chunk)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(chunk);
        byte[] key = LevelDbChunkKeys.BuildChunkKey(dimensionId, chunk.ChunkX, chunk.ChunkZ);
        byte[] payload = chunk.EncodeMinimal();
        lock (_sync)
        {
            _database.Put(key, payload);
        }

        chunk.ClearDirty();
    }

    public void DeleteChunk(string dimensionId, int chunkX, int chunkZ)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte[] key = LevelDbChunkKeys.BuildChunkKey(dimensionId, chunkX, chunkZ);
        lock (_sync)
        {
            _database.Delete(key);
        }
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // LevelDB.Standard has no explicit Flush API; Put is durable enough for Phase 07.
        // Close/reopen is reserved for shutdown via Dispose.
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            _database.Dispose();
        }
    }
}
