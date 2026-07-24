using System.Collections.Concurrent;

namespace Orion.Network;

/// <summary>
/// Continuations for async session work (e.g. online JWT verify).
/// Folia check: precursor to global/region schedule — run only from the tick drain.
/// </summary>
public sealed class SessionWorkQueue
{
    private readonly ConcurrentQueue<Action> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(Action work) => _queue.Enqueue(work);

    public void Drain()
    {
        while (_queue.TryDequeue(out Action? work))
        {
            work();
        }
    }
}
