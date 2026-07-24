namespace Orion.Entity.Animation;

/// <summary>String-keyed registry of animation controller definitions.</summary>
public sealed class AnimationControllerRegistry
{
    private readonly Dictionary<string, AnimationControllerDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _definitions.Count;
            }
        }
    }

    public void Register(AnimationControllerDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Identifier);
        lock (_gate)
        {
            if (!_definitions.TryAdd(definition.Identifier, definition))
            {
                throw new InvalidOperationException(
                    $"Animation controller '{definition.Identifier}' is already registered.");
            }
        }
    }

    public bool TryGet(string identifier, out AnimationControllerDefinition? definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        lock (_gate)
        {
            return _definitions.TryGetValue(identifier, out definition);
        }
    }
}
