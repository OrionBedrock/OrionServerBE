namespace Orion.PluginContracts.Registry;

public sealed record BlockRegistration(
    string Identifier,
    int DefaultStateHash,
    bool Solid = true,
    bool Air = false,
    float Hardness = 0f,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<BlockStateDefinition>? States = null,
    IReadOnlyDictionary<string, string>? Components = null);
