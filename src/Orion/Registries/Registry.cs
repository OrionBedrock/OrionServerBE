namespace Orion.Registries;

/// <summary>Generic string-keyed registry. Fail-fast on duplicate ids.</summary>
public sealed class Registry<TValue>
{
    private readonly Dictionary<string, TValue> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public void Register(string id, TValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            if (!_entries.TryAdd(id, value))
            {
                throw new InvalidOperationException($"Registry already contains '{id}'.");
            }
        }
    }

    public bool TryGet(string id, out TValue? value)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(id, out value);
        }
    }

    public bool Contains(string id)
    {
        lock (_gate)
        {
            return _entries.ContainsKey(id);
        }
    }

    public IReadOnlyList<KeyValuePair<string, TValue>> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToList();
        }
    }
}
