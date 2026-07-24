using Orion.Region;
using Orion.World;
using Orion.World.Chunk;

namespace Orion.Pathfinding;

/// <summary>
/// Walkable iff the block's chunk is loaded in the dimension tickets and owned by the current region.
/// </summary>
public sealed class LoadedOwnedWalkabilityProbe : IWalkabilityProbe
{
    private readonly Dimension _dimension;
    private readonly Regionizer _regionizer;

    public LoadedOwnedWalkabilityProbe(Dimension dimension, Regionizer regionizer)
    {
        _dimension = dimension ?? throw new ArgumentNullException(nameof(dimension));
        _regionizer = regionizer ?? throw new ArgumentNullException(nameof(regionizer));
    }

    public bool IsWalkable(string dimensionId, int blockX, int blockY, int blockZ)
    {
        _ = blockY;
        if (!string.Equals(dimensionId, _dimension.Identifier, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int chunkX = blockX >> 4;
        int chunkZ = blockZ >> 4;
        ChunkColumn? loaded = _dimension.Tickets.GetLoadedChunk(dimensionId, chunkX, chunkZ);
        if (loaded is null)
        {
            return false;
        }

        return RegionOwnership.IsOwnedByCurrentRegion(_regionizer, chunkX, chunkZ);
    }
}
