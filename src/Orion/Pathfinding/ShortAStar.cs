namespace Orion.Pathfinding;

/// <summary>
/// Short horizontal A* on already-loaded, region-owned chunks (cap ~32 nodes).
/// Does not load chunks or cross foreign regions.
/// </summary>
public static class ShortAStar
{
    public const int DefaultMaxNodes = 32;

    public static bool TryFind(
        string dimensionId,
        PathPoint from,
        PathPoint to,
        IWalkabilityProbe probe,
        out IReadOnlyList<PathPoint> path,
        int maxNodes = DefaultMaxNodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionId);
        ArgumentNullException.ThrowIfNull(probe);
        if (maxNodes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNodes));
        }

        int startX = (int)Math.Floor(from.X);
        int startZ = (int)Math.Floor(from.Z);
        int goalX = (int)Math.Floor(to.X);
        int goalZ = (int)Math.Floor(to.Z);
        int y = (int)Math.Floor(from.Y);

        if (!probe.IsWalkable(dimensionId, startX, y, startZ)
            || !probe.IsWalkable(dimensionId, goalX, y, goalZ))
        {
            path = [];
            return false;
        }

        if (startX == goalX && startZ == goalZ)
        {
            path = [new PathPoint(goalX + 0.5, to.Y, goalZ + 0.5)];
            return true;
        }

        var open = new PriorityQueue<(int X, int Z), int>();
        var cameFrom = new Dictionary<(int X, int Z), (int X, int Z)>();
        var gScore = new Dictionary<(int X, int Z), int> { [(startX, startZ)] = 0 };
        var closed = new HashSet<(int X, int Z)>();

        open.Enqueue((startX, startZ), Heuristic(startX, startZ, goalX, goalZ));
        int expansions = 0;

        while (open.Count > 0)
        {
            (int X, int Z) current = open.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }

            expansions++;
            if (expansions > maxNodes)
            {
                path = [];
                return false;
            }

            if (current.X == goalX && current.Z == goalZ)
            {
                path = Reconstruct(cameFrom, current, to.Y);
                return true;
            }

            foreach ((int nx, int nz) in Neighbors(current.X, current.Z))
            {
                if (closed.Contains((nx, nz)))
                {
                    continue;
                }

                if (!probe.IsWalkable(dimensionId, nx, y, nz))
                {
                    continue;
                }

                int tentative = gScore[current] + 1;
                if (gScore.TryGetValue((nx, nz), out int existing) && tentative >= existing)
                {
                    continue;
                }

                cameFrom[(nx, nz)] = current;
                gScore[(nx, nz)] = tentative;
                int f = tentative + Heuristic(nx, nz, goalX, goalZ);
                open.Enqueue((nx, nz), f);
            }
        }

        path = [];
        return false;
    }

    private static int Heuristic(int x, int z, int gx, int gz)
        => Math.Abs(x - gx) + Math.Abs(z - gz);

    private static IEnumerable<(int X, int Z)> Neighbors(int x, int z)
    {
        yield return (x + 1, z);
        yield return (x - 1, z);
        yield return (x, z + 1);
        yield return (x, z - 1);
    }

    private static List<PathPoint> Reconstruct(
        Dictionary<(int X, int Z), (int X, int Z)> cameFrom,
        (int X, int Z) current,
        double y)
    {
        var blocks = new List<(int X, int Z)> { current };
        while (cameFrom.TryGetValue(current, out (int X, int Z) prev))
        {
            current = prev;
            blocks.Add(current);
        }

        blocks.Reverse();
        var points = new List<PathPoint>(blocks.Count);
        foreach ((int bx, int bz) in blocks)
        {
            points.Add(new PathPoint(bx + 0.5, y, bz + 0.5));
        }

        return points;
    }
}
