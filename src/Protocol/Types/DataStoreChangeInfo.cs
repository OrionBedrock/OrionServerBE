using Orion.Protocol.Enums;

namespace Orion.Protocol.Types;

public abstract class DataStoreChangeInfo {
    public abstract DataStoreChangeAction Action { get; }
    public abstract string DataStoreName { get; set; }

    public abstract void Read(Binary.BinaryReader reader);
    public abstract void Write(Binary.BinaryWriter writer);
}
