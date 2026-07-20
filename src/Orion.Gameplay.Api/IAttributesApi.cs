using Orion.Api;

namespace Orion.Gameplay;

/// <summary>
/// Facade for vitals (health + hunger). Resolve via <c>provides: orion:attributes</c> /
/// <c>context.Services.TryGet&lt;IAttributesApi&gt;(...)</c>.
/// </summary>
public interface IAttributesApi
{
    IEntityHealthService Health { get; }
    IPlayerHungerService Hunger { get; }

    /// <summary>
    /// Reveal health/hunger HUD after the core default-hides vitals on join.
    /// </summary>
    void EnableHud(IPlayer player);
}
