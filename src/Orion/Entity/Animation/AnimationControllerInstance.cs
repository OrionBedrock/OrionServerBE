using EntityHandle = Orion.Entity.Entity;

namespace Orion.Entity.Animation;

/// <summary>Runtime instance of one animation controller attached to an entity.</summary>
public sealed class AnimationControllerInstance
{
    private readonly AnimationControllerDefinition _definition;
    private readonly EntityHandle _entity;
    private readonly Dictionary<string, long> _longs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _bools = new(StringComparer.Ordinal);
    private AnimationControllerState _state;
    private long _ticksInState;
    private bool _justEntered = true;

    public AnimationControllerInstance(AnimationControllerDefinition definition, EntityHandle entity)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _state = definition.GetState(definition.InitialState);
    }

    public string Identifier => _definition.Identifier;

    public string CurrentState => _state.Name;

    public long TicksInState => _ticksInState;

    public void Tick()
    {
        AnimationControllerContext context = CreateContext();

        if (_justEntered)
        {
            _state.OnEntry?.Invoke(context);
            _justEntered = false;
            context = CreateContext();
        }

        _state.OnTick?.Invoke(context);
        context = CreateContext();

        for (int i = 0; i < _state.Transitions.Count; i++)
        {
            AnimationControllerTransition transition = _state.Transitions[i];
            if (!transition.Condition(context))
            {
                continue;
            }

            _state.OnExit?.Invoke(context);
            _state = _definition.GetState(transition.TargetState);
            _ticksInState = 0;
            _justEntered = true;
            return;
        }

        _ticksInState++;
    }

    private AnimationControllerContext CreateContext()
        => new(
            _entity,
            Identifier,
            _state.Name,
            _ticksInState,
            _justEntered,
            _longs,
            _bools);
}
