using Orion.Api;
using Orion.Api.Items;

namespace Orion.Gameplay;

/// <summary>
/// Optional gameplay hook for held-item use (eat, drink, …). Registered by plugins via
/// the service registry.
/// </summary>
public interface IPlayerItemUseHandler
{
    /// <summary>Returns false when the held item cannot start a use action.</summary>
    bool TryBeginUse(IPlayer player, IItemStack heldItem, out ulong durationTicks);

    /// <summary>Applies effects when a pending use finishes. Return true if the action succeeded.</summary>
    bool TryCompleteUse(IPlayer player, IItemStack heldItem, int slot);
}
