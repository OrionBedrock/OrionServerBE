namespace Orion.Entity;

/// <summary>
/// Per-entity trait bag: <c>GetOrAdd&lt;T&gt;(factory)</c>.
/// </summary>
public sealed class TraitBag
{
    private readonly Entity _entity;
    private readonly Dictionary<Type, IEntityTrait> _traits = new();
    private readonly object _sync = new();

    internal TraitBag(Entity entity)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public T GetOrAdd<T>(Func<Entity, T> factory) where T : class, IEntityTrait
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (_sync)
        {
            if (_traits.TryGetValue(typeof(T), out IEntityTrait? existing))
            {
                return (T)existing;
            }

            T created = factory(_entity)
                ?? throw new InvalidOperationException($"Trait factory for {typeof(T).Name} returned null.");
            _traits[typeof(T)] = created;
            return created;
        }
    }

    public bool TryGet<T>(out T? trait) where T : class, IEntityTrait
    {
        lock (_sync)
        {
            if (_traits.TryGetValue(typeof(T), out IEntityTrait? existing))
            {
                trait = (T)existing;
                return true;
            }

            trait = null;
            return false;
        }
    }

    internal void NotifyChunkPositionChanged(int chunkX, int chunkZ)
    {
        IEntityTrait[] snapshot;
        lock (_sync)
        {
            snapshot = _traits.Values.ToArray();
        }

        foreach (IEntityTrait trait in snapshot)
        {
            if (trait is IChunkPositionAware aware)
            {
                aware.OnChunkPositionChanged(chunkX, chunkZ);
            }
        }
    }

    internal void DetachAll()
    {
        IEntityTrait[] snapshot;
        lock (_sync)
        {
            snapshot = _traits.Values.ToArray();
            _traits.Clear();
        }

        foreach (IEntityTrait trait in snapshot)
        {
            trait.OnDetach();
        }
    }
}

/// <summary>
/// Optional hook when the owning entity changes chunk coordinates.
/// </summary>
public interface IChunkPositionAware
{
    void OnChunkPositionChanged(int chunkX, int chunkZ);
}
