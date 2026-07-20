namespace Orion.PluginContracts.Registry;

/// <summary>Named block state key for rich <see cref="BlockRegistration"/>.</summary>
public sealed record BlockStateDefinition(string Name, string DefaultValue = "0");
