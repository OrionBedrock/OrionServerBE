using System.Reflection;
using Orion.Api;
using Orion.Api.Math;
using Orion.Api.Network;
using Orion.Plugins;
using Orion.Plugins.Api;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.World;
using Orion.World.Provider;
using GameplayBlock = Orion.Block.Block;
using PlayerEntity = Orion.Player.Player;

namespace Orion.Game.Tests;

/// <summary>
/// S6 Protocol escape: Api helpers + host adapter.
/// Construction paths under test intentionally avoid Protocol types where noted.
/// </summary>
[Collection("PluginHost")]
public sealed class ProtocolEscapeTests
{
    public ProtocolEscapeTests()
    {
        PluginHost.ResetForTests();
        Orion.Block.BlockRegistry.ResetForTests();
    }

    [Fact]
    public void BlockNetwork_CreateUpdateBlock_Adapts_To_UpdateBlockPacket()
    {
        // Api-only construction (no Protocol types in the plugin path).
        IOutboundPacket outbound = BlockNetwork.CreateUpdateBlock(
            new BlockPos(3, 64, -9),
            networkBlockId: 42,
            flags: UpdateBlockNetworkFlags.Network | UpdateBlockNetworkFlags.Neighbors,
            layer: UpdateBlockNetworkFlags.LayerNormal);

        DataPacket wire = OutboundPacketAdapter.ToDataPacket(outbound);
        UpdateBlockPacket update = Assert.IsType<UpdateBlockPacket>(wire);
        Assert.Equal(3, update.Position.X);
        Assert.Equal(64, update.Position.Y);
        Assert.Equal(-9, update.Position.Z);
        Assert.Equal(42, update.NetworkBlockId);
        Assert.Equal(UpdateBlockFlagsType.Network | UpdateBlockFlagsType.Neighbors, update.Flags);
        Assert.Equal(UpdateBlockLayerType.Normal, update.Layer);
    }

    [Fact]
    public void FromProtocol_RoundTrips_Through_Adapter()
    {
        UpdateBlockPacket original = new()
        {
            Position = new Orion.Protocol.Types.BlockPos { X = 1, Y = 2, Z = 3 },
            NetworkBlockId = 7,
            Flags = UpdateBlockFlagsType.Network,
            Layer = UpdateBlockLayerType.Normal
        };

        IOutboundPacket wrapped = OutboundPackets.FromProtocol(original);
        DataPacket adapted = OutboundPacketAdapter.ToDataPacket(wrapped);
        Assert.Same(original, adapted);
    }

    [Fact]
    public void IPlayer_Send_Accepts_CreateUpdateBlock_Without_Throwing()
    {
        PlayerEntity player = new("s6", "0", Guid.NewGuid());
        IOutboundPacket outbound = BlockNetwork.CreateUpdateBlock(new BlockPos(0, 1, 0), 1);
        ((IPlayer)player).Send(outbound);
    }

    [Fact]
    public void FromProtocol_Send_Path_Does_Not_Throw()
    {
        PlayerEntity player = new("s6-proto", "0", Guid.NewGuid());
        UpdateBlockPacket packet = new()
        {
            Position = new Orion.Protocol.Types.BlockPos { X = 0, Y = 0, Z = 0 },
            NetworkBlockId = 0,
            Flags = UpdateBlockFlagsType.Network,
            Layer = UpdateBlockLayerType.Normal
        };

        ((IPlayer)player).Send(OutboundPackets.FromProtocol(packet));
    }

    [Fact]
    public void ApiOnly_SetBlock_And_Broadcast_CreateUpdateBlock()
    {
        using InMemoryProvider provider = new();
        Dimension dimension = new("overworld", DimensionType.Overworld, provider);
        IDimension apiDimension = DimensionApi.For(dimension);

        Orion.Block.BlockRegistry.EnsureLoaded();
        GameplayBlock stone = new("minecraft:stone");
        apiDimension.SetBlock(0, 64, 0, stone);

        IOutboundPacket update = BlockNetwork.CreateUpdateBlock(
            new BlockPos(0, 64, 0),
            stone.Permutation.NetworkId);

        // No attached Server → BroadcastService no-ops; still exercises facade + MaxDistance mapping.
        apiDimension.Broadcast(update, new PacketBroadcastOptions { MaxDistance = 48 });

        DataPacket wire = OutboundPacketAdapter.ToDataPacket(update);
        UpdateBlockPacket packet = Assert.IsType<UpdateBlockPacket>(wire);
        Assert.Equal(stone.Permutation.NetworkId, packet.NetworkBlockId);
        Assert.Equal(0, packet.Position.X);
        Assert.Equal(64, packet.Position.Y);
        Assert.Equal(0, packet.Position.Z);
    }

    [Fact]
    public void SharedAssemblies_Do_Not_Include_Protocol()
    {
        string? protocolAssembly = typeof(UpdateBlockPacket).Assembly.GetName().Name;
        Assert.False(string.IsNullOrEmpty(protocolAssembly));

        // Marker types from PluginHost SharedAssemblies — Protocol must stay out of that set.
        AssemblyName[] shared =
        [
            typeof(IPlayer).Assembly.GetName(),
            typeof(IServer).Assembly.GetName(),
            typeof(Orion.Gameplay.GameplayApi).Assembly.GetName(),
            typeof(Orion.PluginContracts.IOrionPlugin).Assembly.GetName(),
            typeof(Orion.PluginContracts.Network.IPacketPipeline).Assembly.GetName()
        ];

        Assert.DoesNotContain(shared, name => string.Equals(name.Name, protocolAssembly, StringComparison.Ordinal));
        Assert.NotEqual(typeof(IPlayer).Assembly, typeof(UpdateBlockPacket).Assembly);
    }
}
