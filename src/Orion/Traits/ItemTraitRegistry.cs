namespace Orion.Traits;

/// <summary>Registers item traits by content identifier (no assembly scan).</summary>
public sealed class ItemTraitRegistry
{
    private readonly Dictionary<string, IItemTrait> _traits = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Register(string id, IItemTrait trait)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(trait);
        lock (_gate)
        {
            if (!_traits.TryAdd(id, trait))
            {
                throw new InvalidOperationException($"Item trait '{id}' is already registered.");
            }
        }
    }

    public bool TryGet(string id, out IItemTrait? trait)
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
