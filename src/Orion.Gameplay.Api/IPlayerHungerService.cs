using Orion.Api;

namespace Orion.Gameplay;

/// <summary>
/// Opt-in player hunger API. Implemented by the VanillaAttributes plugin.
/// </summary>
public interface IPlayerHungerService
{
    bool TryEat(
        IPlayer player,
        int nutrition,
        float saturationModifier,
        bool canAlwaysEat);

    bool TryAddExhaustion(IPlayer player, float amount);

    bool TryGet(
        IPlayer player,
        out float hunger,
        out float saturation,
        out float exhaustion);

    bool TrySetHunger(
        IPlayer player,
        float hunger,
        float? saturation = null);
}
