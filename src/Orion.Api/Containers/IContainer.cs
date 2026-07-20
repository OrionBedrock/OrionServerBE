using Orion.Api.Items;

namespace Orion.Api.Containers;

/// <summary>Simplified container kind for plugin APIs (not the full Bedrock wire enum).</summary>
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

    /// <summary>Opens this container for <paramref name="player"/> and returns the window id.</summary>
    int Show(Orion.Api.IPlayer player);

    /// <summary>Closes this container for <paramref name="player"/>.</summary>
    void Close(Orion.Api.IPlayer player);

    /// <summary>Removes a viewer; optionally sends close to the client.</summary>
    bool RemoveViewer(Orion.Api.IPlayer player, bool sendClose);

    /// <summary>Current viewers and their window ids.</summary>
    IReadOnlyCollection<KeyValuePair<Orion.Api.IPlayer, int>> GetAllOccupants();
}
