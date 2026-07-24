using Orion.Config;

namespace Orion.Network;

public static class AuthPolicy
{
    public static bool AllowsOffline(OrionSectionConfig orion)
        => orion.AllowOfflineDev || !orion.OnlineMode;

    public static Guid CreateOfflineGuid(string username)
    {
        string normalized = username.Trim().ToLowerInvariant();
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("orion:offline:" + normalized));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }

    public static Guid ResolvePlayerUuid(string identityUuid, string? selfSignedId, string username, bool onlineMode)
    {
        if (Guid.TryParse(identityUuid, out Guid parsedIdentity))
        {
            return parsedIdentity;
        }

        if (!string.IsNullOrWhiteSpace(selfSignedId) && Guid.TryParse(selfSignedId, out Guid parsedSelfSigned))
        {
            return parsedSelfSigned;
        }

        if (!onlineMode)
        {
            return CreateOfflineGuid(username);
        }

        return Guid.NewGuid();
    }

    public static string ResolvePlayerXuid(string identityXuid, Guid uuid, bool onlineMode)
    {
        if (onlineMode && !string.IsNullOrWhiteSpace(identityXuid))
        {
            return identityXuid;
        }

        return uuid.ToString("N");
    }
}
