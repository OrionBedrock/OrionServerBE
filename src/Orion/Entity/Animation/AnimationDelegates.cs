namespace Orion.Entity.Animation;

/// <summary>Action invoked on state enter/exit/tick.</summary>
public delegate void AnimationAction(AnimationControllerContext context);

/// <summary>Condition for a transition (first match wins).</summary>
public delegate bool AnimationCondition(AnimationControllerContext context);
