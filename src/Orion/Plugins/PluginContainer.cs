using McMaster.NETCore.Plugins;
using Orion.Api;

namespace Orion.Plugins;

public sealed class PluginContainer
{
    public required Plugin Plugin { get; init; }
    public required string Name { get; init; }
    public required string AssemblyPath { get; init; }
    public required PluginLoader Loader { get; init; }
    public PluginState State { get; set; }
    public string? FailureReason { get; set; }
}
