using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Packets;

namespace Orion.Network.Handlers;

public static class ResourcePackClientResponseHandler
{
    public static void Handle(ServerContext context, ConnectionSession session, ResourcePackClientResponsePacket packet)
    {
        switch (packet.Response)
        {
            case ResourcePackResponse.Refused:
                context.Sender.Send(
                    session.Connection,
                    new DisconnectPacket
                    {
                        Reason = DisconnectReason.ResourcePackProblem,
                        HideDisconnectionScreen = false,
                        Message = "Required resource packs were refused.",
                        FilteredMessage = "Required resource packs were refused.",
                    });
                context.Sender.Disconnect(session.Connection);
                return;

            case ResourcePackResponse.SendPacks:
                // No packs are advertised in Phase 03.
                return;

            case ResourcePackResponse.AllPacksDownloaded:
                var stack = new ResourcePackStackPacket
                {
                    MustAccept = false,
                    Packs = [],
                    BaseGameVersion = Constants.MinecraftVersion,
                    Experiments = [],
                    ExperimentsPreviouslyToggled = false,
                    IncludeEditorPacks = false,
                };
                context.Sender.Send(session.Connection, stack);
                return;

            case ResourcePackResponse.Completed:
                // StartGame / spawn is Phase 09. Session handshake ends here.
                if (session.State is SessionState.Authenticated or SessionState.PacksSent)
                {
                    session.State = SessionState.HandshakeComplete;
                }
                return;

            default:
                return;
        }
    }
}
