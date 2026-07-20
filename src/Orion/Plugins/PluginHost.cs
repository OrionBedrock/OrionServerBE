using System.Reflection;
using System.Runtime.Loader;
using McMaster.NETCore.Plugins;
using Orion.Config;
using Orion.Api;
using Orion.Api.Traits;
using Orion.Gameplay;
using Orion.PluginContracts;
using Orion.PluginContracts.Diagnostics;
using Orion.PluginContracts.Events;
using Orion.PluginContracts.Messaging;
using Orion.PluginContracts.Network;
using Orion.PluginContracts.Registry;
using Orion.PluginContracts.Services;
using Orion.Plugins.Diagnostics;
using Orion.Plugins.Messaging;
using Orion.Plugins.Network;
using Orion.Plugins.Registry;
using Orion.Plugins.Services;
using Log = Orion.Logger.Logger;

namespace Orion.Plugins;

/// <summary>
/// Orchestrates plugin discovery and lifecycle. Assemblies are loaded <b>only</b> via McMaster
/// (<see cref="PluginLoader"/>) — never <c>Assembly.LoadFrom</c> or a custom ALC.
/// Already-loaded plugin assemblies are exposed to later plugins via
/// <see cref="AssemblyLoadContext.Default"/> resolving so hard-deps (e.g. block_containers → containers)
/// share type identity under <c>PreferSharedTypes</c>.
/// </summary>
public static class PluginHost
{
    private static readonly object Sync = new();
    private static readonly List<LoadedPlugin> Loaded = [];
    private static readonly StubOrionWorld WorldStub = new();

    private static bool _loadAttempted;
    private static bool _enabled;
    private static bool _worldInitialized;
    private static bool _pluginAssemblyResolvingHooked;
    private static Server? _server;
    private static ServerEventBus? _rootEventBus;
    private static ContentRegistriesCore? _registries;
    private static ServiceRegistry? _services;
    private static PluginMessenger? _messenger;
    private static PacketPipeline? _packets;
    private static PluginDiagnostics? _diagnostics;

    public static IReadOnlyList<string> LoadedPluginIds
    {
        get
        {
            lock (Sync)
            {
                return Loaded.Select(p => p.Manifest.Id).ToArray();
            }
        }
    }

    public static IReadOnlyList<IPluginManifest> LoadedManifests
    {
        get
        {
            lock (Sync)
            {
                return Loaded.Select(p => (IPluginManifest)p.Manifest).ToArray();
            }
        }
    }

    /// <summary>Host content registries (created on first LoadConfigured / test ensure).</summary>
    public static ContentRegistriesCore Registries
    {
        get
        {
            lock (Sync)
            {
                return EnsureRegistriesUnlocked();
            }
        }
    }

    /// <summary>Shared service registry (created on first ensure).</summary>
    public static ServiceRegistry Services
    {
        get
        {
            lock (Sync)
            {
                return EnsureServicesUnlocked();
            }
        }
    }

    /// <summary>Shared plugin messenger (created on first ensure).</summary>
    public static PluginMessenger Messenger
    {
        get
        {
            lock (Sync)
            {
                return EnsureMessengerUnlocked();
            }
        }
    }

    /// <summary>Shared packet pipeline (created on first ensure).</summary>
    public static PacketPipeline Packets
    {
        get
        {
            lock (Sync)
            {
                return EnsurePacketsUnlocked();
            }
        }
    }

    /// <summary>Live server after <see cref="EnableAll"/>; used by packet-owning plugins.</summary>
    public static bool TryGetServer(out Server? server)
    {
        lock (Sync)
        {
            server = _server;
            return server is not null;
        }
    }

    /// <summary>Conflict diagnostics and loaded manifests (created on first ensure).</summary>
    public static IPluginDiagnostics Diagnostics
    {
        get
        {
            lock (Sync)
            {
                return EnsureDiagnosticsUnlocked();
            }
        }
    }

    public static void LoadConfigured(OrionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (Sync)
        {
            if (_loadAttempted)
            {
                return;
            }

            _loadAttempted = true;
            PluginDiagnostics diagnostics = EnsureDiagnosticsUnlocked();
            diagnostics.ConflictMode = config.Plugins.ConflictMode;
            EnsureRegistriesUnlocked();
            EnsureServicesUnlocked();
            EnsureMessengerUnlocked();
            EnsurePacketsUnlocked();

            if (!config.Plugins.Enabled)
            {
                Log.Info(LogCategory.Plugins, "Plugins disabled (Plugins.Enabled=false). Skipping plugin load.");
                return;
            }

            string directory = ResolvePluginsDirectory(config.Plugins.Directory);
            if (!Directory.Exists(directory))
            {
                Log.Warn(LogCategory.Plugins, "Plugins enabled but directory not found: {0}", directory);
                return;
            }

            List<PluginManifest> discovered = DiscoverManifests(directory);
            if (discovered.Count == 0)
            {
                Log.Info(LogCategory.Plugins, "No plugins found under {0} (need plugins/*/plugin.json)", directory);
                return;
            }

            IReadOnlyList<PluginManifest> ordered = PluginLoadOrder.Sort(discovered);
            Type[] sharedTypes =
            [
                typeof(IOrionPlugin),
                typeof(IPluginContext),
                typeof(IPluginLoadContext),
                typeof(IPluginManifest),
                typeof(IWorldInitContext),
                typeof(IEventBus),
                typeof(ISignal),
                typeof(ICancellable),
                typeof(EventPriority),
                typeof(ServerEvent),
                typeof(IContentRegistries),
                typeof(IItemRegistry),
                typeof(IBlockRegistry),
                typeof(ICreativeTabRegistry),
                typeof(ICommandRegistry),
                typeof(IGeneratorRegistry),
                typeof(IPluginCommand),
                typeof(ItemRegistration),
                typeof(BlockRegistration),
                typeof(IOrionWorld),
                typeof(IServiceRegistry),
                typeof(ServicePriority),
                typeof(IPluginMessenger),
                typeof(PluginMessage),
                typeof(IPacketPipeline),
                typeof(PacketReceiveHook),
                typeof(PacketSendHook),
                typeof(PacketReceiveContext),
                typeof(PacketSendContext),
                typeof(IPlayerConnection),
                typeof(IServer),
                typeof(IPlayer),
                typeof(IEntity),
                typeof(GameplayApi),
                typeof(BlockTraitBase),
                typeof(ItemTraitBase),
                typeof(EntityTraitBase),
                typeof(PlayerTraitBase),
                typeof(IBlockTraitRegistry),
                typeof(IItemTraitRegistry),
                typeof(IEntityTraitRegistry),
                typeof(IPlayerTraitRegistry)
            ];

            foreach (PluginManifest manifest in ordered)
            {
                try
                {
                    LoadOneWithMcMaster(manifest, sharedTypes);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Failed to load plugin '{manifest.Id}' from {manifest.AssemblyPath}: {exception.Message}",
                        exception);
                }
            }
        }
    }

    /// <summary>Call after <see cref="Orion.Item.ItemRegistry.EnsureLoaded"/> to freeze item/creative/block registries.</summary>
    public static void NotifyCatalogLoaded()
    {
        lock (Sync)
        {
            EnsureRegistriesUnlocked().FreezeItemsAndCreative();
            EnsureRegistriesUnlocked().FreezeBlocks();
        }
    }

    /// <summary>Call after <see cref="ServerHost.Bootstrap"/> to freeze generator registration.</summary>
    public static void NotifyWorldBootstrapped()
    {
        lock (Sync)
        {
            EnsureRegistriesUnlocked().FreezeGenerators();
        }
    }

    public static void EnableAll(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);
        lock (Sync)
        {
            _server = server;
            _rootEventBus = new ServerEventBus(server);
            server.PacketPipeline = EnsurePacketsUnlocked();
            EnsureRegistriesUnlocked().BindCommands(server.Commands);
            EnableAllUnlocked();
            EnsureRegistriesUnlocked().FreezeCommands();
        }
    }

    public static void InitializeWorld()
    {
        lock (Sync)
        {
            if (_worldInitialized)
            {
                return;
            }

            if (!_enabled)
            {
                if (_server is null)
                {
                    throw new InvalidOperationException(
                        "PluginHost.EnableAll(Server) must be called before InitializeWorld when plugins are loaded.");
                }

                EnableAllUnlocked();
            }

            if (Loaded.Count == 0)
            {
                _worldInitialized = true;
                return;
            }

            ContentRegistriesCore registries = EnsureRegistriesUnlocked();
            foreach (LoadedPlugin entry in Loaded)
            {
                entry.Plugin.OnWorldInitialize(new WorldInitContext(
                    entry.Manifest,
                    WorldStub,
                    registries.ForPlugin(entry.Manifest.Id)));
            }

            _worldInitialized = true;
        }
    }

    public static void DisableAll()
    {
        lock (Sync)
        {
            if (Loaded.Count == 0)
            {
                _enabled = false;
                _worldInitialized = false;
                _server = null;
                _rootEventBus = null;
                return;
            }

            ContentRegistriesCore registries = EnsureRegistriesUnlocked();
            ServiceRegistry services = EnsureServicesUnlocked();
            PluginMessenger messenger = EnsureMessengerUnlocked();
            PacketPipeline packets = EnsurePacketsUnlocked();

            for (int i = Loaded.Count - 1; i >= 0; i--)
            {
                LoadedPlugin entry = Loaded[i];
                try
                {
                    entry.EventBus?.UnsubscribeAll();
                    IEventBus events = entry.EventBus
                        ?? _rootEventBus
                        ?? (IEventBus)NoOpEventBus.Instance;
                    IPluginMessenger pluginMessenger = (IPluginMessenger?)entry.Messenger ?? messenger;
                    entry.Plugin.OnDisable(new PluginContext(
                        entry.Manifest,
                        RequireServer(),
                        services,
                        pluginMessenger,
                        events,
                        registries.ForPlugin(entry.Manifest.Id),
                        packets));
                }
                catch (Exception exception)
                {
                    Log.Error(
                        LogCategory.Plugins,
                        "Plugin '{0}' OnDisable failed: {1}",
                        entry.Manifest.Id,
                        exception.Message);
                }
                finally
                {
                    entry.Messenger?.UnsubscribeAll();
                    services.UnregisterAll(entry.Plugin);
                    packets.RemovePlugin(entry.Plugin.Id);
                }
            }

            Loaded.Clear();
            _enabled = false;
            _worldInitialized = false;
            _server = null;
            _rootEventBus = null;
        }
    }

    /// <summary>Test helper: register an already-constructed plugin without McMaster.</summary>
    internal static void RegisterLoadedForTests(IOrionPlugin plugin, PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(manifest);
        lock (Sync)
        {
            EnsureDiagnosticsUnlocked();
            EnsureRegistriesUnlocked();
            EnsureServicesUnlocked();
            EnsureMessengerUnlocked();
            EnsurePacketsUnlocked();
            Loaded.Add(new LoadedPlugin(manifest, plugin, Loader: null, Assembly: null));
        }
    }

    /// <summary>Test helper: set conflict mode on the shared diagnostics store.</summary>
    internal static void SetConflictModeForTests(ConflictMode mode)
    {
        lock (Sync)
        {
            EnsureDiagnosticsUnlocked().ConflictMode = mode;
        }
    }

    internal static void ResetForTests()
    {
        lock (Sync)
        {
            foreach (LoadedPlugin entry in Loaded)
            {
                entry.EventBus?.UnsubscribeAll();
                entry.Messenger?.UnsubscribeAll();
            }

            Loaded.Clear();
            _loadAttempted = false;
            _enabled = false;
            _worldInitialized = false;
            _server = null;
            _rootEventBus = null;
            _registries?.ResetForTests();
            _registries = null;
            _services?.ResetForTests();
            _services = null;
            _messenger?.ResetForTests();
            _messenger = null;
            _packets?.ResetForTests();
            _packets = null;
            _diagnostics?.ResetForTests();
            _diagnostics = null;
        }
    }

    static PluginDiagnostics EnsureDiagnosticsUnlocked()
    {
        if (_diagnostics is null)
        {
            _diagnostics = new PluginDiagnostics();
            _diagnostics.SetManifestsProvider(static () => LoadedManifests);
        }

        return _diagnostics;
    }

    static ContentRegistriesCore EnsureRegistriesUnlocked()
    {
        if (_registries is null)
        {
            _registries = new ContentRegistriesCore();
            _registries.SetDiagnostics(EnsureDiagnosticsUnlocked());
        }

        return _registries;
    }

    static ServiceRegistry EnsureServicesUnlocked()
    {
        if (_services is null)
        {
            _services = new ServiceRegistry();
            _services.SetDiagnostics(EnsureDiagnosticsUnlocked());
        }

        return _services;
    }

    static PluginMessenger EnsureMessengerUnlocked() => _messenger ??= new PluginMessenger();

    static PacketPipeline EnsurePacketsUnlocked()
    {
        if (_packets is null)
        {
            _packets = new PacketPipeline();
            _packets.SetDiagnostics(EnsureDiagnosticsUnlocked());
        }

        return _packets;
    }

    static IServer RequireServer() =>
        _server ?? throw new InvalidOperationException(
            "PluginHost has no Server bound. Call EnableAll(Server) before enabling or disabling plugins.");

    static void EnableAllUnlocked()
    {
        if (_enabled)
        {
            return;
        }

        if (Loaded.Count == 0)
        {
            _enabled = true;
            return;
        }

        if (_server is null || _rootEventBus is null)
        {
            throw new InvalidOperationException("PluginHost.EnableAll(Server) requires a Server instance.");
        }

        ContentRegistriesCore registries = EnsureRegistriesUnlocked();
        ServiceRegistry services = EnsureServicesUnlocked();
        PluginMessenger messenger = EnsureMessengerUnlocked();
        PacketPipeline packets = EnsurePacketsUnlocked();
        _server.PacketPipeline = packets;

        foreach (LoadedPlugin entry in Loaded)
        {
            TrackingEventBus tracking = new(_rootEventBus);
            TrackingPluginMessenger trackingMessenger = new(messenger);
            entry.EventBus = tracking;
            entry.Messenger = trackingMessenger;
            entry.Plugin.OnEnable(new PluginContext(
                entry.Manifest,
                RequireServer(),
                services,
                trackingMessenger,
                tracking,
                registries.ForPlugin(entry.Manifest.Id),
                packets));
            Log.Info(LogCategory.Plugins, "Enabled plugin '{0}' v{1}", entry.Manifest.Id, entry.Manifest.Version);
        }

        _enabled = true;
    }

    static void LoadOneWithMcMaster(PluginManifest manifest, Type[] sharedTypes)
    {
        EnsurePluginAssemblyResolvingUnlocked();

        PluginLoader loader = PluginLoader.CreateFromAssemblyFile(
            manifest.AssemblyPath,
            config =>
            {
                config.PreferSharedTypes = true;
                config.IsUnloadable = false;
                foreach (Type shared in sharedTypes)
                {
                    config.SharedAssemblies.Add(shared.Assembly.GetName());
                }
            });

        Assembly assembly = loader.LoadDefaultAssembly();
        Type? type = assembly.GetType(manifest.Main, throwOnError: false, ignoreCase: false)
            ?? assembly.GetTypes().FirstOrDefault(t =>
                string.Equals(t.FullName, manifest.Main, StringComparison.Ordinal)
                || string.Equals(t.Name, manifest.Main, StringComparison.Ordinal));

        if (type is null || type.IsAbstract || !typeof(IOrionPlugin).IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"Plugin main type '{manifest.Main}' not found or does not implement IOrionPlugin.");
        }

        if (Activator.CreateInstance(type) is not IOrionPlugin plugin)
        {
            throw new InvalidOperationException($"Failed to construct plugin main type '{manifest.Main}'.");
        }

        if (!string.Equals(plugin.Id, manifest.Id, StringComparison.Ordinal))
        {
            Log.Warn(
                LogCategory.Plugins,
                "Plugin id mismatch: manifest '{0}' vs IOrionPlugin.Id '{1}' — using manifest id.",
                manifest.Id,
                plugin.Id);
        }

        ContentRegistriesCore registries = EnsureRegistriesUnlocked();
        // Register before Load so RegisterFromAssembly / trait reflection can resolve hard-deps.
        Loaded.Add(new LoadedPlugin(manifest, plugin, loader, assembly));
        plugin.Load(new PluginLoadContext(manifest, registries.ForPlugin(manifest.Id)));
        Log.Info(
            LogCategory.Plugins,
            "Loaded plugin '{0}' v{1} via McMaster from {2}",
            manifest.Id,
            manifest.Version,
            manifest.AssemblyPath);
    }

    /// <summary>
    /// Lets later plugins resolve assemblies already loaded by earlier plugins (hard deps)
    /// through McMaster's PreferSharedTypes → default ALC path.
    /// </summary>
    static void EnsurePluginAssemblyResolvingUnlocked()
    {
        if (_pluginAssemblyResolvingHooked)
        {
            return;
        }

        AssemblyLoadContext.Default.Resolving += ResolveLoadedPluginAssembly;
        _pluginAssemblyResolvingHooked = true;
    }

    static Assembly? ResolveLoadedPluginAssembly(AssemblyLoadContext _, AssemblyName name)
    {
        // May run re-entrantly while LoadConfigured holds Sync (PreferSharedTypes → default ALC).
        // Do not lock here. Loaded is only mutated on the load thread under Sync.
        foreach (LoadedPlugin entry in Loaded)
        {
            if (entry.Assembly is null)
            {
                continue;
            }

            if (string.Equals(
                    entry.Assembly.GetName().Name,
                    name.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return entry.Assembly;
            }
        }

        return null;
    }

    static List<PluginManifest> DiscoverManifests(string pluginsRoot)
    {
        List<PluginManifest> list = [];
        foreach (string dir in Directory.GetDirectories(pluginsRoot))
        {
            string manifestPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            list.Add(PluginManifest.ParseFile(manifestPath));
        }

        return list;
    }

    static string ResolvePluginsDirectory(string configured)
    {
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configured));
    }

    private sealed class LoadedPlugin(
        PluginManifest Manifest,
        IOrionPlugin Plugin,
        PluginLoader? Loader,
        Assembly? Assembly)
    {
        public PluginManifest Manifest { get; } = Manifest;
        public IOrionPlugin Plugin { get; } = Plugin;
        public PluginLoader? Loader { get; } = Loader;
        public Assembly? Assembly { get; } = Assembly;
        public TrackingEventBus? EventBus { get; set; }
        public TrackingPluginMessenger? Messenger { get; set; }
    }
}

internal sealed class NoOpEventBus : IEventBus
{
    public static NoOpEventBus Instance { get; } = new();

    public void Subscribe<TSignal>(Action<TSignal> handler, EventPriority priority = EventPriority.Normal)
        where TSignal : ISignal
    {
    }

    public void Unsubscribe<TSignal>(Action<TSignal> handler) where TSignal : ISignal
    {
    }

    public IDisposable SubscribeDisposable<TSignal>(
        Action<TSignal> handler,
        EventPriority priority = EventPriority.Normal) where TSignal : ISignal =>
        EmptyDisposable.Instance;

    sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();
        public void Dispose()
        {
        }
    }
}
