namespace Orion.Player;

/// <summary>
/// Generic per-player key-value store (no core schema). Mutate on the owning region.
/// </summary>
public sealed class PlayerDataStore
{
    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _longs = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private bool _dirty;

    public bool IsDirty
    {
        get
        {
            lock (_gate)
            {
                return _dirty;
            }
        }
    }

    public void SetString(string key, string value, bool markDirty = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            _longs.Remove(key);
            _strings[key] = value;
            if (markDirty)
            {
                _dirty = true;
            }
        }
    }

    public bool TryGetString(string key, out string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            return _strings.TryGetValue(key, out value);
        }
    }

    public void SetLong(string key, long value, bool markDirty = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            _strings.Remove(key);
            _longs[key] = value;
            if (markDirty)
            {
                _dirty = true;
            }
        }
    }

    public bool TryGetLong(string key, out long value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            return _longs.TryGetValue(key, out value);
        }
    }

    public bool Delete(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            bool removed = _strings.Remove(key) | _longs.Remove(key);
            if (removed)
            {
                _dirty = true;
            }

            return removed;
        }
    }

    public void ClearDirty()
    {
        lock (_gate)
        {
            _dirty = false;
        }
    }

    public void Clear(bool markDirty = true)
    {
        lock (_gate)
        {
            _strings.Clear();
            _longs.Clear();
            if (markDirty)
            {
                _dirty = true;
            }
        }
    }

    public void LoadFromSnapshot(ReadOnlySpan<byte> blob)
    {
        PlayerDataCodec.Decode(blob, this);
        ClearDirty();
    }

    public byte[] TakeSnapshot() => PlayerDataCodec.Encode(this);

    internal IReadOnlyList<KeyValuePair<string, string>> SnapshotStrings()
    {
        lock (_gate)
        {
            return _strings.ToList();
        }
    }

    internal IReadOnlyList<KeyValuePair<string, long>> SnapshotLongs()
    {
        lock (_gate)
        {
            return _longs.ToList();
        }
    }
}
