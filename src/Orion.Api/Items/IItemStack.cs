namespace Orion.Api.Items;

public interface IItemType
{
    string Identifier { get; }
    int NetworkId { get; }
    int MaxStackSize { get; }
}

public interface IItemStack
{
    IItemType Type { get; }
    int Count { get; }
    uint Metadata { get; }
    int NetworkStackId { get; }

    void SetCount(int count);
    void Increment(int amount = 1);
    void Decrement(int amount = 1);
    bool CanStackWith(IItemStack other);
    IItemStack Clone(int? count = null);
}

/// <summary>Host-registered factory for creating item stacks without referencing Orion.dll.</summary>
public interface IItemStackFactory
{
    IItemStack Create(string identifier, int count = 1, uint metadata = 0);
    IItemStack? TryCreate(string identifier, int count = 1, uint metadata = 0);

    /// <summary>Resolves a creative-menu item by its network id (server-authoritative inventory).</summary>
    IItemStack? TryCreateCreative(uint creativeNetworkId);
}

/// <summary>
/// Stable entry point for plugins. Host wires <see cref="IItemStackFactory"/> at boot
/// via <see cref="SetFactory"/>.
/// </summary>
public static class Items
{
    private static IItemStackFactory? _factory;

    public static void SetFactory(IItemStackFactory factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public static IItemStack Create(string identifier, int count = 1, uint metadata = 0)
    {
        IItemStackFactory factory = _factory
            ?? throw new InvalidOperationException("Items factory is not registered. Host must call Items.SetFactory at boot.");
        return factory.Create(identifier, count, metadata);
    }

    public static IItemStack? TryCreate(string identifier, int count = 1, uint metadata = 0) =>
        _factory?.TryCreate(identifier, count, metadata);

    public static IItemStack? TryCreateCreative(uint creativeNetworkId) =>
        _factory?.TryCreateCreative(creativeNetworkId);
}
