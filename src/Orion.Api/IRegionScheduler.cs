namespace Orion.Api;

/// <summary>
/// Thin region scheduling surface for plugins.
/// The owning region owns mutable world state; sharing mutable cross-region state
/// is the plugin author's responsibility.
/// </summary>
public interface IRegionScheduler
{
    /// <summary>Enqueue work on the region that owns <paramref name="chunkX"/>/<paramref name="chunkZ"/>.</summary>
    void Execute(string worldId, int chunkX, int chunkZ, Action action);
}
