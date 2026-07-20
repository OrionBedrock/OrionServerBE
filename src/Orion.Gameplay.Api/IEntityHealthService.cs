using Orion.Api;

namespace Orion.Gameplay;

/// <summary>
/// Opt-in entity health API. Implemented by the VanillaAttributes plugin.
/// </summary>
public interface IEntityHealthService
{
    bool TryApplyDamage(
        IEntity entity,
        float amount,
        IEntity? damager = null,
        int? damageCause = null);

    bool TryHeal(IEntity entity, float amount);

    bool TryGet(IEntity entity, out float current, out float maximum);

    bool TrySet(IEntity entity, float current);
}
