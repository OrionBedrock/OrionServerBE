using EntityHandle = Orion.Entity.Entity;
using Orion.Entity;

namespace Orion.Traits;

/// <summary>Factory for entity traits keyed by identifier (plugins bind later).</summary>
public delegate IEntityTrait EntityTraitFactory(EntityHandle entity);

/// <summary>
/// Registers entity trait factories by identifier. No assembly scan — plugins call Register explicitly.
/// Instance attachment remains on <see cref="TraitBag"/>.
/// </summary>
public sealed class EntityTraitRegistry
{
    private readonly Dictionary<string, EntityTraitFactory> _factories = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Register(string id, EntityTraitFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            if (!_factories.TryAdd(id, factory))
            {
                throw new InvalidOperationException($"Entity trait '{id}' is already registered.");
            }
        }
    }

    public bool TryGet(string id, out EntityTraitFactory? factory)
    {
        lock (_gate)
        {
            return _factories.TryGetValue(id, out factory);
        }
    }

    public bool Contains(string id)
    {
        lock (_gate)
        {
            return _factories.ContainsKey(id);
        }
    }
}
