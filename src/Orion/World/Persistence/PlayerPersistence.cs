using System.Collections.Concurrent;
using Orion.Runtime;
using Orion.World.Provider;
using Orion.World.Provider.LevelDb;

namespace Orion.World.Persistence;

/// <summary>
/// Dirty player KV blobs → QueueIoPersistence write (never on tick).
/// </summary>
public sealed class PlayerPersistence : IDisposable
{
    private readonly IWorldProvider _provider;
    private readonly OrionThreadPools? _pools;
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);
    private readonly ManualResetEventSlim _idle = new(true);
    private int _inflight;
    private bool _disposed;

    public PlayerPersistence(IWorldProvider provider, OrionThreadPools? pools = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _pools = pools;
    }

    public int PendingCount => _pending.Count;

    public void ScheduleSave(string xuid, Orion.Player.PlayerDataStore store)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(xuid);
        ArgumentNullException.ThrowIfNull(store);

        if (!store.IsDirty)
        {
            return;
        }

        byte[] payload = store.TakeSnapshot();
        store.ClearDirty();
        _pending[xuid] = 0;
        _idle.Reset();
        Interlocked.Increment(ref _inflight);

        void Work()
        {
            try
            {
                _provider.SavePlayerBlob(xuid, payload);
                _pending.TryRemove(xuid, out _);
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
            throw new TimeoutException("Timed out waiting for player persistence IO to drain.");
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
