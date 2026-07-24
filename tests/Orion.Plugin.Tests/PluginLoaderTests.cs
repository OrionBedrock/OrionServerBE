using Orion.Api;
using Orion.Plugins;
using Xunit;

namespace Orion.Plugins.Tests;

public sealed class PluginLoaderTests
{
    [Fact]
    public void LoadAll_empty_directory_is_ok()
    {
        string root = CreateTempRoot();
        try
        {
            string plugins = Path.Combine(root, "plugins");
            Directory.CreateDirectory(plugins);

            var manager = new PluginManager(new StubServer("empty"));
            manager.LoadAll(plugins);

            Assert.Equal(0, manager.StartedCount);
            Assert.Empty(manager.Plugins);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void LoadAll_missing_directory_creates_empty()
    {
        string root = CreateTempRoot();
        try
        {
            string plugins = Path.Combine(root, "plugins-missing");
            Assert.False(Directory.Exists(plugins));

            var manager = new PluginManager(new StubServer("missing"));
            manager.LoadAll(plugins);

            Assert.True(Directory.Exists(plugins));
            Assert.Equal(0, manager.StartedCount);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void LoadAll_hello_plugin_runs_lifecycle()
    {
        string root = CreateTempRoot();
        try
        {
            string helloDir = Path.Combine(root, "plugins", "Hello");
            Directory.CreateDirectory(helloDir);
            string sourceDll = Path.Combine(AppContext.BaseDirectory, "hello-source", "Hello.dll");
            Assert.True(File.Exists(sourceDll), $"Hello.dll fixture missing at {sourceDll}");
            File.Copy(sourceDll, Path.Combine(helloDir, "Hello.dll"));

            var server = new StubServer("HelloHost");
            var manager = new PluginManager(server);
            manager.LoadAll(Path.Combine(root, "plugins"));

            Assert.Equal(1, manager.StartedCount);
            PluginContainer container = Assert.Single(manager.Plugins);
            Assert.Equal(PluginState.Started, container.State);
            Assert.Equal("Hello", container.Name);
            Assert.Equal("Hello", container.Plugin.Name);
            Assert.Equal("HelloHost", container.Plugin.Server.Name);

            string lifecycle = ReadLifecycle(container.Plugin);
            Assert.Equal("OnLoad;OnStart;", lifecycle);

            manager.DisableAll();
            Assert.Equal(PluginState.Disabled, container.State);
            Assert.Equal("OnLoad;OnStart;OnDisable;", ReadLifecycle(container.Plugin));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string ReadLifecycle(Orion.Api.Plugin plugin)
    {
        System.Reflection.PropertyInfo? prop = plugin.GetType().GetProperty("Lifecycle");
        Assert.NotNull(prop);
        return Assert.IsType<string>(prop.GetValue(plugin));
    }

    private static string CreateTempRoot()
        => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "orion-plugin-" + Guid.NewGuid().ToString("N"))).FullName;

    private static void TryDelete(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private sealed class StubServer(string name) : IOrionServer
    {
        public string Name { get; } = name;
        public IGlobalScheduler? GlobalScheduler => null;
        public IRegionScheduler? RegionScheduler => null;
        public IContentRegistries? Registries => null;
    }
}
