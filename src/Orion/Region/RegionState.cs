namespace Orion.Region;

/// <summary>
/// Folia-style region lifecycle states.
/// </summary>
public enum RegionState
{
    Transient = 0,
    Ready = 1,
    Ticking = 2,
    Dead = 3,
}
