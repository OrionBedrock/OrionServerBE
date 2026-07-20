namespace Orion.Scheduling;

using Orion.Player;
using Orion.World;
using ApiSpawnOptions = Orion.Api.EntitySpawnOptions;
using CoreSpawnOptions = Orion.Entity.Traits.Types.EntitySpawnOptions;

public static class AreaPlayerSpawnPipeline
{
    public static void ContinueSpawn(Server server, Player player)
    {
        _ = server;
        _ = player;
    }

    public static bool TryCompleteSpawn(
        Server server,
        Player player,
        Dimension dimension,
        out CoreSpawnOptions options)
    {
#if DEBUG
        if (dimension.UsesAreaThreading() && dimension.World is not null)
        {
            ThreadGuard.AssertSimulationThread(dimension, dimension.World);
        }
#endif

        options = new CoreSpawnOptions(InitialSpawn: true);

        if (dimension.World is null)
        {
            return false;
        }

        WorldPlayerPresence.OnPlayerEnteredWorld(server, dimension.World);

        PlayerSpawnSignal spawnSignal = new(player, new ApiSpawnOptions(InitialSpawn: options.InitialSpawn));
        server.Emit(spawnSignal);
        if (!spawnSignal.Emit())
        {
            return false;
        }

        options = new CoreSpawnOptions(InitialSpawn: spawnSignal.Options.InitialSpawn);
        player.Spawn(dimension, options);
        return true;
    }
}
