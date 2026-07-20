namespace Orion.Gameplay;

/// <summary>
/// Facade for block crack / destroy.
/// Resolve via <c>provides: orion:mining</c>.
/// </summary>
public interface IMiningApi
{
    IPlayerBlockBreakHandler BlockBreak { get; }
}
