namespace Orion.Api.Network;

/// <summary>
/// Wraps a host/Protocol wire packet for <see cref="IPlayer.Send"/> without referencing Orion.dll.
/// Prefer typed helpers (e.g. <see cref="BlockNetwork"/>) when available.
/// </summary>
public sealed class OpaqueOutboundPacket : IOutboundPacket
{
    public OpaqueOutboundPacket(object wirePacket) =>
        WirePacket = wirePacket ?? throw new ArgumentNullException(nameof(wirePacket));

    public object WirePacket { get; }
}
