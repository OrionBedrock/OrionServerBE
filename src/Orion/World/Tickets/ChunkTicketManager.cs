using Orion.Region;
using Orion.World.Chunk;

namespace Orion.World.Tickets;

/// <summary>
/// Holds a simulation load on a chunk. Dispose releases one refcount.
/// </summary>
public sealed class SimulationTicket : IDisposable
{
    private readonly ChunkTicketManager _manager;
    private bool _disposed;

    internal SimulationTicket(ChunkTicketManager manager, string dimensionId, int chunkX, int chunkZ)
    {
        _manager = manager;
        DimensionId = dimensionId;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }

    public string DimensionId { get; }

    public int ChunkX { get; }

    public int ChunkZ { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _manager.Release(this);
    }
}

/// <summary>
/// Refcounts simulation tickets and mirrors loaded chunks into the regionizer.
/// </summary>
public sealed class ChunkTicketManager
{
    private readonly Regionizer _regionizer;
    private readonly object _sync = new();
    private readonly Dictionary<(string Dim, int X, int Z), int> _refs = new();
    private readonly Dictionary<(string Dim, int X, int Z), ChunkColumn> _loaded = new();

    public ChunkTicketManager(Regionizer regionizer)
    {
        _regionizer = regionizer ?? throw new ArgumentNullException(nameof(regionizer));
    }

    public int LoadedChunkCount
    {
        get
        {
            lock (_sync)
            {
                return _loaded.Count;
            }
        }
    }

    public ChunkColumn? GetLoadedChunk(string dimensionId, int chunkX, int chunkZ)
    {
        lock (_sync)
        {
            return _loaded.TryGetValue((dimensionId, chunkX, chunkZ), out ChunkColumn? chunk) ? chunk : null;
        }
    }

    public SimulationTicket Acquire(
        string dimensionId,
        int chunkX,
        int chunkZ,
        Func<string, int, int, ChunkColumn> loadOrCreate)
    {
        ArgumentNullException.ThrowIfNull(loadOrCreate);
        lock (_sync)
        {
            var key = (dimensionId, chunkX, chunkZ);
            if (!_refs.TryGetValue(key, out int count) || count == 0)
            {
                ChunkColumn chunk = loadOrCreate(dimensionId, chunkX, chunkZ);
                chunk.IsLoaded = true;
                _loaded[key] = chunk;
                _refs[key] = 1;
                _regionizer.AddChunk(chunkX, chunkZ);
            }
            else
            {
                _refs[key] = count + 1;
            }

            return new SimulationTicket(this, dimensionId, chunkX, chunkZ);
        }
    }

    internal void Release(SimulationTicket ticket)
    {
        lock (_sync)
        {
            var key = (ticket.DimensionId, ticket.ChunkX, ticket.ChunkZ);
            if (!_refs.TryGetValue(key, out int count))
            {
                return;
            }

            count--;
            if (count > 0)
            {
                _refs[key] = count;
                return;
            }

            _refs.Remove(key);
            if (_loaded.Remove(key, out ChunkColumn? chunk))
            {
                chunk.IsLoaded = false;
            }

            _regionizer.RemoveChunk(ticket.ChunkX, ticket.ChunkZ);
        }
    }

    public void PutGenerated(string dimensionId, ChunkColumn chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        lock (_sync)
        {
            var key = (dimensionId, chunk.ChunkX, chunk.ChunkZ);
            if (!_refs.ContainsKey(key))
            {
                return;
            }

            chunk.IsLoaded = true;
            chunk.IsGenerated = true;
            _loaded[key] = chunk;
        }
    }
}
