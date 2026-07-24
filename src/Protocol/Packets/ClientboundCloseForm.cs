using Orion.Protocol.Enums;

namespace Orion.Protocol.Packets;

[Packet(PacketId.ClientboundCloseForm)]
public sealed record ClientboundCloseFormPacket : DataPacket {
    public override void Deserialize(Binary.BinaryReader reader) { }

    public override void Serialize(Binary.BinaryWriter writer) { }
}
