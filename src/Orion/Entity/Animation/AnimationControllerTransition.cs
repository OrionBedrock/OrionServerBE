namespace Orion.Entity.Animation;

/// <summary>Single transition edge in an animation controller graph.</summary>
public sealed class AnimationControllerTransition
{
    public required string TargetState { get; init; }

    public required AnimationCondition Condition { get; init; }
}
