namespace Orion.Api;

/// <summary>Thin global-region scheduling surface for plugins.</summary>
public interface IGlobalScheduler
{
    /// <summary>Enqueue work for the next (or current) global region tick.</summary>
    void Execute(Action action);
}
