namespace Orion.Scheduling;

using Orion.Api.Events;
using Orion.World.Threading;
using WorldInstance = Orion.World.World;
using GameplayEntity = Orion.Entity.Entity;
using GameplayPlayer = Orion.Player.Player;

/// <summary>
/// Chooses which thread runs signal handlers. Global events
/// (<see cref="ServerEvent.ServerStart"/>, <see cref="ServerEvent.PlayerJoin"/>) run inline on the caller;
/// player/entity signals prefer the owning area thread when area threading is active.
/// Priority ordering is handled by <see cref="Server"/>; this type only affects affinity.
/// </summary>
internal static class SignalAffinity
{
    public static bool IsGlobalEvent(ISignal signal) =>
        signal.Event is ServerEvent.ServerStart or ServerEvent.PlayerJoin;

    public static WorldInstance? TryResolveWorld(Server server, ISignal signal)
    {
        switch (signal)
        {
            case PlayerSignal playerSignal when playerSignal.Player is GameplayPlayer player:
                if (player.Dimension?.World is WorldInstance playerWorld)
                {
                    return playerWorld;
                }

                return playerSignal.Event == ServerEvent.PlayerSpawn ? server.GetWorld() : null;
            case EntityHurtSignal hurtSignal when hurtSignal.Entity is GameplayEntity hurtEntity:
                return hurtEntity.Dimension?.World as WorldInstance;
            case EntitySpawnSignal spawnSignal when spawnSignal.Entity is GameplayEntity spawnEntity:
                return spawnEntity.Dimension?.World as WorldInstance;
            case EntityDieSignal dieSignal when dieSignal.Entity is GameplayEntity dieEntity:
                return dieEntity.Dimension?.World as WorldInstance;
            default:
                return null;
        }
    }

    public static AreaHandle? TryResolveArea(Server server, ISignal signal)
    {
        if (!server.Properties.AreaThreadingEnabled || !server.AreaScheduler.IsActive)
        {
            return null;
        }

        return signal switch
        {
            PlayerSignal playerSignal when playerSignal.Player is GameplayEntity playerEntity
                && TryResolveAreaForEntity(playerEntity, out AreaHandle playerArea) => playerArea,
            EntityHurtSignal hurtSignal when hurtSignal.Entity is GameplayEntity hurtEntity
                && TryResolveAreaForEntity(hurtEntity, out AreaHandle hurtArea) => hurtArea,
            EntitySpawnSignal spawnSignal when spawnSignal.Entity is GameplayEntity spawnEntity
                && TryResolveAreaForEntity(spawnEntity, out AreaHandle spawnArea) => spawnArea,
            EntityDieSignal dieSignal when dieSignal.Entity is GameplayEntity dieEntity
                && TryResolveAreaForEntity(dieEntity, out AreaHandle dieArea) => dieArea,
            _ => null
        };
    }

    static bool TryResolveAreaForEntity(GameplayEntity entity, out AreaHandle area)
    {
        area = default;
        if (entity.Dimension is not Dimension dimension)
        {
            return false;
        }

        int chunkX = (int)entity.Position.X >> 4;
        int chunkZ = (int)entity.Position.Z >> 4;
        AreaShard shard = dimension.ShardManager.ResolveShard(chunkX, chunkZ);
        area = new AreaHandle(dimension, shard.AreaIndex);
        return true;
    }
}
