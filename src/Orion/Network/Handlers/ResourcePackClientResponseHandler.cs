using Orion.Config;
using Orion.Protocol.Enums;
using Orion.Protocol.Io;
using Orion.Protocol.Packets;
using Orion.Player;
using Orion.Player.Traits;

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
                if (session.State is SessionState.Authenticated or SessionState.PacksSent)
                {
                    session.State = SessionState.HandshakeComplete;
                }

                if (context.Players is null || context.World is null || context.Regionizer is null)
                {
                    return;
                }

                if (session.Player is not null)
                {
                    return;
                }

                SpawnPlayer(context, session);
                return;

            default:
                return;
        }
    }

    private static void SpawnPlayer(ServerContext context, ConnectionSession session)
    {
        var dims = context.Config.Server.WorldDefaultSettings.Dimensions;
        DimensionConfig dimConfig = dims.Count > 0 ? dims[0] : new DimensionConfig();

        Player.Player player = context.Players!.Create(
            session,
            context.World!,
            context.Regionizer!,
            dimConfig,
            context.Permissions);

        PlayerSpawnPipeline.SendSpawnSequence(context, player);

        PacketSendGate.Bind(context.Sender);
        PlayerChunkStreamingTrait streaming = player.Entity.Traits.GetOrAdd(_ => new PlayerChunkStreamingTrait(player));
        streaming.Start();
    }
}
