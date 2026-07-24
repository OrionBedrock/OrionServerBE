using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;

namespace Orion.Protocol.Packets;

[Packet(PacketId.BlockEvent)]
public sealed record BlockEventPacket : DataPacket {
    /// <summary>
    /// Block position for this event.
    /// </summary>
    public BlockPos Position;

    /// <summary>
    /// Block event type.
    /// </summary>
    public BlockEventType Type;

    /// <summary>
    /// Event-specific data value.
    /// </summary>
    public int Data;

    public override void Deserialize(Binary.BinaryReader reader) {
        BlockPos position = Position;
        position.Read(reader);
        Position = position;
        Type = (BlockEventType)reader.ReadVarInt();
        Data = reader.ReadVarInt();
    }

    public override void Serialize(Binary.BinaryWriter writer) {
        Position.Write(writer);
        writer.WriteZigZag((int)Type);
        writer.WriteZigZag(Data);
    }
}
