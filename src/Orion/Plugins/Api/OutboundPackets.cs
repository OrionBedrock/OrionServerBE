using Orion.Api.Network;
using Orion.Plugins.Api;
using Orion.Protocol.Packets;

namespace Orion.Api.Network;

/// <summary>
/// Escape hatch: wrap a Protocol <see cref="DataPacket"/> for
/// <see cref="IPlayer.Send"/> / <see cref="IDimension.Broadcast"/>.
/// Prefer <see cref="BlockNetwork"/> helpers when available.
/// </summary>
/// <remarks>
/// Lives in the host assembly under the stable <c>Orion.Api.Network</c> namespace
/// so plugins that already reference Protocol can call it without a second API surface.
/// </remarks>
public static class OutboundPackets
{
    public static IOutboundPacket FromProtocol(DataPacket packet) =>
        new ProtocolOutboundPacket(packet);
}
