namespace Orion.PluginContracts.Registry;

public sealed record ItemRegistration(
    string Identifier,
    bool Creative = true,
    int? CreativeCategory = null,
    int MaxStackSize = 64,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyDictionary<string, string>? Components = null,
    string? BlockIdentifier = null);
