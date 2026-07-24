using System.Collections.Concurrent;
using RakNet;

namespace Orion.Network;

/// <summary>
/// Incoming game payloads copied off the RakNet I/O thread.
/// Folia check: precursor to scheduling onto global/region (Phase 04); drain only on the tick loop.
/// </summary>
public sealed class SessionPacketQueue
{
    private readonly ConcurrentQueue<(NetworkConnection Connection, byte[] Payload)> _queue = new();

    public void Enqueue(NetworkConnection connection, ReadOnlyMemory<byte> payload)
    {
        _queue.Enqueue((connection, payload.ToArray()));
    }

    public bool TryDequeue(out NetworkConnection connection, out byte[] payload)
    {
        if (_queue.TryDequeue(out var item))
        {
            connection = item.Connection;
            payload = item.Payload;
            return true;
        }

        connection = null!;
        payload = null!;
        return false;
    }

    public int Count => _queue.Count;
}
