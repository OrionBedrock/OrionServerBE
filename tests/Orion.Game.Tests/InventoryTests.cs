extern alias PluginContainers;

using Orion.Item;
using Orion.Api.Items;
using OrionInventory;
using PluginContainer = PluginContainers::Orion.Containers.Container;
using PluginContainerType = PluginContainers::Orion.Containers.ContainerType;

namespace Orion.Game.Tests;

public sealed class InventoryTests
{
    [Fact]
    public void EntityInventory_AddItem_StoresRegisteredBlockItem()
    {
        ItemRegistry.EnsureLoaded();
        IItemStack? dirt = Items.TryCreate("minecraft:dirt", 16);
        Assert.NotNull(dirt);

        Orion.Entity.Entity entity = new("minecraft:zombie");
        EntityInventoryTrait inventory = new(entity);

        bool added = inventory.Container.AddItem(dirt!);
        Assert.True(added);
        Assert.Equal(16, inventory.Container.GetItem(0)!.Count);
    }

    [Fact]
    public void Container_AddItem_StacksMatchingItemsBeforeUsingNewSlot()
    {
        ItemRegistry.EnsureLoaded();
        IItemStack? a = Items.TryCreate("minecraft:dirt", 32);
        IItemStack? b = Items.TryCreate("minecraft:dirt", 16);
        Assert.NotNull(a);
        Assert.NotNull(b);

        PluginContainer container = new(PluginContainerType.Inventory, 36);
        Assert.True(container.AddItem(a!));
        Assert.True(container.AddItem(b!));

        Assert.Equal(48, container.GetItem(0)!.Count);
        Assert.Null(container.GetItem(1));
    }
}
