using Orion.Api.Math;

namespace Orion.Api.Network;

/// <summary>
/// Protocol-free helpers for common block-related outbound packets.
/// Prefer these over crafting Protocol packets when a facade exists.
/// </summary>
public static class BlockNetwork
{
    /// <summary>
    /// Creates an outbound block update for <see cref="IPlayer.Send"/> /
    /// <see cref="IDimension.Broadcast"/>. Does not mutate the world — pair with
    /// <see cref="IDimension.SetBlock"/> / <see cref="IDimension.SetPermutation"/> when needed.
    /// </summary>
    /// <param name="position">Block coordinates.</param>
    /// <param name="networkBlockId">Runtime network block state id.</param>
    /// <param name="flags">Update flags; default Network.</param>
    /// <param name="layer">Block layer; default Normal.</param>
    public static UpdateBlockOutbound CreateUpdateBlock(
        BlockPos position,
        int networkBlockId,
        uint flags = UpdateBlockNetworkFlags.Network,
        uint layer = UpdateBlockNetworkFlags.LayerNormal) =>
        new()
        {
            Position = position,
            NetworkBlockId = networkBlockId,
            Flags = flags,
            Layer = layer
        };
}
