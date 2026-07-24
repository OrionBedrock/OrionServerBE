using Orion.Protocol.Enums;
using BinaryReader = Basalt.Binary.BinaryReader;
using BinaryWriter = Basalt.Binary.BinaryWriter;

namespace Orion.Protocol.Packets;

[Packet(PacketId.ResourcePackChunkRequest)]
public sealed record ResourcePackChunkRequestPacket : DataPacket {
    public string Uuid = string.Empty;
    public uint ChunkIndex;

    public override void Deserialize(BinaryReader reader) {
        Uuid = reader.ReadVarString();
        ChunkIndex = reader.ReadUInt32(true);
    }

    public override void Serialize(BinaryWriter writer) {
        writer.WriteVarString(Uuid);
        writer.WriteUInt32(ChunkIndex, true);
    }
}
