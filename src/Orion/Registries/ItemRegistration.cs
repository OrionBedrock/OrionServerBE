namespace Orion.Registries;

/// <summary>Registered item id for ItemRegistry packet (no vanilla mechanics).</summary>
public sealed class ItemRegistration
{
    public ItemRegistration(string id, short runtimeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        RuntimeId = runtimeId;
    }

    public string Id { get; }

    public short RuntimeId { get; }
}
