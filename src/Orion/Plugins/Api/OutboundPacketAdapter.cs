using Orion.Api.Network;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using ProtocolBlockPos = Orion.Protocol.Types.BlockPos;

namespace Orion.Plugins.Api;

/// <summary>Maps plugin <see cref="IOutboundPacket"/> instances to Protocol <see cref="DataPacket"/>.</summary>
internal static class OutboundPacketAdapter
{
    public static DataPacket ToDataPacket(IOutboundPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return packet switch
        {
            UpdateBlockOutbound update => ToUpdateBlock(update),
            ProtocolOutboundPacket protocol => protocol.Packet,
            _ => throw new NotSupportedException(
                $"Unsupported IOutboundPacket type '{packet.GetType().FullName}'. " +
                "Use BlockNetwork helpers or OutboundPackets.FromProtocol.")
        };
    }

    public static DataPacket[] ToDataPackets(IOutboundPacket[] packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        DataPacket[] result = new DataPacket[packets.Length];
        for (int i = 0; i < packets.Length; i++)
        {
            result[i] = ToDataPacket(packets[i]);
        }

        return result;
    }

    static UpdateBlockPacket ToUpdateBlock(UpdateBlockOutbound update) =>
        new()
        {
            Position = new ProtocolBlockPos
            {
                X = update.Position.X,
                Y = update.Position.Y,
                Z = update.Position.Z
            },
            NetworkBlockId = update.NetworkBlockId,
            Flags = (UpdateBlockFlagsType)update.Flags,
            Layer = (UpdateBlockLayerType)update.Layer
        };
}

/// <summary>Host wrapper that carries a Protocol <see cref="DataPacket"/> as <see cref="IOutboundPacket"/>.</summary>
internal sealed class ProtocolOutboundPacket(DataPacket packet) : IOutboundPacket
{
    public DataPacket Packet { get; } = packet ?? throw new ArgumentNullException(nameof(packet));
}
