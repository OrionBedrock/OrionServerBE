using System.Collections.Concurrent;
using Orion.Entity.Traits;
using Orion.Network;
using Orion.Region;
using Orion.World;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.Player;

/// <summary>
/// Session-bound player: entity + connection. Streaming trait arrives in later Phase 09 commits.
/// </summary>
public sealed class Player
{
    private readonly ConcurrentQueue<Action> _regionMailbox = new();
    private int _viewDistanceChebyshev = 8;
    private Action<int>? _viewDistanceApplied;

    public Player(
        EntityHandle entity,
        ConnectionSession session,
        Regionizer regionizer,
        float spawnX,
        float spawnY,
        float spawnZ)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Regionizer = regionizer ?? throw new ArgumentNullException(nameof(regionizer));
        UniqueId = entity.EntityId;
        RuntimeId = unchecked((ulong)entity.EntityId);
        SpawnX = spawnX;
        SpawnY = spawnY;
        SpawnZ = spawnZ;
    }

    public EntityHandle Entity { get; }

    public ConnectionSession Session { get; }

    public Regionizer Regionizer { get; }

    public long UniqueId { get; }

    public ulong RuntimeId { get; }

    public float SpawnX { get; }

    public float SpawnY { get; }

    public float SpawnZ { get; }

    public Dimension Dimension => Entity.Dimension;

    public int ChunkX => Entity.ChunkX;

    public int ChunkZ => Entity.ChunkZ;

    public int ViewDistanceChebyshev => Volatile.Read(ref _viewDistanceChebyshev);

    public bool IsRemoved => Entity.IsRemoved;

    /// <summary>
    /// Folia: enqueue work for the owning region tick (not RakNet I/O).
    /// </summary>
    public void EnqueueOnRegion(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (IsRemoved)
        {
            return;
        }

        _regionMailbox.Enqueue(work);
    }

    public void SetViewDistanceChebyshev(int radius)
    {
        if (radius < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        Volatile.Write(ref _viewDistanceChebyshev, radius);
        _viewDistanceApplied?.Invoke(radius);
    }

    internal void BindViewDistanceListener(Action<int> listener)
        => _viewDistanceApplied = listener ?? throw new ArgumentNullException(nameof(listener));

    /// <summary>
    /// Drain mailbox + region scheduler under temporary ownership.
    /// </summary>
    public void TickRegion()
    {
        if (IsRemoved)
        {
            return;
        }

        ChunkRegion? region = Regionizer.GetRegionAt(ChunkX, ChunkZ);
        if (region is null || !region.IsAlive)
        {
            while (_regionMailbox.TryDequeue(out Action? orphan))
            {
                orphan();
            }

            return;
        }

        using IDisposable? ownership = region.TryMarkTickingWithOwnership();
        if (ownership is null)
        {
            return;
        }

        try
        {
            while (_regionMailbox.TryDequeue(out Action? work))
            {
                work();
            }

            region.DrainSchedulerTasks();
        }
        finally
        {
            region.MarkNotTicking();
        }
    }

    public void EnableSimulationTickets(int radiusChunks)
    {
        ChunkLoadingTrait trait = Entity.Traits.GetOrAdd(e => new ChunkLoadingTrait(e));
        trait.Enable(Math.Max(0, radiusChunks));
    }

    public void Remove()
    {
        Entity.Remove();
        while (_regionMailbox.TryDequeue(out _))
        {
        }
    }
}
