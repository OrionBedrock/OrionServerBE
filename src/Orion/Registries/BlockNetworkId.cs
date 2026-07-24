using System.Collections.Concurrent;
using System.Text;

namespace Orion.Registries;

/// <summary>Bedrock block network-id hash (empty states) — same algorithm as Basalt.</summary>
public static class BlockNetworkId
{
    public const uint HashOffset = 0x811C9DC5;

    private static readonly ConcurrentDictionary<string, int> Cache = new(StringComparer.Ordinal);

    public static int Air { get; } = ForIdentifier("minecraft:air");

    public static int ForIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return Cache.GetOrAdd(identifier, static id => HashEmptyStates(id));
    }

    private static int HashEmptyStates(string identifier)
    {
        uint hash = HashOffset;
        HashByte(ref hash, 10);
        HashUInt16(ref hash, 0);

        HashByte(ref hash, 8);
        HashNbtString(ref hash, "name");
        HashNbtString(ref hash, identifier);

        HashByte(ref hash, 10);
        HashNbtString(ref hash, "states");
        HashByte(ref hash, 0);
        HashByte(ref hash, 0);

        return unchecked((int)hash);
    }

    private static void HashNbtString(ref uint hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        HashUInt16(ref hash, checked((ushort)bytes.Length));
        for (int i = 0; i < bytes.Length; i++)
        {
            HashByte(ref hash, bytes[i]);
        }
    }

    private static void HashUInt16(ref uint hash, ushort value)
    {
        HashByte(ref hash, (byte)value);
        HashByte(ref hash, (byte)(value >> 8));
    }

    private static void HashByte(ref uint hash, byte value)
    {
        hash ^= value;
        hash += (hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24);
    }
}
