using System.Collections.Concurrent;
using Orion.Config;

namespace Orion.Runtime;

/// <summary>
/// Named worker pools from Runtime.Threads. Chunk/async pools are reserved for later phases.
/// </summary>
public sealed class OrionThreadPools : IDisposable
{
    private readonly NamedWorkerPool _regionTick;
    private readonly NamedWorkerPool _raknet;
    private readonly NamedWorkerPool _chunkIo;
    private readonly NamedWorkerPool _chunkWorkers;
    private readonly NamedWorkerPool _ioPersistence;
    private readonly NamedWorkerPool _asyncScheduler;
    private bool _disposed;

    public OrionThreadPools(ThreadPoolBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);
        Budget = budget;

        _regionTick = new NamedWorkerPool("Orion-RegionTick", budget.RegionTick);
        _raknet = new NamedWorkerPool("Orion-Raknet", budget.Raknet);
        _chunkIo = new NamedWorkerPool("Orion-ChunkIo", budget.ChunkIo);
        _chunkWorkers = new NamedWorkerPool("Orion-ChunkWorkers", budget.ChunkWorkers);
        _ioPersistence = new NamedWorkerPool("Orion-IoPersistence", budget.IoPersistence);
        _asyncScheduler = new NamedWorkerPool("Orion-AsyncScheduler", budget.AsyncScheduler);
    }

    public ThreadPoolBudget Budget { get; }

    public void QueueRegionTick(Action work) => _regionTick.Queue(work);

    public void QueueRaknet(Action work) => _raknet.Queue(work);

    public void QueueChunkIo(Action work) => _chunkIo.Queue(work);

    public void QueueChunkWorker(Action work) => _chunkWorkers.Queue(work);

    public void QueueIoPersistence(Action work) => _ioPersistence.Queue(work);

    public void QueueAsyncScheduler(Action work) => _asyncScheduler.Queue(work);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _regionTick.Dispose();
        _raknet.Dispose();
        _chunkIo.Dispose();
        _chunkWorkers.Dispose();
        _ioPersistence.Dispose();
        _asyncScheduler.Dispose();
    }
}

internal sealed class NamedWorkerPool : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread[] _workers;
    private bool _disposed;

    public NamedWorkerPool(string name, int workerCount)
    {
        if (workerCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount));
        }

        _workers = new Thread[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            int index = i;
            var thread = new Thread(() => WorkerLoop())
            {
                IsBackground = true,
                Name = $"{name}-{index}",
            };
            _workers[i] = thread;
            thread.Start();
        }
    }

    public void Queue(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _queue.Add(work);
    }

    private void WorkerLoop()
    {
        try
        {
            foreach (Action work in _queue.GetConsumingEnumerable())
            {
                try
                {
                    work();
                }
                catch
                {
                    // Keep workers alive; logging arrives later.
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.CompleteAdding();
        foreach (Thread worker in _workers)
        {
            worker.Join(TimeSpan.FromSeconds(2));
        }

        _queue.Dispose();
    }
}
