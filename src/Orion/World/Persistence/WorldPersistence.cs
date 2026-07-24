using System.Collections.Concurrent;
using Orion.Runtime;
using Orion.World.Chunk;
using Orion.World.Provider;
using Orion.World.Provider.LevelDb;

namespace Orion.World.Persistence;

/// <summary>
/// Dirty chunks → encode snapshot → QueueIoPersistence write (never on tick).
/// </summary>
public sealed class WorldPersistence : IDisposable
{
    private readonly IWorldProvider _provider;
    private readonly OrionThreadPools? _pools;
    private readonly ConcurrentDictionary<(string Dim, int X, int Z), byte> _pending = new();
    private readonly ManualResetEventSlim _idle = new(true);
    private int _inflight;
    private bool _disposed;

    public WorldPersistence(IWorldProvider provider, OrionThreadPools? pools = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _pools = pools;
    }

    public int PendingCount => _pending.Count;

    public void ScheduleSave(string dimensionId, ChunkColumn chunk)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentNullException.ThrowIfNull(chunk);

        if (!chunk.IsDirty && !chunk.IsGenerated)
        {
            return;
        }

        var key = (dimensionId, chunk.ChunkX, chunk.ChunkZ);
        byte[] payload = chunk.EncodeMinimal();
        _pending[key] = 0;
        _idle.Reset();
        Interlocked.Increment(ref _inflight);

        void Work()
        {
            try
            {
                var snapshot = ChunkColumn.DecodeMinimal(payload);
                _provider.SaveChunk(dimensionId, snapshot);
                chunk.ClearDirty();
                _pending.TryRemove(key, out _);
            }
            finally
            {
                if (Interlocked.Decrement(ref _inflight) == 0)
                {
                    _idle.Set();
                }
            }
        }

        if (_pools is not null)
        {
            _pools.QueueIoPersistence(Work);
        }
        else
        {
            Work();
        }
    }

    public void Flush(TimeSpan? timeout = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_idle.Wait(timeout ?? TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Timed out waiting for world persistence IO to drain.");
        }

        if (_provider is LevelDbWorldProvider levelDb)
        {
            levelDb.Flush();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Flush(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Best-effort flush on dispose.
        }

        _idle.Dispose();
    }
}
