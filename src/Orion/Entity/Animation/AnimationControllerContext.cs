using EntityHandle = Orion.Entity.Entity;

namespace Orion.Entity.Animation;

/// <summary>Per-tick evaluation context for conditions and actions.</summary>
public sealed class AnimationControllerContext
{
    private readonly Dictionary<string, long> _longs;
    private readonly Dictionary<string, bool> _bools;

    public AnimationControllerContext(
        EntityHandle entity,
        string controllerId,
        string currentState,
        long ticksInState,
        bool justEntered,
        Dictionary<string, long> longs,
        Dictionary<string, bool> bools)
    {
        Entity = entity;
        ControllerId = controllerId;
        CurrentState = currentState;
        TicksInState = ticksInState;
        JustEntered = justEntered;
        _longs = longs;
        _bools = bools;
    }

    public EntityHandle Entity { get; }

    public string ControllerId { get; }

    public string CurrentState { get; }

    public long TicksInState { get; }

    public bool JustEntered { get; }

    public long GetLong(string name, long defaultValue = 0)
        => _longs.TryGetValue(name, out long value) ? value : defaultValue;

    public void SetLong(string name, long value) => _longs[name] = value;

    public bool GetBool(string name, bool defaultValue = false)
        => _bools.TryGetValue(name, out bool value) ? value : defaultValue;

    public void SetBool(string name, bool value) => _bools[name] = value;
}
