using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Packets;

namespace Orion.Network.Handlers;

public static class RequestNetworkSettingsHandler
{
    public static void Handle(ServerContext context, ConnectionSession session, RequestNetworkSettingsPacket packet)
    {
        if (packet.Protocol != Constants.ProtocolVersion)
        {
            DisconnectReason reason = packet.Protocol < Constants.ProtocolVersion
                ? DisconnectReason.OutdatedClient
                : DisconnectReason.OutdatedServer;

            context.Sender.Send(
                session.Connection,
                new DisconnectPacket
                {
                    Reason = reason,
                    HideDisconnectionScreen = true,
                    Message = string.Empty,
                    FilteredMessage = string.Empty,
                },
                CompressionMethod.NotPresent);

            context.Sender.Disconnect(session.Connection);
            return;
        }

        var response = new NetworkSettingsPacket
        {
            CompressionThreshold = (ushort)Math.Clamp(context.Config.Server.Network.CompressionThreshold, 0, ushort.MaxValue),
            CompressionMethod = context.Sender.ConfiguredCompression,
            ClientThrottle = false,
            ClientThrottleThreshold = 0,
            ClientThrottleScalar = 0f,
        };

        context.Sender.Send(session.Connection, response, CompressionMethod.NotPresent);
        session.State = SessionState.NetworkReady;
    }
}
