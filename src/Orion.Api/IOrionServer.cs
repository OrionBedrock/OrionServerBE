namespace Orion.Api;

/// <summary>
/// Host surface exposed to plugins. The owning region owns mutable world state;
/// sharing mutable cross-region state is the plugin author's responsibility.
/// </summary>
public interface IOrionServer
{
    /// <summary>Configured server display name.</summary>
    string Name { get; }

    /// <summary>
    /// Global region scheduler (runs on the global tick). Null before the host finishes start.
    /// </summary>
    IGlobalScheduler? GlobalScheduler { get; }

    /// <summary>
    /// Region scheduler: work is enqueued on the region that owns the target chunk.
    /// Null before the host finishes start.
    /// </summary>
    IRegionScheduler? RegionScheduler { get; }

    /// <summary>
    /// Core content registries after bootstrap. Null before the host finishes start.
    /// </summary>
    IContentRegistries? Registries { get; }
}
