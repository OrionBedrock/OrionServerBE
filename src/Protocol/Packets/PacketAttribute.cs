using Orion.Protocol.Enums;

namespace Orion.Protocol.Packets;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PacketAttribute : Attribute {
    public PacketId Id;

    public PacketAttribute(PacketId id) {
        Id = id;
    }
}
