namespace Orion.Entity.Animation;

/// <summary>Fluent builder for <see cref="AnimationControllerDefinition"/>.</summary>
public sealed class AnimationControllerBuilder
{
    private readonly string _identifier;
    private string? _initialState;
    private readonly Dictionary<string, AnimationControllerStateBuilder> _states = new(StringComparer.Ordinal);

    internal AnimationControllerBuilder(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        _identifier = identifier;
    }

    public AnimationControllerBuilder Initial(string stateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        _initialState = stateName;
        return this;
    }

    public AnimationControllerBuilder State(string name, Action<AnimationControllerStateBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        if (!_states.TryGetValue(name, out AnimationControllerStateBuilder? builder))
        {
            builder = new AnimationControllerStateBuilder(name);
            _states[name] = builder;
        }

        configure(builder);
        return this;
    }

    public AnimationControllerDefinition Build()
    {
        if (_states.Count == 0)
        {
            throw new InvalidOperationException($"Animation controller '{_identifier}' must define at least one state.");
        }

        string initial = _initialState ?? _states.Keys.First();
        if (!_states.ContainsKey(initial))
        {
            throw new InvalidOperationException(
                $"Animation controller '{_identifier}' initial state '{initial}' was not defined.");
        }

        var states = new Dictionary<string, AnimationControllerState>(StringComparer.Ordinal);
        foreach ((string name, AnimationControllerStateBuilder builder) in _states)
        {
            AnimationControllerState state = builder.Build();
            foreach (AnimationControllerTransition transition in state.Transitions)
            {
                if (!_states.ContainsKey(transition.TargetState))
                {
                    throw new InvalidOperationException(
                        $"Animation controller '{_identifier}' state '{name}' transitions to unknown state '{transition.TargetState}'.");
                }
            }

            states[name] = state;
        }

        return new AnimationControllerDefinition
        {
            Identifier = _identifier,
            InitialState = initial,
            States = states,
        };
    }
}

public sealed class AnimationControllerStateBuilder
{
    private readonly string _name;
    private AnimationAction? _onEntry;
    private AnimationAction? _onExit;
    private AnimationAction? _onTick;
    private readonly List<AnimationControllerTransition> _transitions = [];

    internal AnimationControllerStateBuilder(string name) => _name = name;

    public AnimationControllerStateBuilder OnEntry(AnimationAction action)
    {
        _onEntry = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    public AnimationControllerStateBuilder OnExit(AnimationAction action)
    {
        _onExit = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    public AnimationControllerStateBuilder OnTick(AnimationAction action)
    {
        _onTick = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }

    public AnimationControllerStateBuilder When(AnimationCondition condition, string targetState)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetState);
        _transitions.Add(new AnimationControllerTransition
        {
            TargetState = targetState,
            Condition = condition,
        });
        return this;
    }

    internal AnimationControllerState Build()
        => new()
        {
            Name = _name,
            OnEntry = _onEntry,
            OnExit = _onExit,
            OnTick = _onTick,
            Transitions = [.. _transitions],
        };
}

/// <summary>Entry point for building C# animation controllers.</summary>
public static class AnimationController
{
    public static AnimationControllerBuilder Create(string identifier)
        => new(identifier);
}
