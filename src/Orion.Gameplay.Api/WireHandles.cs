namespace Orion.Gameplay;

/// <summary>
/// Wire handle for a Protocol <c>ItemStackRequest</c>. Owner <c>orion:inventory</c> casts <see cref="Value"/>.
/// Rich DTOs land in S6.
/// </summary>
public sealed class ItemStackRequestWire(object value)
{
    public object Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}

/// <summary>
/// Wire handle for a Protocol <c>ItemStackResponse</c>. Owner <c>orion:inventory</c> casts <see cref="Value"/>.
/// </summary>
public sealed class ItemStackResponseWire(object value)
{
    public object Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}

/// <summary>
/// Wire handle for a Protocol <c>FullContainerName</c>. Owner <c>orion:inventory</c> casts <see cref="Value"/>.
/// </summary>
public sealed class ContainerNameWire(object value)
{
    public object Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}
