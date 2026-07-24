using Orion.Entity.Animation;
using Orion.Region;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.Entity.Traits;

/// <summary>
/// Hosts animation controller instances on an entity.
/// <see cref="Tick"/> is a no-op unless the current thread owns the entity's chunk region (Folia).
/// </summary>
public sealed class AnimationControllerTrait : IEntityTrait
{
    private readonly EntityHandle _entity;
    private readonly AnimationControllerRegistry _registry;
    private readonly Regionizer _regionizer;
    private readonly IAnimationEffectSink _effects;
    private readonly Dictionary<string, AnimationControllerInstance> _instances = new(StringComparer.Ordinal);

    public AnimationControllerTrait(
        EntityHandle entity,
        AnimationControllerRegistry registry,
        Regionizer regionizer,
        IAnimationEffectSink? effects = null)
    {
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _regionizer = regionizer ?? throw new ArgumentNullException(nameof(regionizer));
        _effects = effects ?? NullAnimationEffectSink.Instance;
    }

    public AnimationControllerInstance Attach(string definitionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        if (!_registry.TryGet(definitionId, out AnimationControllerDefinition? definition) || definition is null)
        {
            throw new KeyNotFoundException($"Animation controller '{definitionId}' is not registered.");
        }

        if (_instances.ContainsKey(definitionId))
        {
            throw new InvalidOperationException($"Controller '{definitionId}' is already attached.");
        }

        var instance = new AnimationControllerInstance(definition, _entity, _effects);
        _instances[definitionId] = instance;
        return instance;
    }

    public bool TryGetInstance(string definitionId, out AnimationControllerInstance? instance)
        => _instances.TryGetValue(definitionId, out instance);

    /// <summary>
    /// Advances all attached controllers when called on the owning region tick thread.
    /// </summary>
    public void Tick()
    {
        if (_entity.IsRemoved)
        {
            return;
        }

        if (!RegionOwnership.IsOwnedByCurrentRegion(_regionizer, _entity.ChunkX, _entity.ChunkZ))
        {
            return;
        }

        foreach (AnimationControllerInstance instance in _instances.Values)
        {
            instance.Tick();
        }
    }

    public void OnDetach() => _instances.Clear();
}
