namespace Orion.Api;

/// <summary>
/// Plugin entry point loaded into an isolated McMaster assembly load context.
/// Shared types live in <c>Orion.Api</c> so the host and plugin share the same type identity.
/// </summary>
public abstract class Plugin
{
    public IOrionServer Server { get; internal set; } = null!;

    public string Name { get; internal set; } = "";

    public string AssemblyPath { get; internal set; } = "";

    public virtual void OnLoad()
    {
    }

    public virtual void OnStart()
    {
    }

    public virtual void OnDisable()
    {
    }
}
