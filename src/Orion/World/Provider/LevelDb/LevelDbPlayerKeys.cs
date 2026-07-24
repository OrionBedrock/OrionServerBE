using System.Text;

namespace Orion.World.Provider.LevelDb;

internal static class LevelDbPlayerKeys
{
    // Distinct from chunk tag 0x4F ('O').
    private const byte TagOrionPlayer = 0x50; // 'P'

    public static byte[] BuildPlayerKey(string xuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xuid);
        byte[] xuidBytes = Encoding.UTF8.GetBytes(xuid);
        if (xuidBytes.Length > byte.MaxValue)
        {
            throw new ArgumentException("XUID is too long for LevelDB key.", nameof(xuid));
        }

        byte[] key = new byte[1 + 1 + xuidBytes.Length];
        key[0] = TagOrionPlayer;
        key[1] = (byte)xuidBytes.Length;
        xuidBytes.CopyTo(key.AsSpan(2));
        return key;
    }
}
