using System.Reflection;
using Orion.Api.Traits;
using Orion.Block.Traits;
using Orion.Entity.Traits;
using Orion.Item.Traits;
using Orion.PluginContracts.Registry;
using Orion.Player;

namespace Orion.Plugins.Registry;

internal sealed class BlockTraitRegistryFacade(ContentRegistriesCore core) : IBlockTraitRegistry
{
    public void RegisterFromAssembly(Assembly assembly, string pluginId) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginContext.Registries.");

    public void Register(Type traitType, string pluginId) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginContext.Registries.");

    internal void RegisterFromAssembly(string pluginId, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        core.ThrowIfTraitsFrozen();
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(BlockTraitBase).IsAssignableFrom(type))
            {
                continue;
            }

            Register(pluginId, type);
        }
    }

    internal void Register(string pluginId, Type traitType)
    {
        ArgumentNullException.ThrowIfNull(traitType);
        core.ThrowIfTraitsFrozen();
        EnsureBlockTrait(traitType);
        _ = pluginId;
        BlockTraitRegistry.Register(traitType);
    }

    static void EnsureBlockTrait(Type traitType)
    {
        if (!typeof(BlockTraitBase).IsAssignableFrom(traitType))
        {
            throw new ArgumentException($"{traitType.FullName} is not a BlockTraitBase.", nameof(traitType));
        }

        if (!typeof(BlockTrait).IsAssignableFrom(traitType))
        {
            throw new ArgumentException(
                $"{traitType.FullName} must subclass Orion.Block.Traits.BlockTrait until extraction completes.",
                nameof(traitType));
        }
    }
}

internal sealed class ItemTraitRegistryFacade(ContentRegistriesCore core) : IItemTraitRegistry
{
    public void RegisterFromAssembly(Assembly assembly, string pluginId) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginContext.Registries.");

    public void Register(Type traitType, string pluginId) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginContext.Registries.");

    internal void RegisterFromAssembly(string pluginId, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        core.ThrowIfTraitsFrozen();
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(ItemTraitBase).IsAssignableFrom(type))
            {
                continue;
            }

            Register(pluginId, type);
        }
    }

    internal void Register(string pluginId, Type traitType)
    {
        ArgumentNullException.ThrowIfNull(traitType);
        core.ThrowIfTraitsFrozen();
        if (!typeof(ItemTraitBase).IsAssignableFrom(traitType))
        {
            throw new ArgumentException($"{traitType.FullName} is not an ItemTraitBase.", nameof(traitType));
        }

        if (!typeof(ItemTrait).IsAssignableFrom(traitType))
        {
            throw new ArgumentException(
                $"{traitType.FullName} must subclass Orion.Item.Traits.ItemTrait until extraction completes.",
                nameof(traitType));
        }

        _ = pluginId;
        ItemTraitRegistry.Register(traitType);
    }
}

internal sealed class EntityTraitRegistryFacade(ContentRegistriesCore core) : IEntityTraitRegistry
{
    public void RegisterFromAssembly(Assembly assembly, string pluginId) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginContext.Registries.");

    public void Register(Type traitType, string pluginId) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginContext.Registries.");

    internal void RegisterFromAssembly(string pluginId, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        core.ThrowIfTraitsFrozen();
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(EntityTraitBase).IsAssignableFrom(type))
            {
                continue;
            }

            // Player-scoped traits go through IPlayerTraitRegistry.
            if (typeof(PlayerTrait).IsAssignableFrom(type) || typeof(PlayerTraitBase).IsAssignableFrom(type))
            {
                continue;
            }

            Register(pluginId, type);
        }
    }

    internal void Register(string pluginId, Type traitType)
    {
        ArgumentNullException.ThrowIfNull(traitType);
        core.ThrowIfTraitsFrozen();
        if (!typeof(EntityTraitBase).IsAssignableFrom(traitType))
        {
            throw new ArgumentException($"{traitType.FullName} is not an EntityTraitBase.", nameof(traitType));
        }

        if (typeof(PlayerTrait).IsAssignableFrom(traitType) || typeof(PlayerTraitBase).IsAssignableFrom(traitType))
        {
            throw new ArgumentException(
                $"{traitType.FullName} is a player trait; use Registries.PlayerTraits.",
                nameof(traitType));
        }

        if (!typeof(EntityTrait).IsAssignableFrom(traitType))
        {
            throw new ArgumentException(
                $"{traitType.FullName} must subclass Orion.Entity.Traits.EntityTrait until extraction completes.",
                nameof(traitType));
        }

        _ = pluginId;
        EntityTraitRegistry.Register(traitType);
    }
}

internal sealed class PlayerTraitRegistryFacade(ContentRegistriesCore core) : IPlayerTraitRegistry
{
    public void RegisterFromAssembly(Assembly assembly, string pluginId) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginContext.Registries.");

    public void Register(Type traitType, string pluginId) =>
        throw new InvalidOperationException("Use plugin-scoped registries from IPluginContext.Registries.");

    internal void RegisterFromAssembly(string pluginId, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        core.ThrowIfTraitsFrozen();
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract)
            {
                continue;
            }

            if (typeof(PlayerTrait).IsAssignableFrom(type) || typeof(PlayerTraitBase).IsAssignableFrom(type))
            {
                Register(pluginId, type);
            }
        }
    }

    internal void Register(string pluginId, Type traitType)
    {
        ArgumentNullException.ThrowIfNull(traitType);
        core.ThrowIfTraitsFrozen();
        bool isPlayerTrait = typeof(PlayerTrait).IsAssignableFrom(traitType)
            || typeof(PlayerTraitBase).IsAssignableFrom(traitType);
        if (!isPlayerTrait)
        {
            throw new ArgumentException($"{traitType.FullName} is not a player trait.", nameof(traitType));
        }

        if (!typeof(EntityTrait).IsAssignableFrom(traitType))
        {
            throw new ArgumentException(
                $"{traitType.FullName} must subclass Orion.Player.PlayerTrait until extraction completes.",
                nameof(traitType));
        }

        _ = pluginId;
        EntityTraitRegistry.Register(traitType);
    }
}
