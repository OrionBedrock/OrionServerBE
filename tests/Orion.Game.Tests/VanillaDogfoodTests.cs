namespace Orion.Game.Tests;

public sealed class VanillaDogfoodTests
{
    [Fact]
    public void FirstParty_Plugins_Do_Not_Reference_Orion_Csproj()
    {
        string? pluginsRoot = FindPluginsRoot();
        Assert.False(string.IsNullOrEmpty(pluginsRoot), "Plugins-Orion root not found");

        List<string> offenders = [];
        foreach (string csproj in Directory.EnumerateFiles(pluginsRoot, "*.csproj", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(csproj);
            if (text.Contains("Orion.csproj", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(pluginsRoot, csproj));
            }
        }

        Assert.True(offenders.Count == 0, "Plugins still reference Orion.csproj:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void FirstParty_Plugins_Use_NuGet_Not_Local_OrionServerBE_Paths()
    {
        string? pluginsRoot = FindPluginsRoot();
        Assert.False(string.IsNullOrEmpty(pluginsRoot), "Plugins-Orion root not found");

        List<string> offenders = [];
        foreach (string csproj in Directory.EnumerateFiles(pluginsRoot, "*.csproj", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(csproj);
            if (text.Contains("OrionServerBERoot", StringComparison.Ordinal) ||
                text.Contains("src/Orion/", StringComparison.Ordinal) ||
                text.Contains("src/Protocol/", StringComparison.Ordinal) ||
                text.Contains("src/PluginContracts/", StringComparison.Ordinal) ||
                text.Contains("src/Orion.Api/", StringComparison.Ordinal) ||
                text.Contains("src/Orion.Gameplay.Api/", StringComparison.Ordinal) ||
                text.Contains("src/Binary/", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(pluginsRoot, csproj));
            }
        }

        Assert.True(offenders.Count == 0,
            "Plugins still reference local OrionServerBE paths:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void FirstParty_Plugins_Have_NuGet_Config()
    {
        string? pluginsRoot = FindPluginsRoot();
        Assert.False(string.IsNullOrEmpty(pluginsRoot), "Plugins-Orion root not found");
        Assert.True(File.Exists(Path.Combine(pluginsRoot, "nuget.config")),
            "Plugins-Orion/nuget.config missing (local SDK feed)");
    }

    static string? FindPluginsRoot()
    {
        string? dir = new DirectoryInfo(AppContext.BaseDirectory).FullName;
        for (int i = 0; i < 12 && dir is not null; i++)
        {
            string sibling = Path.Combine(dir, "Plugins-Orion");
            if (Directory.Exists(sibling) &&
                Directory.EnumerateFiles(sibling, "*.csproj", SearchOption.AllDirectories).Any())
            {
                return sibling;
            }

            if (File.Exists(Path.Combine(dir, "Orion.sln")) ||
                File.Exists(Path.Combine(dir, "OrionServerBE.sln")) ||
                Directory.Exists(Path.Combine(dir, "src", "Orion")))
            {
                string fromRepo = Path.GetFullPath(Path.Combine(dir, "..", "Plugins-Orion"));
                if (Directory.Exists(fromRepo))
                {
                    return fromRepo;
                }
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }
}
