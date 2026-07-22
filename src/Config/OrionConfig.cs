using System.Text.Json.Serialization;

namespace Orion.Config;

/// <summary>
/// Root configuration model. Maps 1:1 to config/server.json.
/// </summary>
public sealed class OrionConfig
{
    [JsonPropertyName("Logging")]
    public LoggingConfig Logging { get; init; } = new();

    [JsonPropertyName("Server")]
    public ServerSection Server { get; init; } = new();

    [JsonPropertyName("Runtime")]
    public RuntimeConfig Runtime { get; init; } = new();

    [JsonPropertyName("Plugins")]
    public PluginsConfig Plugins { get; init; } = new();
}

public sealed class LoggingConfig
{
    [JsonPropertyName("LogLevel")]
    public LogLevelConfig LogLevel { get; init; } = new();
}

public sealed class LogLevelConfig
{
    [JsonPropertyName("System")]
    public CategoryLogLevel System { get; init; } = new();

    [JsonPropertyName("World")]
    public CategoryLogLevel World { get; init; } = new();

    [JsonPropertyName("Orion")]
    public CategoryLogLevel Orion { get; init; } = new();

    [JsonPropertyName("RakNet")]
    public CategoryLogLevel RakNet { get; init; } = new();

    [JsonPropertyName("Protocol")]
    public CategoryLogLevel Protocol { get; init; } = new();

    [JsonPropertyName("Binary")]
    public CategoryLogLevel Binary { get; init; } = new();

    [JsonPropertyName("Plugins")]
    public CategoryLogLevel Plugins { get; init; } = new();

    public bool IsEnabled(string category, LogLevel level) =>
        IsEnabled(ParseCategory(category), level);

    public bool IsEnabled(LogCategory category, LogLevel level)
    {
        CategoryLogLevel settings = GetCategorySettings(category);
        return level switch
        {
            LogLevel.Debug => settings.Debug,
            LogLevel.Info => settings.Info,
            LogLevel.Warn => settings.Warn,
            LogLevel.Error => settings.Error,
            LogLevel.Chat => settings.Chat,
            _ => true
        };
    }

    private CategoryLogLevel GetCategorySettings(LogCategory category) => category switch
    {
        LogCategory.System => System,
        LogCategory.World => World,
        LogCategory.Orion => Orion,
        LogCategory.RakNet => RakNet,
        LogCategory.Protocol => Protocol,
        LogCategory.Binary => Binary,
        LogCategory.Plugins => Plugins,
        _ => System
    };

    private static LogCategory ParseCategory(string category) =>
        Enum.TryParse<LogCategory>(category, ignoreCase: true, out LogCategory parsed)
            ? parsed
            : LogCategory.System;
}

public sealed class CategoryLogLevel
{
    [JsonPropertyName("Debug")]
    public bool Debug { get; init; } = true;

    [JsonPropertyName("Info")]
    public bool Info { get; init; } = true;

    [JsonPropertyName("Warn")]
    public bool Warn { get; init; } = true;

    [JsonPropertyName("Error")]
    public bool Error { get; init; } = true;

    [JsonPropertyName("Chat")]
    public bool Chat { get; init; } = true;
}

public enum LogCategory
{
    System,
    World,
    Orion,
    RakNet,
    Protocol,
    Binary,
    Plugins
}

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
    Chat
}

public sealed class ServerSection
{
    [JsonPropertyName("Edition")]
    public string Edition { get; init; } = "MCPE";

    [JsonPropertyName("Name")]
    public string Name { get; init; } = "OrionServer";

    [JsonPropertyName("Motd")]
    public string Motd { get; init; } = "OrionServer";

    [JsonPropertyName("Orion")]
    public OrionSection Orion { get; init; } = new();

    [JsonPropertyName("WorldDefaultSettings")]
    public WorldProperties WorldDefaultSettings { get; init; } = new();

    [JsonPropertyName("Network")]
    public NetworkConfig Network { get; init; } = new();

    [JsonPropertyName("Raknet")]
    public RaknetConfig Raknet { get; init; } = new();
}

public sealed class OrionSection
{
    [JsonPropertyName("Permissions")]
    public string Permissions { get; init; } = "./config/permissions.json";

    [JsonPropertyName("Resources")]
    public ResourcesConfig Resources { get; init; } = new();

    [JsonPropertyName("SpawnWorldIdentifier")]
    public string SpawnWorldIdentifier { get; init; } = "default";

    [JsonPropertyName("MovementValidation")]
    public bool MovementValidation { get; init; } = true;

    [JsonPropertyName("MovementHorizontalThreshold")]
    public float MovementHorizontalThreshold { get; init; } = 0.4f;

    [JsonPropertyName("MovementVerticalThreshold")]
    public float MovementVerticalThreshold { get; init; } = 0.6f;

    [JsonPropertyName("ShutdownMessage")]
    public string ShutdownMessage { get; init; } = "Server is shutting down...";

    [JsonPropertyName("TicksPerSecond")]
    public int TicksPerSecond { get; init; } = 20;

    [JsonPropertyName("OfflineMode")]
    public bool OfflineMode { get; init; } = true;
}

public sealed class ResourcesConfig
{
    [JsonPropertyName("Path")]
    public string Path { get; init; } = "./resource_packs";

    [JsonPropertyName("MustAccept")]
    public bool MustAccept { get; init; } = true;

    [JsonPropertyName("ChunkDownloadTimeout")]
    public int ChunkDownloadTimeout { get; init; } = 1;

    [JsonPropertyName("ChunkMaxSize")]
    public int ChunkMaxSize { get; init; } = 262144;
}

public sealed class NetworkConfig
{
    [JsonPropertyName("CompressionMethod")]
    public int CompressionMethod { get; init; }

    [JsonPropertyName("CompressionThreshold")]
    public int CompressionThreshold { get; init; } = 1;

    [JsonPropertyName("FrameMonitoring")]
    public bool FrameMonitoring { get; init; } = true;

    [JsonPropertyName("PacketsPerFrame")]
    public int PacketsPerFrame { get; init; } = 64;
}

public sealed class RaknetConfig
{
    [JsonPropertyName("Address")]
    public string Address { get; init; } = "0.0.0.0";

    [JsonPropertyName("PortIPV4")]
    public ushort PortIPV4 { get; init; } = 19132;

    [JsonPropertyName("PortIPV6")]
    public ushort PortIPV6 { get; init; } = 19133;

    [JsonPropertyName("Protocol")]
    public int Protocol { get; init; } = 1001;

    [JsonPropertyName("Version")]
    public string Version { get; init; } = "1.26.30";

    [JsonPropertyName("Message")]
    public string Message { get; init; } = "Orion";

    [JsonPropertyName("MaxConnections")]
    public int MaxConnections { get; init; } = 40;

    [JsonPropertyName("MtuMaxSize")]
    public int MtuMaxSize { get; init; } = 1492;

    [JsonPropertyName("MtuMinSize")]
    public int MtuMinSize { get; init; } = 400;

    [JsonPropertyName("ValidatePort")]
    public bool ValidatePort { get; init; } = true;
}

public sealed class WorldJsonFile
{
    [JsonPropertyName("settings")]
    public WorldProperties Settings { get; init; } = new();
}

public sealed class WorldProperties
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; init; } = "default";

    [JsonPropertyName("seed")]
    public long Seed { get; init; } = 2570659193;

    [JsonPropertyName("gamemode")]
    public string Gamemode { get; init; } = "survival";

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; init; } = "normal";

    [JsonPropertyName("saveInterval")]
    public int SaveInterval { get; init; } = 5;

    [JsonPropertyName("dimensions")]
    public List<DimensionConfig> Dimensions { get; init; } = [new()];

    [JsonPropertyName("gamerules")]
    public GamerulesConfig Gamerules { get; init; } = new();
}

public sealed class DimensionConfig
{
    [JsonPropertyName("identifier")]
    public string Identifier { get; init; } = "overworld";

    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("generator")]
    public string Generator { get; init; } = "void";

    [JsonPropertyName("viewDistance")]
    public int ViewDistance { get; init; } = 20;

    [JsonPropertyName("simulationDistance")]
    public int SimulationDistance { get; init; } = 10;

    [JsonPropertyName("spawnPosition")]
    public int[] SpawnPosition { get; init; } = [0, -57, 0];

    [JsonPropertyName("chunkPregeneration")]
    [JsonConverter(typeof(ChunkPregenerationJsonConverter))]
    public List<ChunkPregenerationConfig> ChunkPregeneration { get; init; } = [];

    [JsonPropertyName("threadingAreas")]
    public List<ThreadingAreaConfig> ThreadingAreas { get; init; } = [];
}

public sealed class ThreadingAreaConfig
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "default";

    [JsonPropertyName("start")]
    public int[] Start { get; init; } = [-50, -50];

    [JsonPropertyName("end")]
    public int[] End { get; init; } = [50, 50];
}

public sealed class ChunkPregenerationConfig
{
    [JsonPropertyName("start")]
    public int[] Start { get; init; } = [-400, -400];

    [JsonPropertyName("end")]
    public int[] End { get; init; } = [400, 400];

    [JsonPropertyName("memoryLock")]
    public bool MemoryLock { get; init; } = true;
}

public sealed class GamerulesConfig
{
    [JsonPropertyName("showCoordinates")]
    public bool ShowCoordinates { get; init; } = true;

    [JsonPropertyName("showDaysPlayed")]
    public bool ShowDaysPlayed { get; init; }

    [JsonPropertyName("doDayLightCycle")]
    public bool DoDayLightCycle { get; init; } = true;

    [JsonPropertyName("doImmediateRespawn")]
    public bool DoImmediateRespawn { get; init; }

    [JsonPropertyName("doTileDrops")]
    public bool DoTileDrops { get; init; } = true;

    [JsonPropertyName("keepInventory")]
    public bool KeepInventory { get; init; }

    [JsonPropertyName("fallDamage")]
    public bool FallDamage { get; init; } = true;

    [JsonPropertyName("fireDamage")]
    public bool FireDamage { get; init; } = true;

    [JsonPropertyName("drowningDamage")]
    public bool DrowningDamage { get; init; } = true;

    [JsonPropertyName("randomTickSpeed")]
    public int RandomTickSpeed { get; init; } = 1;

    [JsonPropertyName("locatorBar")]
    public bool LocatorBar { get; init; }
}

public sealed class RuntimeConfig
{
    [JsonPropertyName("ThreadPool")]
    public ThreadPoolConfig ThreadPool { get; init; } = new();
}

/// <summary>
/// Caps the .NET thread pool used by async continuations and background work.
/// Values of 0 leave the corresponding limit unchanged (OS/.NET default).
/// </summary>
public sealed class ThreadPoolConfig
{
    [JsonPropertyName("MinWorkerThreads")]
    public int MinWorkerThreads { get; init; }

    [JsonPropertyName("MaxWorkerThreads")]
    public int MaxWorkerThreads { get; init; }

    [JsonPropertyName("MinIoCompletionThreads")]
    public int MinIoCompletionThreads { get; init; }

    [JsonPropertyName("MaxIoCompletionThreads")]
    public int MaxIoCompletionThreads { get; init; }
}
