using RakNet;

namespace Orion.Network;

public enum SessionState
{
    Connected,
    NetworkReady,
    Authenticating,
    Authenticated,
    PacksSent,
    HandshakeComplete,
    InGame,
}

/// <summary>
/// Per-connection Bedrock session state.
/// Folia check: mutated only from the session drain / region tick path, not from RakNet I/O callbacks.
/// </summary>
public sealed class ConnectionSession
{
    public ConnectionSession(NetworkConnection connection)
    {
        Connection = connection;
    }

    public NetworkConnection Connection { get; }

    public SessionState State { get; set; } = SessionState.Connected;

    public string? Username { get; set; }

    public string? Xuid { get; set; }

    public Guid Uuid { get; set; }

    public string? IdentityPublicKey { get; set; }

    public bool OfflineAuth { get; set; }

    public Orion.Player.Player? Player { get; set; }
}
