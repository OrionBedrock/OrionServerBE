namespace Orion.Pathfinding;

/// <summary>Walkability probe for short A* (no GetBlock yet — loaded + owned).</summary>
public interface IWalkabilityProbe
{
    bool IsWalkable(string dimensionId, int blockX, int blockY, int blockZ);
}
