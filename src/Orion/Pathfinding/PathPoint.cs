namespace Orion.Pathfinding;

/// <summary>World-space waypoint for path helpers.</summary>
public readonly record struct PathPoint(double X, double Y, double Z)
{
    public double DistanceSquaredTo(double x, double y, double z)
    {
        double dx = X - x;
        double dy = Y - y;
        double dz = Z - z;
        return dx * dx + dy * dy + dz * dz;
    }

    public double HorizontalDistanceTo(double x, double z)
    {
        double dx = X - x;
        double dz = Z - z;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
