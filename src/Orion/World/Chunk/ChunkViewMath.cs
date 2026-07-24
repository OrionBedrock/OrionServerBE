using SystemMath = System.Math;

namespace Orion.World.Chunk;

/// <summary>
/// Bedrock client view-distance math (matches Geyser ChunkUtils.squareToCircle).
/// Server streaming stays Chebyshev; the client culls with a circle.
/// </summary>
public static class ChunkViewMath
{
    public const int MaxBedrockViewDistance = 96;

    public static int SquareToCircle(int renderDistance)
    {
        int padded = (int)SystemMath.Ceiling((renderDistance + 1) * SystemMath.Sqrt(2.0));
        return SystemMath.Clamp(padded, 1, MaxBedrockViewDistance);
    }

    public static int MaxChebyshevForClientCircle(int clientMaxCircle)
    {
        int limit = SystemMath.Clamp(clientMaxCircle, 1, MaxBedrockViewDistance);
        int radius = (int)SystemMath.Floor(limit / SystemMath.Sqrt(2.0) - 1.0);
        radius = SystemMath.Clamp(radius, 1, limit);
        while (radius < limit && SquareToCircle(radius + 1) <= limit)
        {
            radius++;
        }

        while (radius > 1 && SquareToCircle(radius) > limit)
        {
            radius--;
        }

        return radius;
    }

    public static uint PublisherRadiusBlocks(int renderDistance) =>
        (uint)(SquareToCircle(renderDistance) << 4);
}
