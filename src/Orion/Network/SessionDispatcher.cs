using Orion.Config;
using Orion.Network.Handlers;
using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Packets;
using RakNet;

namespace Orion.Network;

/// <summary>
/// Holds session services for packet handlers.
/// </summary>
public sealed class ServerContext
{
    public ServerContext(
        OrionConfig config,
        SessionManager sessions,
        PacketSender sender,
        SessionPacketQueue queue,
        SessionWorkQueue work)
    {
        Config = config;
        Sessions = sessions;
        Sender = sender;
        Queue = queue;
        Work = work;
    }

    public OrionConfig Config { get; }
    public SessionManager Sessions { get; }
    public PacketSender Sender { get; }
    public SessionPacketQueue Queue { get; }
    public SessionWorkQueue Work { get; }
}

/// <summary>
/// Drains the session packet/work queues on the tick loop and dispatches login-related packets.
/// Folia check: precursor to global/region scheduling (Phase 04).
/// </summary>
public sealed class SessionDispatcher
{
    private readonly ServerContext _context;
    private readonly byte[] _scratch = new byte[GamePacketCodec.DefaultScratchSize];
    private readonly List<DataPacket> _decoded = new(16);

    public SessionDispatcher(ServerContext context)
    {
        _context = context;
    }

    public void Drain()
    {
        while (_context.Queue.TryDequeue(out NetworkConnection connection, out byte[] payload))
        {
            if (!_context.Sessions.TryGet(connection, out ConnectionSession? session))
            {
                continue;
            }

            _decoded.Clear();
            if (!GamePacketCodec.TryDecode(payload, _scratch, _decoded))
            {
                continue;
            }

            foreach (DataPacket packet in _decoded)
            {
                Dispatch(session, packet);
            }
        }

        _context.Work.Drain();
    }

    private void Dispatch(ConnectionSession session, DataPacket packet)
    {
        PacketId id = Packet.GetId(packet);
        switch (id)
        {
            case PacketId.RequestNetworkSettings:
                RequestNetworkSettingsHandler.Handle(_context, session, (RequestNetworkSettingsPacket)packet);
                break;

            case PacketId.Login:
                LoginHandler.Handle(_context, session, (LoginPacket)packet);
                break;

            case PacketId.ResourcePackClientResponse:
                ResourcePackClientResponseHandler.Handle(_context, session, (ResourcePackClientResponsePacket)packet);
                break;
        }
    }
}
