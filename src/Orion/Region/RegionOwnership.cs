namespace Orion.Region;

/// <summary>
/// Folia/TickThread-style ownership checks for chunk regions.
/// </summary>
public static class RegionOwnership
{
    [ThreadStatic]
    private static ChunkRegion? t_currentRegion;

    public static ChunkRegion? CurrentRegion => t_currentRegion;

    public static IDisposable Enter(ChunkRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        ChunkRegion? previous = t_currentRegion;
        t_currentRegion = region;
        return new OwnershipScope(previous);
    }

    public static bool IsOwnedByCurrentRegion(Regionizer regionizer, int chunkX, int chunkZ)
    {
        ArgumentNullException.ThrowIfNull(regionizer);
        ChunkRegion? current = t_currentRegion;
        if (current is null || !current.IsAlive)
        {
            return false;
        }

        ChunkRegion? at = regionizer.GetRegionAt(chunkX, chunkZ);
        return ReferenceEquals(current, at);
    }

    public static void EnsureOwnedByCurrentRegion(Regionizer regionizer, int chunkX, int chunkZ, string? message = null)
    {
        if (!IsOwnedByCurrentRegion(regionizer, chunkX, chunkZ))
        {
            throw new InvalidOperationException(
                message ?? $"Chunk ({chunkX},{chunkZ}) is not owned by the current region thread.");
        }
    }

    public static bool IsGlobalOrOwned(Regionizer regionizer, GlobalRegion global, int chunkX, int chunkZ)
    {
        ArgumentNullException.ThrowIfNull(global);
        if (global.IsCurrentThread)
        {
            return true;
        }

        return IsOwnedByCurrentRegion(regionizer, chunkX, chunkZ);
    }

    private sealed class OwnershipScope(ChunkRegion? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            t_currentRegion = previous;
        }
    }
}
