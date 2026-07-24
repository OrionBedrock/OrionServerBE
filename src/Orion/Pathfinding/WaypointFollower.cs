using Orion.Region;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.Pathfinding;

/// <summary>
/// Follows an ordered waypoint list with <see cref="EntityHandle.TryMove"/>.
/// Edge skip (foreign region) pauses without advancing the index.
/// </summary>
public sealed class WaypointFollower
{
    private readonly IReadOnlyList<PathPoint> _waypoints;
    private int _index;

    public WaypointFollower(IReadOnlyList<PathPoint> waypoints)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        if (waypoints.Count == 0)
        {
            throw new ArgumentException("Waypoint list must not be empty.", nameof(waypoints));
        }

        _waypoints = waypoints;
    }

    public int Index => _index;

    public bool IsCompleted => _index >= _waypoints.Count;

    public PathPoint? CurrentTarget => IsCompleted ? null : _waypoints[_index];

    /// <summary>
    /// Advances one step toward the current waypoint. Caller must be on owning region tick.
    /// </summary>
    public bool Tick(EntityHandle entity, Regionizer regionizer, double stepDistance, double arriveEpsilon = 0.25)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(regionizer);
        if (stepDistance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stepDistance));
        }

        if (entity.IsRemoved || IsCompleted)
        {
            return false;
        }

        PathPoint target = _waypoints[_index];
        double dx = target.X - entity.X;
        double dy = target.Y - entity.Y;
        double dz = target.Z - entity.Z;
        double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        if (dist <= arriveEpsilon)
        {
            _index++;
            return true;
        }

        double scale = Math.Min(1.0, stepDistance / dist);
        double nextX = entity.X + dx * scale;
        double nextY = entity.Y + dy * scale;
        double nextZ = entity.Z + dz * scale;

        if (!entity.TryMove(regionizer, nextX, nextY, nextZ))
        {
            return false;
        }

        double after = target.DistanceSquaredTo(entity.X, entity.Y, entity.Z);
        if (after <= arriveEpsilon * arriveEpsilon)
        {
            _index++;
        }

        return true;
    }
}
