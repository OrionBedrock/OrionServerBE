using System.Collections.Concurrent;
using Orion.Region;
using Orion.Runtime;
using Orion.Scheduler;
using Orion.World.Chunk;
using Orion.World.Tickets;

namespace Orion.World.Generation;

/// <summary>
/// Miss on provider → ChunkWorkers generate → apply on owning region via RegionScheduler.
/// </summary>
public sealed class ChunkLoadPipeline
{
    private readonly Regionizer _regionizer;
    private readonly RegionScheduler _regionScheduler;
    private readonly OrionThreadPools? _pools;
    private readonly GeneratorRegistry _registry;
    private readonly ConcurrentDictionary<(string Dim, int X, int Z), byte> _inflight = new();

    public ChunkLoadPipeline(
        Regionizer regionizer,
        RegionScheduler regionScheduler,
        GeneratorRegistry registry,
        OrionThreadPools? pools = null)
    {
        _regionizer = regionizer ?? throw new ArgumentNullException(nameof(regionizer));
        _regionScheduler = regionScheduler ?? throw new ArgumentNullException(nameof(regionScheduler));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _pools = pools;
    }

    public GeneratorRegistry Registry => _registry;

    /// <summary>
    /// Queue generation when the loaded column is not yet generated.
    /// </summary>
    public void RequestGenerate(
        string worldId,
        string dimensionId,
        string generatorId,
        ChunkTicketManager tickets,
        ChunkColumn placeholder)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(placeholder);

        if (placeholder.IsGenerated)
        {
            return;
        }

        var key = (dimensionId, placeholder.ChunkX, placeholder.ChunkZ);
        if (!_inflight.TryAdd(key, 0))
        {
            return;
        }

        int chunkX = placeholder.ChunkX;
        int chunkZ = placeholder.ChunkZ;
        IChunkGenerator generator = _registry.Get(
            string.IsNullOrWhiteSpace(generatorId) ? VoidGenerator.Id : generatorId);

        void Work()
        {
            try
            {
                var generated = new ChunkColumn(chunkX, chunkZ);
                generator.Generate(generated);

                _regionScheduler.Execute(worldId, chunkX, chunkZ, () =>
                {
                    tickets.PutGenerated(dimensionId, generated);
                    _inflight.TryRemove(key, out _);
                });

                // Phase 07 bridge: drain apply under temporary region ownership until
                // dedicated chunk-region tick loops land with ChunkLoadingTrait.
                PumpRegionApply(chunkX, chunkZ);
            }
            catch
            {
                _inflight.TryRemove(key, out _);
                throw;
            }
        }

        if (_pools is not null)
        {
            _pools.QueueChunkWorker(Work);
        }
        else
        {
            Work();
        }
    }

    public void PumpRegionApply(int chunkX, int chunkZ)
    {
        ChunkRegion? region = _regionizer.GetRegionAt(chunkX, chunkZ);
        if (region is null || !region.IsAlive)
        {
            return;
        }

        using IDisposable? ownership = region.TryMarkTickingWithOwnership();
        if (ownership is null)
        {
            return;
        }

        try
        {
            region.DrainSchedulerTasks();
        }
        finally
        {
            region.MarkNotTicking();
        }
    }

    public bool IsInflight(string dimensionId, int chunkX, int chunkZ)
        => _inflight.ContainsKey((dimensionId, chunkX, chunkZ));
}
