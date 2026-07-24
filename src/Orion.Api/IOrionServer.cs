namespace Orion.Api;

/// <summary>
/// Host surface exposed to plugins. The owning region owns mutable world state;
/// sharing mutable cross-region state is the plugin author's responsibility.
/// </summary>
public interface IOrionServer
{
    /// <summary>Configured server display name.</summary>
    string Name { get; }
}
