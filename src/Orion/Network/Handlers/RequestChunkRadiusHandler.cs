using Orion.Player;
using Orion.Protocol.Packets;
using Orion.World.Chunk;

namespace Orion.Network.Handlers;

public static class RequestChunkRadiusHandler
{
    public static void Handle(ServerContext context, ConnectionSession session, RequestChunkRadiusPacket packet)
    {
        if (session.Player is not { } player)
        {
            return;
        }

        int serverMax = ResolveServerMaxView(context, player);
        int clientMax = packet.MaxChunkRadius > 0
            ? packet.MaxChunkRadius
            : ChunkViewMath.MaxBedrockViewDistance;

        int maxChebyshev = ChunkViewMath.MaxChebyshevForClientCircle(clientMax);
        int radius = Math.Clamp(packet.ChunkRadius, 1, Math.Min(serverMax, maxChebyshev));
        int circular = ChunkViewMath.SquareToCircle(radius);

        context.Sender.Send(
            session.Connection,
            new UpdateChunkRadiusPacket { ChunkRadius = circular });

        player.SetViewDistanceChebyshev(radius);
    }

    private static int ResolveServerMaxView(ServerContext context, Player.Player player)
    {
        var dims = context.Config.Server.WorldDefaultSettings.Dimensions;
        int configured = dims.Count > 0 ? dims[0].ViewDistance : 8;
        if (string.Equals(player.Dimension.Identifier, dims.Count > 0 ? dims[0].Identifier : null, StringComparison.OrdinalIgnoreCase))
        {
            configured = dims[0].ViewDistance;
        }
        else
        {
            foreach (var dim in dims)
            {
                if (string.Equals(dim.Identifier, player.Dimension.Identifier, StringComparison.OrdinalIgnoreCase))
                {
                    configured = dim.ViewDistance;
                    break;
                }
            }
        }

        return Math.Clamp(configured, 1, ChunkViewMath.MaxBedrockViewDistance);
    }
}
