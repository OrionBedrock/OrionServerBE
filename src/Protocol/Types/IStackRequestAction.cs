using BinaryReader = Basalt.Binary.BinaryReader;
using BinaryWriter = Basalt.Binary.BinaryWriter;

namespace Orion.Protocol.Types;

public interface IStackRequestAction {
    byte ActionType { get; }
    void Read(BinaryReader reader);
    void Write(BinaryWriter writer);
}
