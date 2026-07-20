using Orion.Api;
using Orion.Api.Containers;
using Orion.Api.Items;

namespace Orion.Gameplay;

/// <summary>
/// Resolved player inventory handle for core and plugins (implemented by VanillaInventory).
/// </summary>
public interface IPlayerInventoryAccess
{
    IContainer Container { get; }

    int SelectedSlot { get; }

    void SetHeldSlot(int slot);

    IItemStack? GetHeldItem();

    void Clear();

    void SyncToPlayer(IPlayer player);

    void SyncHeldItemToClient(IPlayer player);
}
