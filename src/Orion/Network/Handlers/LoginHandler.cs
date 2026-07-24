using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Login;
using Orion.Protocol.Login.Data;
using Orion.Protocol.Packets;

namespace Orion.Network.Handlers;

public static class LoginHandler
{
    public static void Handle(ServerContext context, ConnectionSession session, LoginPacket packet)
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

        LoginEnvelope envelope = LoginEnvelope.Parse(packet.Identity);
        bool offlineLogin = OfflineIdentity.IsOfflineLogin(envelope)
            || envelope.AuthenticationType == 2;

        if (offlineLogin)
        {
            if (!AuthPolicy.AllowsOffline(context.Config.Server.Orion))
            {
                Reject(context, session, "Offline mode is not supported. Please connect to Xbox services.");
                return;
            }

            try
            {
                VerifiedIdentity identity = OfflineIdentity.VerifyOffline(envelope, packet.Client);
                CompleteLogin(context, session, packet, identity, offline: true);
            }
            catch (Exception exception)
            {
                Reject(context, session, "Authentication failed.", exception.Message);
            }

            return;
        }

        // Online path (JWT) is wired in the follow-up session commit.
        Reject(context, session, "Online authentication is not available yet.");
    }

    internal static void CompleteLogin(
        ServerContext context,
        ConnectionSession session,
        LoginPacket packet,
        VerifiedIdentity identity,
        bool offline)
    {
        ClientData clientData = LoginPayload.Parse(packet.Client);
        bool onlineMode = context.Config.Server.Orion.OnlineMode;

        Guid uuid = AuthPolicy.ResolvePlayerUuid(
            identity.Uuid, clientData.SelfSignedId, identity.Username, onlineMode);
        string xuid = AuthPolicy.ResolvePlayerXuid(identity.Xuid, uuid, onlineMode);

        session.Username = identity.Username;
        session.Xuid = xuid;
        session.Uuid = uuid;
        session.IdentityPublicKey = identity.IdentityPublicKey;
        session.OfflineAuth = offline;
        session.State = SessionState.Authenticated;

        var status = new PlayStatusPacket(PlayStatus.LoginSuccess);
        var resources = new ResourcePacksInfoPacket
        {
            MustAccept = false,
            HasAddons = false,
            HasScripts = false,
            ForceDisableVibrantVisuals = false,
            WorldTemplateUuid = Guid.Empty,
            WorldTemplateVersion = string.Empty,
            Packs = [],
        };

        context.Sender.Send(session.Connection, [status, resources]);
        session.State = SessionState.PacksSent;
    }

    internal static void Reject(
        ServerContext context,
        ConnectionSession session,
        string message,
        string? detail = null)
    {
        _ = detail;
        context.Sender.Send(
            session.Connection,
            new DisconnectPacket
            {
                Reason = DisconnectReason.Disconnected,
                HideDisconnectionScreen = false,
                Message = message,
                FilteredMessage = message,
            },
            CompressionMethod.NotPresent);

        context.Sender.Disconnect(session.Connection);
    }
}
