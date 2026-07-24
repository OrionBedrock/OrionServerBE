namespace Orion.Entity;

/// <summary>
/// Marker for traits attached to an <see cref="Entity"/>.
/// </summary>
public interface IEntityTrait
{
    /// <summary>
    /// Called when the owning entity is removed or the trait is detached.
    /// </summary>
    void OnDetach();
}
