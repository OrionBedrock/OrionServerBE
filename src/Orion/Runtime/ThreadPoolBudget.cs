using Orion.Config;

namespace Orion.Runtime;

/// <summary>
/// Resolves configured thread limits with Folia-like headroom (~20% cores reserved).
/// </summary>
public sealed class ThreadPoolBudget
{
    public ThreadPoolBudget(
        int processorCount,
        int regionTick,
        int raknet,
        int chunkIo,
        int chunkWorkers,
        int ioPersistence,
        int asyncScheduler,
        int reservedHeadroom)
    {
        ProcessorCount = processorCount;
        RegionTick = regionTick;
        Raknet = raknet;
        ChunkIo = chunkIo;
        ChunkWorkers = chunkWorkers;
        IoPersistence = ioPersistence;
        AsyncScheduler = asyncScheduler;
        ReservedHeadroom = reservedHeadroom;
    }

    public int ProcessorCount { get; }
    public int RegionTick { get; }
    public int Raknet { get; }
    public int ChunkIo { get; }
    public int ChunkWorkers { get; }
    public int IoPersistence { get; }
    public int AsyncScheduler { get; }
    public int ReservedHeadroom { get; }

    public static ThreadPoolBudget Resolve(ThreadPoolsConfig config, int? processorCount = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        int cores = processorCount is > 0 ? processorCount.Value : Math.Max(1, Environment.ProcessorCount);
        int reservedHeadroom = Math.Max(1, (int)Math.Ceiling(cores * 0.20));

        int raknet = ResolveFixed(config.Raknet.Max, fallback: 2);
        int chunkIo = ResolveFixed(config.ChunkIo.Max, fallback: 2);
        int chunkWorkers = ResolveFixed(config.ChunkWorkers.Max, fallback: 2);
        int ioPersistence = ResolveFixed(config.IoPersistence.Max, fallback: 2);
        int asyncScheduler = ResolveFixed(config.AsyncScheduler.Max, fallback: 2);

        int regionTick = config.RegionTick.Max;
        if (regionTick <= 0)
        {
            int committed = raknet + chunkIo + chunkWorkers + ioPersistence + asyncScheduler + reservedHeadroom;
            regionTick = Math.Max(1, cores - committed);
        }
        else
        {
            regionTick = Math.Max(1, regionTick);
        }

        return new ThreadPoolBudget(
            cores,
            regionTick,
            raknet,
            chunkIo,
            chunkWorkers,
            ioPersistence,
            asyncScheduler,
            reservedHeadroom);
    }

    private static int ResolveFixed(int configured, int fallback)
        => configured <= 0 ? Math.Max(1, fallback) : configured;
}
