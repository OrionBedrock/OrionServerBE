using System.Text.Json.Serialization;

namespace Orion.Permissions;

public sealed class PermissionDocument
{
    [JsonPropertyName("ops")]
    public List<string> Ops { get; set; } = [];

    [JsonPropertyName("permissions")]
    public Dictionary<string, bool> Permissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("groups")]
    public Dictionary<string, PermissionGroup> Groups { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("players")]
    public Dictionary<string, PlayerPermissionEntry> Players { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PermissionGroup
{
    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = [];
}

public sealed class PlayerPermissionEntry
{
    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = [];
}

public static class PermissionNodes
{
    public const string Command = "orion.command";
    public const string BlockBreak = "orion.block.break";
    public const string BlockPlace = "orion.block.place";
    public const string Admin = "orion.admin";
}

/// <summary>
/// Effective permission snapshot for one player session.
/// </summary>
public sealed class ResolvedPermissions
{
    private readonly HashSet<string> _nodes;

    public ResolvedPermissions(bool isOperator, IEnumerable<string> nodes)
    {
        IsOperator = isOperator;
        _nodes = new HashSet<string>(nodes, StringComparer.OrdinalIgnoreCase);
        if (IsOperator)
        {
            _nodes.Add(PermissionNodes.Admin);
            _nodes.Add(PermissionNodes.BlockBreak);
            _nodes.Add(PermissionNodes.BlockPlace);
            _nodes.Add(PermissionNodes.Command);
        }
    }

    public bool IsOperator { get; }

    public bool Has(string node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(node);
        if (IsOperator)
        {
            return true;
        }

        return _nodes.Contains(node);
    }
}
