namespace Orion.Gameplay;

/// <summary>
/// Facade for block placement / item-use-on-block.
/// Resolve via <c>provides: orion:building</c>.
/// </summary>
public interface IBuildingApi
{
    IPlayerBlockUseHandler BlockUse { get; }
}
