using Orion.PluginContracts.Registry;

namespace Orion.Plugins.Registry;

internal sealed class BlockRegistryFacade(ContentRegistriesCore core) : IBlockRegistry
{
    public void Register(BlockRegistration registration) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginLoadContext.Registries.");

    internal void Register(string pluginId, BlockRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.Identifier);
        core.ThrowIfBlocksFrozen();

        if (!core.TryClaimIdentifier(pluginId, registration.Identifier))
        {
            return;
        }

        Block.BlockRegistry.RegisterPluginBlock(
            registration.Identifier,
            registration.DefaultStateHash,
            solid: registration.Solid,
            air: registration.Air,
            hardness: registration.Hardness,
            tags: registration.Tags,
            states: registration.States,
            components: registration.Components);
    }
}
