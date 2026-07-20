using Orion.Config;
using Orion.PluginContracts.Diagnostics;
using Orion.PluginContracts.Registry;
using Orion.Plugins.Diagnostics;
using Orion.Protocol.Registry;
using Orion.World.Generation;
using Log = Orion.Logger.Logger;

namespace Orion.Plugins.Registry;

/// <summary>Host-owned content registries shared by all plugins (per-plugin views claim ownership).</summary>
public sealed class ContentRegistriesCore
{
    readonly object _sync = new();
    readonly Dictionary<string, string> _identifierOwners = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> _commandOwners = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _generatorOwners = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _registeredItems = new(StringComparer.Ordinal);

    PluginDiagnostics? _diagnostics;

    bool _commandsFrozen;
    bool _itemsFrozen;
    bool _blocksFrozen;
    bool _creativeFrozen;
    bool _generatorsFrozen;
    bool _traitsFrozen;

    public ContentRegistriesCore()
    {
        Items = new ItemRegistryFacade(this);
        Blocks = new BlockRegistryFacade(this);
        CreativeTabs = new CreativeTabRegistryFacade(this);
        Generators = new GeneratorRegistryFacade(this);
        Commands = new CommandRegistryFacade(this);
        BlockTraits = new BlockTraitRegistryFacade(this);
        ItemTraits = new ItemTraitRegistryFacade(this);
        EntityTraits = new EntityTraitRegistryFacade(this);
        PlayerTraits = new PlayerTraitRegistryFacade(this);
    }

    public void SetDiagnostics(PluginDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;
    }

    public IItemRegistry Items { get; }
    public IBlockRegistry Blocks { get; }
    public ICreativeTabRegistry CreativeTabs { get; }
    public IGeneratorRegistry Generators { get; }
    public ICommandRegistry Commands { get; }
    public IBlockTraitRegistry BlockTraits { get; }
    public IItemTraitRegistry ItemTraits { get; }
    public IEntityTraitRegistry EntityTraits { get; }
    public IPlayerTraitRegistry PlayerTraits { get; }

    public IContentRegistries ForPlugin(string pluginId) =>
        new PluginScopedContentRegistries(this, pluginId);

    public void BindCommands(Orion.Commands.CommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(commandRegistry);
        lock (_sync)
        {
            ((CommandRegistryFacade)Commands).Bind(commandRegistry);
        }
    }

    public void FreezeItemsAndCreative()
    {
        lock (_sync)
        {
            _itemsFrozen = true;
            _creativeFrozen = true;
        }
    }

    public void FreezeBlocks()
    {
        lock (_sync)
        {
            _blocksFrozen = true;
            _traitsFrozen = true;
        }
    }

    public void FreezeGenerators()
    {
        lock (_sync)
        {
            _generatorsFrozen = true;
            GeneratorFactory.Freeze();
        }
    }

    public void FreezeCommands()
    {
        lock (_sync)
        {
            _commandsFrozen = true;
        }
    }

    public void ResetForTests()
    {
        lock (_sync)
        {
            _identifierOwners.Clear();
            _commandOwners.Clear();
            _generatorOwners.Clear();
            _registeredItems.Clear();
            _itemsFrozen = false;
            _blocksFrozen = false;
            _creativeFrozen = false;
            _generatorsFrozen = false;
            _commandsFrozen = false;
            _traitsFrozen = false;
            ((CommandRegistryFacade)Commands).ResetForTests();
        }

        GeneratorFactory.ResetForTests();
    }

    internal bool TryClaimIdentifier(string pluginId, string identifier)
    {
        string? existingOwner;
        lock (_sync)
        {
            if (_identifierOwners.TryGetValue(identifier, out string? owner))
            {
                if (string.Equals(owner, pluginId, StringComparison.Ordinal))
                {
                    return true;
                }

                existingOwner = owner;
            }
            else
            {
                _identifierOwners[identifier] = pluginId;
                return true;
            }
        }

        RejectOwnership(
            "registry.item",
            identifier,
            existingOwner,
            pluginId,
            $"Registry ownership: plugin '{pluginId}' cannot claim '{identifier}' (owned by '{existingOwner}').");
        return false;
    }

    internal bool TryClaimCommand(string pluginId, string commandName)
    {
        string? existingOwner;
        lock (_sync)
        {
            if (_commandOwners.TryGetValue(commandName, out string? owner))
            {
                if (string.Equals(owner, pluginId, StringComparison.Ordinal))
                {
                    return true;
                }

                existingOwner = owner;
            }
            else
            {
                _commandOwners[commandName] = pluginId;
                return true;
            }
        }

        RejectOwnership(
            "registry.command",
            commandName,
            existingOwner,
            pluginId,
            $"Command ownership: plugin '{pluginId}' cannot claim '/{commandName}' (owned by '{existingOwner}').");
        return false;
    }

    internal bool TryClaimGenerator(string pluginId, string name)
    {
        string? existingOwner;
        lock (_sync)
        {
            if (_generatorOwners.TryGetValue(name, out string? owner))
            {
                if (string.Equals(owner, pluginId, StringComparison.Ordinal))
                {
                    return true;
                }

                existingOwner = owner;
            }
            else
            {
                _generatorOwners[name] = pluginId;
                return true;
            }
        }

        RejectOwnership(
            "registry.generator",
            name,
            existingOwner,
            pluginId,
            $"Generator ownership: plugin '{pluginId}' cannot claim '{name}' (owned by '{existingOwner}').");
        return false;
    }

    void RejectOwnership(string kind, string key, string winnerPluginId, string loserPluginId, string message)
    {
        if (_diagnostics is not null)
        {
            _diagnostics.Report(new PluginConflict(kind, key, winnerPluginId, loserPluginId, message));
            return;
        }

        Log.Warn(LogCategory.Plugins, message);
    }

    internal void ThrowIfCreativeFrozen()
    {
        if (_creativeFrozen || CuratedItemCatalog.IsInitialized)
        {
            throw new InvalidOperationException(
                "Creative tab entries must be registered before catalog freeze (plugin Load).");
        }
    }

    internal void ThrowIfItemsFrozen()
    {
        if (_itemsFrozen || CuratedItemCatalog.IsInitialized)
        {
            throw new InvalidOperationException(
                "Items must be registered before catalog freeze (plugin Load).");
        }
    }

    internal void ThrowIfBlocksFrozen()
    {
        if (_blocksFrozen || Block.BlockRegistry.IsLoaded)
        {
            throw new InvalidOperationException(
                "Blocks must be registered before BlockRegistry.EnsureLoaded.");
        }
    }

    internal void ThrowIfGeneratorsFrozen()
    {
        if (_generatorsFrozen || GeneratorFactory.IsFrozen)
        {
            throw new InvalidOperationException(
                "Generators must be registered before world bootstrap.");
        }
    }

    internal void ThrowIfCommandsFrozen()
    {
        if (_commandsFrozen)
        {
            throw new InvalidOperationException(
                "Commands cannot be registered after command registry freeze.");
        }
    }

    internal void ThrowIfTraitsFrozen()
    {
        if (_traitsFrozen || Block.BlockRegistry.IsLoaded)
        {
            throw new InvalidOperationException(
                "Traits must be registered before BlockRegistry.EnsureLoaded / catalog freeze.");
        }
    }

    internal void MarkItemRegistered(string identifier)
    {
        lock (_sync)
        {
            _registeredItems.Add(identifier);
        }
    }

    internal bool IsItemRegistered(string identifier)
    {
        lock (_sync)
        {
            if (_registeredItems.Contains(identifier))
            {
                return true;
            }
        }

        if (!CuratedItemCatalog.IsInitialized)
        {
            return false;
        }

        return CuratedItemCatalog.TryGetByIdentifier(identifier, out _);
    }

    sealed class PluginScopedContentRegistries(ContentRegistriesCore core, string pluginId) : IContentRegistries
    {
        public IItemRegistry Items { get; } = new ScopedItemRegistry(core, pluginId);
        public IBlockRegistry Blocks { get; } = new ScopedBlockRegistry(core, pluginId);
        public ICommandRegistry Commands { get; } = new ScopedCommandRegistry(core, pluginId);
        public ICreativeTabRegistry CreativeTabs { get; } = new ScopedCreativeTabRegistry(core, pluginId);
        public IGeneratorRegistry Generators { get; } = new ScopedGeneratorRegistry(core, pluginId);
        public IBlockTraitRegistry BlockTraits { get; } = new ScopedBlockTraitRegistry(core, pluginId);
        public IItemTraitRegistry ItemTraits { get; } = new ScopedItemTraitRegistry(core, pluginId);
        public IEntityTraitRegistry EntityTraits { get; } = new ScopedEntityTraitRegistry(core, pluginId);
        public IPlayerTraitRegistry PlayerTraits { get; } = new ScopedPlayerTraitRegistry(core, pluginId);
    }

    sealed class ScopedItemRegistry(ContentRegistriesCore core, string pluginId) : IItemRegistry
    {
        public void Register(ItemRegistration registration) =>
            ((ItemRegistryFacade)core.Items).Register(pluginId, registration);

        public bool IsRegistered(string identifier) => core.Items.IsRegistered(identifier);
    }

    sealed class ScopedBlockRegistry(ContentRegistriesCore core, string pluginId) : IBlockRegistry
    {
        public void Register(BlockRegistration registration) =>
            ((BlockRegistryFacade)core.Blocks).Register(pluginId, registration);
    }

    sealed class ScopedCommandRegistry(ContentRegistriesCore core, string pluginId) : ICommandRegistry
    {
        public void Register(IPluginCommand command) =>
            ((CommandRegistryFacade)core.Commands).Register(pluginId, command);
    }

    sealed class ScopedCreativeTabRegistry(ContentRegistriesCore core, string pluginId) : ICreativeTabRegistry
    {
        public void AddEntry(string entryPluginId, int category, string identifier) =>
            ((CreativeTabRegistryFacade)core.CreativeTabs).AddEntry(
                string.IsNullOrWhiteSpace(entryPluginId) ? pluginId : entryPluginId,
                category,
                identifier);
    }

    sealed class ScopedGeneratorRegistry(ContentRegistriesCore core, string pluginId) : IGeneratorRegistry
    {
        public void Register(string name, Type generatorType) =>
            ((GeneratorRegistryFacade)core.Generators).Register(pluginId, name, generatorType);
    }

    sealed class ScopedBlockTraitRegistry(ContentRegistriesCore core, string pluginId) : IBlockTraitRegistry
    {
        public void RegisterFromAssembly(System.Reflection.Assembly assembly, string _) =>
            ((BlockTraitRegistryFacade)core.BlockTraits).RegisterFromAssembly(pluginId, assembly);

        public void Register(Type traitType, string _) =>
            ((BlockTraitRegistryFacade)core.BlockTraits).Register(pluginId, traitType);
    }

    sealed class ScopedItemTraitRegistry(ContentRegistriesCore core, string pluginId) : IItemTraitRegistry
    {
        public void RegisterFromAssembly(System.Reflection.Assembly assembly, string _) =>
            ((ItemTraitRegistryFacade)core.ItemTraits).RegisterFromAssembly(pluginId, assembly);

        public void Register(Type traitType, string _) =>
            ((ItemTraitRegistryFacade)core.ItemTraits).Register(pluginId, traitType);
    }

    sealed class ScopedEntityTraitRegistry(ContentRegistriesCore core, string pluginId) : IEntityTraitRegistry
    {
        public void RegisterFromAssembly(System.Reflection.Assembly assembly, string _) =>
            ((EntityTraitRegistryFacade)core.EntityTraits).RegisterFromAssembly(pluginId, assembly);

        public void Register(Type traitType, string _) =>
            ((EntityTraitRegistryFacade)core.EntityTraits).Register(pluginId, traitType);
    }

    sealed class ScopedPlayerTraitRegistry(ContentRegistriesCore core, string pluginId) : IPlayerTraitRegistry
    {
        public void RegisterFromAssembly(System.Reflection.Assembly assembly, string _) =>
            ((PlayerTraitRegistryFacade)core.PlayerTraits).RegisterFromAssembly(pluginId, assembly);

        public void Register(Type traitType, string _) =>
            ((PlayerTraitRegistryFacade)core.PlayerTraits).Register(pluginId, traitType);
    }
}
