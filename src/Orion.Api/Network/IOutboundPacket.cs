namespace Orion.Api.Network;

/// <summary>
/// Marker for packets plugins can send via <see cref="IPlayer.Send"/>.
/// Protocol adapters land in SDK step S6.
/// </summary>
public interface IOutboundPacket;
