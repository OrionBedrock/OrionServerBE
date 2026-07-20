using Orion.Api;
using Orion.Api.Containers;
using Orion.Api.Items;

namespace Orion.Gameplay;

/// <summary>
/// Opt-in player inventory API. Implemented by the VanillaInventory plugin.
/// </summary>
public interface IPlayerInventoryService
{
    bool TryOpenInventory(IPlayer player);

    bool TryCloseInventory(IPlayer player, int windowId);

    bool TryGetAccess(IPlayer player, out IPlayerInventoryAccess? access);

    IItemStack? GetHeldItem(IPlayer player);

    bool TrySetHeldSlot(IPlayer player, int slot);

    bool TryGive(IPlayer player, IItemStack stack, out int leftover);

    bool TryClear(IPlayer player);

    bool TryCollect(IPlayer player, IItemStack stack, out ushort moved);

    bool TrySyncToClient(IPlayer player);

    /// <summary>
    /// Reveal hotbar HUD after the core default-hides it on join.
    /// </summary>
    void EnableHud(IPlayer player);

    IContainer? ResolveContainer(IPlayer player, ContainerNameWire name);

    /// <summary>Process a client ItemStackRequest; returns false if inventory plugin is inactive.</summary>
    bool TryProcessItemStackRequest(
        IPlayer player,
        ItemStackRequestWire request,
        out ItemStackResponseWire response);
}
