using Orion.Protocol.Enums;
using Orion.Protocol.Packets;

namespace Orion.Protocol.Packets;

[Packet(PacketId.ClientCacheStatus)]
public sealed record ClientCacheStatusPacket : DataPacket {
    /// <summary>
    /// Whether client-side cache is enabled.
    /// </summary>
    public bool Enabled;

    public override void Deserialize(Binary.BinaryReader reader) {
        Enabled = reader.ReadBool();
    }

    public override void Serialize(Binary.BinaryWriter writer) {
        writer.WriteBool(Enabled);
    }
}
