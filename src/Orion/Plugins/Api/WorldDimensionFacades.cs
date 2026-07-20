using System.Runtime.CompilerServices;
using Orion.Api;
using Orion.Api.Blocks;
using Orion.Api.Network;
using Orion.Block;
using Orion.World;
using GameplayBlock = Orion.Block.Block;
using GameplayEntity = Orion.Entity.Entity;
using ApiVec3f = Orion.Api.Math.Vec3f;
using ProtocolVec3f = Orion.Protocol.Types.Vec3f;
using ApiSpawnOptions = Orion.Api.EntitySpawnOptions;
using CoreSpawnOptions = Orion.Entity.Traits.Types.EntitySpawnOptions;
using WorldInstance = Orion.World.World;

namespace Orion.Plugins.Api;

internal static class WorldApi
{
    private static readonly ConditionalWeakTable<WorldInstance, WorldFacade> Worlds = new();

    public static IWorld For(WorldInstance world) => Worlds.GetValue(world, static w => new WorldFacade(w));
}

internal sealed class WorldFacade(WorldInstance world) : IWorld
{
    internal WorldInstance Inner => world;

    public string Name => world.Name;

    public IDimension? GetDimension(string name)
    {
        Dimension? dimension = world.GetDimension(name);
        return dimension is null ? null : DimensionApi.For(dimension);
    }

    public IReadOnlyCollection<IDimension> Dimensions
    {
        get
        {
            List<IDimension> list = [];
            foreach (Dimension dimension in world.Dimensions)
            {
                list.Add(DimensionApi.For(dimension));
            }

            return list;
        }
    }
}

internal static class DimensionApi
{
    private static readonly ConditionalWeakTable<Dimension, DimensionFacade> Dimensions = new();

    public static IDimension For(Dimension dimension) =>
        Dimensions.GetValue(dimension, static d => new DimensionFacade(d));

    public static Dimension? TryUnwrap(IDimension? dimension) =>
        dimension is DimensionFacade facade ? facade.Inner : null;
}

internal sealed class DimensionFacade(Dimension dimension) : IDimension
{
    internal Dimension Inner => dimension;

    public string Name => dimension.Identifier;

    public IWorld World =>
        dimension.World is null
            ? throw new InvalidOperationException("Dimension is not attached to a world.")
            : WorldApi.For(dimension.World);

    public IBlock? GetBlock(int x, int y, int z, int layer = 0) =>
        dimension.GetBlock(x, y, z, layer);

    public void SetBlock(int x, int y, int z, IBlock block, int layer = 0, bool dirty = true)
    {
        if (block is not GameplayBlock gameplay)
        {
            throw new ArgumentException("Block must be an Orion.Block.Block instance.", nameof(block));
        }

        dimension.SetBlock(x, y, z, gameplay, layer, dirty);
    }

    public IBlockPermutation GetPermutation(int x, int y, int z, int layer = 0) =>
        dimension.GetGameplayPermutation(x, y, z, layer);

    public void SetPermutation(int x, int y, int z, IBlockPermutation permutation, int layer = 0, bool dirty = true)
    {
        if (permutation is not BlockPermutation gameplay)
        {
            throw new ArgumentException("Permutation must be an Orion.Block.BlockPermutation instance.", nameof(permutation));
        }

        dimension.SetGameplayPermutation(x, y, z, gameplay, layer, dirty);
    }

    public IEntity SpawnEntity(string typeIdentifier, ApiVec3f position, ApiSpawnOptions? options = null)
    {
        GameplayEntity entity = new(typeIdentifier)
        {
            Position = new ProtocolVec3f(position.X, position.Y, position.Z)
        };
        entity.Spawn(dimension, new CoreSpawnOptions(InitialSpawn: options?.InitialSpawn ?? true));
        return entity;
    }

    public IReadOnlyCollection<IEntity> GetEntities()
    {
        List<IEntity> list = [];
        foreach (GameplayEntity entity in dimension.GetEntities())
        {
            list.Add(entity);
        }

        return list;
    }

    public void Broadcast(IOutboundPacket packet, PacketBroadcastOptions? options = null)
    {
        BroadcastOptions? broadcastOptions = null;
        if (options?.MaxDistance is double maxDistance)
        {
            broadcastOptions = new BroadcastOptions { Radius = (float)maxDistance };
        }

        dimension.Broadcast(OutboundPacketAdapter.ToDataPacket(packet), broadcastOptions);
    }
}
