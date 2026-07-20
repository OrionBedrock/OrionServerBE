using Orion.PluginContracts.Events;

namespace Orion.PluginContracts.Network;

public sealed class PacketReceiveContext : ICancellable
{
    public required IPlayerConnection Connection { get; init; }

    /// <summary>Resolved player for this connection when available (host fills before dispatch).</summary>
    public object? Player { get; init; }

    public required int PacketId { get; init; }

    public required ReadOnlyMemory<byte> Payload { get; init; }

    public bool Cancelled { get; private set; }

    public void Cancel() => Cancelled = true;

    /// <summary>If set by an owning handler, core switch skips the default handler.</summary>
    public bool Handled { get; set; }

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;

    /// <summary>Typed helper for plugins targeting Orion.Api.IPlayer.</summary>
    public TPlayer? GetPlayer<TPlayer>() where TPlayer : class => Player as TPlayer;
}
