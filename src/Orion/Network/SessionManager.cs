using System.Collections.Concurrent;
using RakNet;

namespace Orion.Network;

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<NetworkConnection, ConnectionSession> _sessions = new();

    public ConnectionSession Create(NetworkConnection connection)
    {
        var session = new ConnectionSession(connection);
        _sessions[connection] = session;
        return session;
    }

    public bool TryGet(NetworkConnection connection, out ConnectionSession session)
        => _sessions.TryGetValue(connection, out session!);

    public bool Remove(NetworkConnection connection)
        => _sessions.TryRemove(connection, out _);

    public IEnumerable<ConnectionSession> All => _sessions.Values;

    public ConnectionSession? FindByUsername(string username)
    {
        foreach (var session in _sessions.Values)
        {
            if (session.Username is not null
                && string.Equals(session.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                return session;
            }
        }

        return null;
    }

    public ConnectionSession? FindByXuid(string xuid)
    {
        if (string.IsNullOrWhiteSpace(xuid))
        {
            return null;
        }

        foreach (var session in _sessions.Values)
        {
            if (session.Xuid is not null
                && string.Equals(session.Xuid, xuid, StringComparison.Ordinal))
            {
                return session;
            }
        }

        return null;
    }
}
