using Orion.Api.Items;
using Orion.Item;

namespace Orion.Item;

internal sealed class ItemStackFactory : IItemStackFactory
{
    public IItemStack Create(string identifier, int count = 1, uint metadata = 0)
    {
        ItemType type = ItemType.Get(identifier)
            ?? throw new InvalidOperationException($"Unknown item type '{identifier}'.");
        return new ItemStack(type, (ushort)Math.Clamp(count, 0, ushort.MaxValue), metadata);
    }

    public IItemStack? TryCreate(string identifier, int count = 1, uint metadata = 0)
    {
        ItemType? type = ItemType.Get(identifier);
        return type is null
            ? null
            : new ItemStack(type, (ushort)Math.Clamp(count, 0, ushort.MaxValue), metadata);
    }

    public IItemStack? TryCreateCreative(uint creativeNetworkId) =>
        ItemRegistry.GetCreativeItem(creativeNetworkId);
}
