using Orion.Protocol.Enums;
using Orion.Protocol.Packets;

namespace Orion.Protocol.Packets;

[Packet(PacketId.SetLocalPlayerAsInitialized)]
public sealed record SetLocalPlayerAsInitializedPacket : DataPacket {
    /// <summary>
    /// The runtime id of the entity
    /// </summary>
    public ulong EntityRuntimeId;

    public override void Deserialize(Binary.BinaryReader reader) {
        EntityRuntimeId = reader.ReadVarULong();
    }

    public override void Serialize(Binary.BinaryWriter writer) {
        writer.WriteVarULong(EntityRuntimeId);
    }
}
