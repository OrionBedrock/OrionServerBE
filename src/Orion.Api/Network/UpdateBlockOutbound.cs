using Orion.Api.Math;

namespace Orion.Api.Network;

/// <summary>
/// Protocol-free outbound block update. The host adapts this to the wire
/// <c>UpdateBlock</c> packet when sent via <see cref="IPlayer.Send"/> or
/// <see cref="IDimension.Broadcast"/>.
/// </summary>
public sealed class UpdateBlockOutbound : IOutboundPacket
{
    /// <summary>Block coordinates in the dimension.</summary>
    public required BlockPos Position { get; init; }

    /// <summary>Runtime network block state id (same meaning as Protocol NetworkBlockId).</summary>
    public required int NetworkBlockId { get; init; }

    /// <summary>
    /// Update flags (Protocol <c>UpdateBlockFlagsType</c> numeric values).
    /// Default is Network (2), matching typical client sync usage.
    /// </summary>
    public uint Flags { get; init; } = UpdateBlockNetworkFlags.Network;

    /// <summary>
    /// Target layer (Protocol <c>UpdateBlockLayerType</c> numeric values).
    /// Default is Normal (0).
    /// </summary>
    public uint Layer { get; init; } = UpdateBlockNetworkFlags.LayerNormal;
}

/// <summary>Numeric flag/layer constants aligned with Protocol UpdateBlock enums.</summary>
public static class UpdateBlockNetworkFlags
{
    public const uint None = 0;
    public const uint Neighbors = 1;
    public const uint Network = 2;
    public const uint NoGraphic = 4;
    public const uint Priority = 8;

    public const uint LayerNormal = 0;
    public const uint LayerWaterLogged = 1;
}
