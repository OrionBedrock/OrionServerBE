using Orion.Api.Items;

namespace Orion.Api.Containers;

public enum ContainerType
{
    Inventory = 0,
    Container = 1,
    Hotbar = 2,
    Cursor = 3
}

public interface IContainer
{
    ContainerType Type { get; }
    int? Identifier { get; set; }
    int GetSize();
    IItemStack? GetItem(int slot);
    void SetItem(int slot, IItemStack item);
    bool AddItem(IItemStack item);
    void ClearSlot(int slot);
    void Clear();
    void Update();
    void UpdateSlot(int slot);
}
