using System.Text.Json;

namespace Orion.Permissions;

public sealed class PermissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly PermissionDocument _document;

    public PermissionService(PermissionDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public PermissionDocument Document => _document;

    public static PermissionService Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Permissions file not found: {fullPath}", fullPath);
        }

        string json = File.ReadAllText(fullPath);
        PermissionDocument? document = JsonSerializer.Deserialize<PermissionDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize permissions: {fullPath}");

        Normalize(document);
        return new PermissionService(document);
    }

    public static PermissionService CreateEmpty()
    {
        var document = new PermissionDocument
        {
            Permissions =
            {
                [PermissionNodes.Command] = true,
                [PermissionNodes.BlockBreak] = false,
                [PermissionNodes.BlockPlace] = false,
            },
            Groups =
            {
                ["default"] = new PermissionGroup { Permissions = [PermissionNodes.Command] },
                ["admin"] = new PermissionGroup
                {
                    Permissions =
                    [
                        PermissionNodes.Command,
                        PermissionNodes.BlockBreak,
                        PermissionNodes.BlockPlace,
                        PermissionNodes.Admin,
                    ],
                },
            },
        };
        return new PermissionService(document);
    }

    public ResolvedPermissions Resolve(string? username, string? xuid)
    {
        bool isOp = IsListedOperator(username, xuid);
        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach ((string node, bool allowed) in _document.Permissions)
        {
            if (allowed)
            {
                granted.Add(node);
            }
        }

        string groupName = isOp ? "admin" : "default";
        PlayerPermissionEntry? playerEntry = FindPlayerEntry(username, xuid);
        if (playerEntry is not null && !string.IsNullOrWhiteSpace(playerEntry.Group))
        {
            groupName = playerEntry.Group;
        }

        if (_document.Groups.TryGetValue(groupName, out PermissionGroup? group))
        {
            foreach (string node in group.Permissions)
            {
                if (!string.IsNullOrWhiteSpace(node))
                {
                    granted.Add(node);
                }
            }
        }

        if (playerEntry is not null)
        {
            foreach (string node in playerEntry.Permissions)
            {
                if (!string.IsNullOrWhiteSpace(node))
                {
                    granted.Add(node);
                }
            }
        }

        if (granted.Contains(PermissionNodes.Admin))
        {
            isOp = true;
        }

        return new ResolvedPermissions(isOp, granted);
    }

    public bool IsListedOperator(string? username, string? xuid)
    {
        foreach (string op in _document.Ops)
        {
            if (string.IsNullOrWhiteSpace(op))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(xuid)
                && string.Equals(op, xuid, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(username)
                && string.Equals(op, username, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private PlayerPermissionEntry? FindPlayerEntry(string? username, string? xuid)
    {
        if (!string.IsNullOrWhiteSpace(xuid)
            && _document.Players.TryGetValue(xuid, out PlayerPermissionEntry? byXuid))
        {
            return byXuid;
        }

        if (!string.IsNullOrWhiteSpace(username)
            && _document.Players.TryGetValue(username, out PlayerPermissionEntry? byName))
        {
            return byName;
        }

        // Case-insensitive username scan (dictionary comparer may already be ordinal-ignore).
        if (!string.IsNullOrWhiteSpace(username))
        {
            foreach ((string key, PlayerPermissionEntry entry) in _document.Players)
            {
                if (string.Equals(key, username, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
        }

        return null;
    }

    private static void Normalize(PermissionDocument document)
    {
        document.Ops ??= [];
        document.Permissions ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        document.Groups ??= new Dictionary<string, PermissionGroup>(StringComparer.OrdinalIgnoreCase);
        document.Players ??= new Dictionary<string, PlayerPermissionEntry>(StringComparer.OrdinalIgnoreCase);
    }
}
