using System.Text;

namespace Orion.Player;

/// <summary>Binary codec for <see cref="PlayerDataStore"/> blobs (versioned, no NBT).</summary>
public static class PlayerDataCodec
{
    public const byte Version = 1;

    private const byte TypeString = 1;
    private const byte TypeLong = 2;

    public static byte[] Encode(PlayerDataStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        writer.Write(Version);

        IReadOnlyList<KeyValuePair<string, string>> strings = store.SnapshotStrings();
        IReadOnlyList<KeyValuePair<string, long>> longs = store.SnapshotLongs();
        writer.Write(strings.Count + longs.Count);

        foreach (KeyValuePair<string, string> pair in strings)
        {
            writer.Write(TypeString);
            writer.Write(pair.Key);
            writer.Write(pair.Value);
        }

        foreach (KeyValuePair<string, long> pair in longs)
        {
            writer.Write(TypeLong);
            writer.Write(pair.Key);
            writer.Write(pair.Value);
        }

        writer.Flush();
        return ms.ToArray();
    }

    public static void Decode(ReadOnlySpan<byte> blob, PlayerDataStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (blob.IsEmpty)
        {
            return;
        }

        var reader = new BinaryReader(new MemoryStream(blob.ToArray()), Encoding.UTF8, leaveOpen: false);
        byte version = reader.ReadByte();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported player data blob version {version}.");
        }

        int count = reader.ReadInt32();
        if (count < 0)
        {
            throw new InvalidDataException("Negative entry count in player data blob.");
        }

        store.Clear(markDirty: false);
        for (int i = 0; i < count; i++)
        {
            byte type = reader.ReadByte();
            string key = reader.ReadString();
            switch (type)
            {
                case TypeString:
                    store.SetString(key, reader.ReadString(), markDirty: false);
                    break;
                case TypeLong:
                    store.SetLong(key, reader.ReadInt64(), markDirty: false);
                    break;
                default:
                    throw new InvalidDataException($"Unknown player data entry type {type}.");
            }
        }
    }
}
