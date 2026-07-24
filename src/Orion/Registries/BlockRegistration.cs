namespace Orion.Registries;

/// <summary>Registered block id for network/palette (no vanilla mechanics).</summary>
public sealed class BlockRegistration
{
    public BlockRegistration(string id, int networkId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        NetworkId = networkId;
    }

    public string Id { get; }

    public int NetworkId { get; }
}
