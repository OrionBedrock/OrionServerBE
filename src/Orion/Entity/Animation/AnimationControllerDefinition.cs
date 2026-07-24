namespace Orion.Entity.Animation;

/// <summary>Immutable server-side animation controller definition.</summary>
public sealed class AnimationControllerDefinition
{
    public required string Identifier { get; init; }

    public required string InitialState { get; init; }

    public required IReadOnlyDictionary<string, AnimationControllerState> States { get; init; }

    public AnimationControllerState GetState(string name)
    {
        if (!States.TryGetValue(name, out AnimationControllerState? state))
        {
            throw new KeyNotFoundException($"Animation controller '{Identifier}' has no state '{name}'.");
        }

        return state;
    }
}
