namespace Orion.Api.Network;

/// <summary>
/// Marker for packets plugins can send via <see cref="IPlayer.Send"/> or
/// <see cref="IDimension.Broadcast"/>. Prefer Api helpers (e.g.
/// <see cref="BlockNetwork.CreateUpdateBlock"/>); wrap Protocol packets with
/// host <c>OutboundPackets.FromProtocol</c> when needed.
/// </summary>
public interface IOutboundPacket;
