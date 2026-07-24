namespace Orion.Traits;

/// <summary>Registers block traits by content identifier (no assembly scan).</summary>
public sealed class BlockTraitRegistry
{
    private readonly Dictionary<string, IBlockTrait> _traits = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Register(string id, IBlockTrait trait)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(trait);
        lock (_gate)
        {
            if (!_traits.TryAdd(id, trait))
            {
                throw new InvalidOperationException($"Block trait '{id}' is already registered.");
            }
        }
    }

    public bool TryGet(string id, out IBlockTrait? trait)
    {
        lock (_gate)
        {
            return _traits.TryGetValue(id, out trait);
        }
    }

    public bool Contains(string id)
    {
        lock (_gate)
        {
            return _traits.ContainsKey(id);
        }
    }
}
