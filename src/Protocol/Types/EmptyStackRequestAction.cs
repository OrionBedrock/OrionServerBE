using BinaryReader = Basalt.Binary.BinaryReader;
using BinaryWriter = Basalt.Binary.BinaryWriter;

namespace Orion.Protocol.Types;

public sealed class EmptyStackRequestAction(byte type) : IStackRequestAction, DataType {
    public byte ActionType => type;
    public void Read(BinaryReader reader) { }
    public void Write(BinaryWriter writer) { }
}
