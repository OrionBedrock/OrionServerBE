using System.Reflection;
using McMaster.NETCore.Plugins;
using Orion.Api;

namespace Orion.Plugins;

/// <summary>
/// Scans <c>plugins/&lt;Name&gt;/&lt;Name&gt;.dll</c>, loads via McMaster, and runs lifecycle hooks.
/// Missing or empty plugin directories are fine. Failures are isolated per plugin.
/// </summary>
public sealed class PluginManager
{
    private readonly IOrionServer _server;
    private readonly List<PluginContainer> _plugins = [];
    private readonly Type[] _sharedTypes;
    private bool _loaded;

    public PluginManager(IOrionServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        _server = server;
        _sharedTypes = typeof(Plugin).Assembly.GetExportedTypes();
    }

    public IReadOnlyList<PluginContainer> Plugins => _plugins;

    public int LoadedCount => _plugins.Count(static p => p.State is PluginState.Loaded or PluginState.Started);

    public int StartedCount => _plugins.Count(static p => p.State == PluginState.Started);

    /// <summary>
    /// Discover → McMaster load → <see cref="Plugin.OnLoad"/> → <see cref="Plugin.OnStart"/>.
    /// </summary>
    public void LoadAll(string directory)
    {
        if (_loaded)
        {
            throw new InvalidOperationException("Plugins already loaded.");
        }

        _loaded = true;
        string absolute = Path.GetFullPath(directory);
        if (!Directory.Exists(absolute))
        {
            Directory.CreateDirectory(absolute);
            return;
        }

        foreach (string subDir in Directory.GetDirectories(absolute))
        {
            string pluginName = Path.GetFileName(subDir);
            string pluginDll = Path.Combine(subDir, $"{pluginName}.dll");
            if (!File.Exists(pluginDll))
            {
                continue;
            }

            LoadOne(pluginName, pluginDll);
        }

        StartAll();
    }

    public void DisableAll()
    {
        for (int i = _plugins.Count - 1; i >= 0; i--)
        {
            PluginContainer container = _plugins[i];
            if (container.State != PluginState.Started)
            {
                continue;
            }

            try
            {
                container.Plugin.OnDisable();
                container.State = PluginState.Disabled;
            }
            catch (Exception exception)
            {
                container.State = PluginState.Failed;
                container.FailureReason = exception.Message;
                Console.Error.WriteLine($"Failed to disable plugin '{container.Name}': {exception.Message}");
            }
        }
    }

    private void StartAll()
    {
        foreach (PluginContainer container in _plugins)
        {
            if (container.State != PluginState.Loaded)
            {
                continue;
            }

            try
            {
                container.Plugin.OnStart();
                container.State = PluginState.Started;
            }
            catch (Exception exception)
            {
                container.State = PluginState.Failed;
                container.FailureReason = exception.Message;
                Console.Error.WriteLine($"Failed to start plugin '{container.Name}': {exception.Message}");
            }
        }
    }

    private void LoadOne(string pluginName, string assemblyPath)
    {
        try
        {
            PluginLoader loader = PluginLoader.CreateFromAssemblyFile(
                assemblyPath,
                sharedTypes: _sharedTypes);

            Assembly assembly = loader.LoadDefaultAssembly();
            Type entry = GetEntry(assembly);
            if (Activator.CreateInstance(entry) is not Plugin plugin)
            {
                throw new InvalidOperationException($"Plugin entry '{entry.FullName}' could not be created.");
            }

            plugin.Server = _server;
            plugin.Name = pluginName;
            plugin.AssemblyPath = assemblyPath;
            plugin.OnLoad();

            _plugins.Add(new PluginContainer
            {
                Plugin = plugin,
                Name = pluginName,
                AssemblyPath = assemblyPath,
                Loader = loader,
                State = PluginState.Loaded,
            });
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to load plugin '{pluginName}': {exception.Message}");
        }
    }

    private static Type GetEntry(Assembly assembly)
    {
        Type[] entries = assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Plugin).IsAssignableFrom(type))
            .ToArray();

        if (entries.Length == 0)
        {
            throw new InvalidOperationException($"No Plugin entry type found in '{assembly.GetName().Name}'.");
        }

        if (entries.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple Plugin entry types found in '{assembly.GetName().Name}': {string.Join(", ", entries.Select(t => t.FullName))}.");
        }

        return entries[0];
    }
}
