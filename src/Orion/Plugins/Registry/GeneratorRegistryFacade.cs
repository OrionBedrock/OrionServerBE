using Orion.Api.Worldgen;
using Orion.PluginContracts.Registry;
using Orion.World.Generation;

namespace Orion.Plugins.Registry;

internal sealed class GeneratorRegistryFacade(ContentRegistriesCore core) : IGeneratorRegistry
{
    public void Register(string name, Type generatorType) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginLoadContext.Registries.");

    internal void Register(string pluginId, string name, Type generatorType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(generatorType);
        core.ThrowIfGeneratorsFrozen();

        if (!typeof(WorldGeneratorBase).IsAssignableFrom(generatorType) || generatorType.IsAbstract)
        {
            throw new ArgumentException(
                $"Generator type '{generatorType.FullName}' must be a concrete subclass of {nameof(WorldGeneratorBase)}.",
                nameof(generatorType));
        }

        if (!core.TryClaimGenerator(pluginId, name))
        {
            return;
        }

        GeneratorFactory.Register(name, generatorType);
    }
}
