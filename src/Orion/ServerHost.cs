using Orion.Config;
using Orion.RakNet;
using Orion.Scheduling;
using Orion.World;
using Orion.World.Generation;
using Orion.World.Pregeneration;
using Orion.World.Provider;
using Orion.World.Threading;
using Orion.Protocol.Enums;
using WorldLogger = Orion.Logger.Logger;

namespace Orion;

/// <summary>
/// Boots config, world persistence, area scheduling, and RakNet gameplay networking.
/// </summary>
public sealed class ServerHost : IDisposable
{
    public Server Server { get; }

    public World.World World => Server.GetWorld();

    public SchedulingBootstrap Scheduling { get; }

    public NetworkServer? NetworkServer { get; private set; }

    private ServerHost(Server server, SchedulingBootstrap scheduling)
    {
        Server = server;
        Scheduling = scheduling;
    }

    public static ServerHost Bootstrap(OrionConfig config, string? worldsRoot = null, bool startSchedulers = true)
    {
        ArgumentNullException.ThrowIfNull(config);

        OrionInfo.SetCanAcceptPlayers(false);

        SchedulingThreadBudget threadBudget = SchedulingThreadRequirements.Compute(config);

        ServerProperties properties = new()
        {
            TicksPerSecond = config.Server.Orion.TicksPerSecond,
            AreaThreadingEnabled = threadBudget.AreaThreadingEnabled,
            SessionThreadingEnabled = threadBudget.SessionWorkerCount > 0,
            AreaThreadCount = threadBudget.AreaWorkerCount,
            SessionThreadCount = threadBudget.SessionWorkerCount,
            SimulationDistance = config.Server.WorldDefaultSettings.Dimensions.FirstOrDefault()?.SimulationDistance ?? 10,
            OnlineMode = !config.Server.Orion.OfflineMode
        };

        WorldLogger.Info(
            LogCategory.System,
            "Scheduling threads: {0} area worker(s), {1} session worker(s) ({2} dedicated)",
            threadBudget.AreaWorkerCount,
            threadBudget.SessionWorkerCount,
            threadBudget.DedicatedWorkerThreads);

        Server server = new(properties);

        string spawnWorldIdentifier = config.Server.Orion.SpawnWorldIdentifier;
        ResolvedWorldSettings resolved = WorldSettingsResolver.Resolve(
            spawnWorldIdentifier,
            config.Server.WorldDefaultSettings,
            worldsRoot);

        string dbPath = Path.Combine(resolved.DirectoryPath, "db");
        Directory.CreateDirectory(resolved.DirectoryPath);

        World.World world = new(resolved.Identifier, new LevelDbProvider(dbPath))
        {
            Gamerules = resolved.Settings.Gamerules
        };
        ChunkPregenerator pregenerator = new();

        WorldLogger.Info(LogCategory.World, "Starting chunk pregeneration for world '{0}'", resolved.Identifier);

        foreach (DimensionConfig dimensionConfig in resolved.Settings.Dimensions)
        {
            Generator generator = GeneratorFactory.Create(dimensionConfig.Generator);
            world.CreateDimension(
                dimensionConfig.Identifier,
                (DimensionType)dimensionConfig.Type,
                generator,
                dimensionConfig.ThreadingAreas);

            Dimension dimension = world.GetDimension(dimensionConfig.Identifier)!;
            GameRulesFactory.Apply(dimension.Gamerules, resolved.Settings.Gamerules);
            pregenerator.PregenerateAll(dimension, dimensionConfig.ChunkPregeneration ?? [], dimensionConfig.Identifier);

            if (threadBudget.AreaThreadingEnabled)
            {
                AttachConfiguredAreas(server, dimension);
            }
        }

        WorldLogger.Info(LogCategory.World, "Chunk pregeneration complete for world '{0}'", resolved.Identifier);

        server.SetWorld(world);
        SchedulingBootstrap scheduling = new(server);

        if (startSchedulers)
        {
            scheduling.Start();
        }

        OrionInfo.SetCanAcceptPlayers(true);
        server.Emit(new ServerStartSignal());

        return new ServerHost(server, scheduling);
    }

    public void AttachNetwork(NetworkServer networkServer)
    {
        NetworkServer = networkServer ?? throw new ArgumentNullException(nameof(networkServer));
        NetworkServer.OnMessage += (connection, payload) => Server.Network.HandlePacket(connection, payload);
        NetworkServer.OnDisconnected += connection => Server.Network.HandleDisconnected(connection);
    }

    public void Dispose()
    {
        OrionInfo.SetCanAcceptPlayers(false);
        NetworkServer?.Dispose();
        Scheduling.Dispose();
        Server.GetWorld().Dispose();
    }

    private static void AttachConfiguredAreas(Server server, Dimension dimension)
    {
        if (!server.Properties.AreaThreadingEnabled || !server.AreaScheduler.IsActive)
        {
            return;
        }

        foreach (AreaShard shard in dimension.ShardManager.Shards)
        {
            if (shard.IsDefault)
            {
                continue;
            }

            server.AreaScheduler.RequestAttachArea(dimension, shard.AreaIndex);
        }
    }
}
