using Orion.Config;
using Orion.Network.Handlers;
using Orion.Player;
using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Packets;
using Orion.Region;
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
        SessionWorkQueue work,
        PlayerManager? players = null,
        Orion.World.World? world = null,
        Regionizer? regionizer = null)
    {
        Config = config;
        Sessions = sessions;
        Sender = sender;
        Queue = queue;
        Work = work;
        Players = players;
        World = world;
        Regionizer = regionizer;
    }

    public OrionConfig Config { get; }
    public SessionManager Sessions { get; }
    public PacketSender Sender { get; }
    public SessionPacketQueue Queue { get; }
    public SessionWorkQueue Work { get; }
    public PlayerManager? Players { get; set; }
    public Orion.World.World? World { get; set; }
    public Regionizer? Regionizer { get; set; }
}

/// <summary>
/// Drains session queues. Pre-game packets dispatch on the global tick;
/// in-game packets enqueue to the player's region mailbox (Folia).
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
                if (session.State >= SessionState.InGame && session.Player is { } player)
                {
                    DataPacket captured = packet;
                    player.EnqueueOnRegion(() => Dispatch(session, captured));
                }
                else
                {
                    Dispatch(session, packet);
                }
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

            case PacketId.RequestChunkRadius:
                RequestChunkRadiusHandler.Handle(_context, session, (RequestChunkRadiusPacket)packet);
                break;
        }
    }
}
