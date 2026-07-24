using BinaryReader = Basalt.Binary.BinaryReader;
using BinaryWriter = Basalt.Binary.BinaryWriter;

namespace Orion.Protocol.Types;

public struct BoolType : DataType {
    public bool Value;
    public void Read(BinaryReader reader) => Value = reader.ReadBool();
    public void Write(BinaryWriter writer) => writer.WriteBool(Value);

    public static implicit operator bool(BoolType value) => value.Value;
    public static implicit operator BoolType(bool value) => new() { Value = value };
}
