namespace Orion.Entity.Animation;

/// <summary>One state node in an animation controller graph.</summary>
public sealed class AnimationControllerState
{
    public required string Name { get; init; }

    public AnimationAction? OnEntry { get; init; }

    public AnimationAction? OnExit { get; init; }

    public AnimationAction? OnTick { get; init; }

    public required IReadOnlyList<AnimationControllerTransition> Transitions { get; init; }
}
