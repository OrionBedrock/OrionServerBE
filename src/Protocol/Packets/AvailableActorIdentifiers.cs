using Orion.Protocol.Enums;
using Orion.Protocol.Nbt;
using Orion.Protocol.Packets;

namespace Orion.Protocol.Packets;

[Packet(PacketId.AvailableActorIdentifiers)]
public sealed record AvailableActorIdentifiersPacket : DataPacket {
    private static readonly TagOptions NetworkNbtOptions = new(Name: true, Type: true, VarInt: true);

    /// <summary>
    /// Actor identifier table as NBT.
    /// </summary>
    public CompoundTag Data = new();

    public override void Deserialize(Binary.BinaryReader reader) {
        Data = Io.NBT.ReadTag<CompoundTag>(reader, NetworkNbtOptions);
    }

    public override void Serialize(Binary.BinaryWriter writer) {
        Io.NBT.WriteTag(writer, Data, NetworkNbtOptions);
    }
}
