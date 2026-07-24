using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orion.Config;

public sealed class OrionConfig
{
    public LoggingConfig Logging { get; set; } = new();
    public ServerRootConfig Server { get; set; } = new();
    public RuntimeConfig Runtime { get; set; } = new();
    public PluginsConfig Plugins { get; set; } = new();

    public static OrionConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize(json, OrionConfigJsonContext.Default.OrionConfig)
            ?? throw new InvalidOperationException($"Failed to deserialize config: {path}");
        return config;
    }
}

public sealed class LoggingConfig
{
    public Dictionary<string, Dictionary<string, bool>>? LogLevel { get; set; }
}

public sealed class ServerRootConfig
{
    public string Edition { get; set; } = "MCPE";
    public string Name { get; set; } = "OrionServer";
    public string Motd { get; set; } = "OrionServer";
    public OrionSectionConfig Orion { get; set; } = new();
    public WorldDefaultSettingsConfig WorldDefaultSettings { get; set; } = new();
    public NetworkConfig Network { get; set; } = new();
    public RaknetConfig Raknet { get; set; } = new();
}

public sealed class OrionSectionConfig
{
    public string Permissions { get; set; } = "./config/permissions.json";
    public string SpawnWorldIdentifier { get; set; } = "default";
    public bool MovementValidation { get; set; } = true;
    public double MovementHorizontalThreshold { get; set; } = 0.4;
    public double MovementVerticalThreshold { get; set; } = 0.6;
    public string ShutdownMessage { get; set; } = "Server is shutting down...";
    public int TicksPerSecond { get; set; } = 20;
    public bool OnlineMode { get; set; } = true;
    public bool AllowOfflineDev { get; set; }
}

public sealed class WorldDefaultSettingsConfig
{
    public string Identifier { get; set; } = "default";
    public long Seed { get; set; }
    public string Gamemode { get; set; } = "survival";
    public string Difficulty { get; set; } = "normal";
    public int SaveInterval { get; set; } = 5;
    public List<DimensionConfig> Dimensions { get; set; } = [];
}

public sealed class DimensionConfig
{
    public string Identifier { get; set; } = "overworld";
    public int Type { get; set; }
    public string Generator { get; set; } = "void";
    public int ViewDistance { get; set; } = 8;
    public int SimulationDistance { get; set; } = 8;
    public int[] SpawnPosition { get; set; } = [0, 64, 0];
}

public sealed class NetworkConfig
{
    public int CompressionMethod { get; set; }
    public int CompressionThreshold { get; set; } = 1;
    public bool FrameMonitoring { get; set; } = true;
    public int PacketsPerFrame { get; set; } = 64;
}

public sealed class RaknetConfig
{
    public string Address { get; set; } = "0.0.0.0";
    public ushort PortIPV4 { get; set; } = 19132;
    public ushort PortIPV6 { get; set; } = 19133;
    public string Message { get; set; } = "OrionServer";
    public int MaxConnections { get; set; } = 150;
    public int MtuMaxSize { get; set; } = 1492;
    public int MtuMinSize { get; set; } = 400;
    public bool ValidatePort { get; set; } = true;
}

public sealed class RuntimeConfig
{
    public ThreadPoolsConfig Threads { get; set; } = new();
    public RegionsConfig Regions { get; set; } = new();
}

public sealed class ThreadPoolsConfig
{
    public ThreadLimitConfig Raknet { get; set; } = new() { Max = 2 };
    public ThreadLimitConfig RegionTick { get; set; } = new() { Max = 0 };
    public ThreadLimitConfig ChunkIo { get; set; } = new() { Max = 2 };
    public ThreadLimitConfig ChunkWorkers { get; set; } = new() { Max = 2 };
    public ThreadLimitConfig IoPersistence { get; set; } = new() { Max = 2 };
    public ThreadLimitConfig AsyncScheduler { get; set; } = new() { Max = 2 };
}

public sealed class ThreadLimitConfig
{
    public int Max { get; set; }
}

public sealed class RegionsConfig
{
    public int GridExponent { get; set; } = 4;
    public string Scheduler { get; set; } = "EDF";
}

public sealed class PluginsConfig
{
    public string Directory { get; set; } = "./plugins";
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(OrionConfig))]
internal partial class OrionConfigJsonContext : JsonSerializerContext;
