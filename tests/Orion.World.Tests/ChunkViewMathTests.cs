using Orion.World.Chunk;
using Xunit;

namespace Orion.World.Tests;

public sealed class ChunkViewMathTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void SquareToCircle_IsAtLeastChebyshev(int r)
    {
        int circle = ChunkViewMath.SquareToCircle(r);
        Assert.True(circle >= r);
        Assert.InRange(circle, 1, ChunkViewMath.MaxBedrockViewDistance);
    }

    [Fact]
    public void MaxChebyshevForClientCircle_FitsInsideCircle()
    {
        const int clientMax = 12;
        int r = ChunkViewMath.MaxChebyshevForClientCircle(clientMax);
        Assert.True(ChunkViewMath.SquareToCircle(r) <= clientMax);
        if (r < clientMax)
        {
            Assert.True(ChunkViewMath.SquareToCircle(r + 1) > clientMax
                || ChunkViewMath.SquareToCircle(r + 1) > ChunkViewMath.MaxBedrockViewDistance
                || r + 1 > clientMax);
        }
    }

    [Fact]
    public void PublisherRadiusBlocks_IsCircleShifted()
    {
        const int r = 8;
        Assert.Equal((uint)(ChunkViewMath.SquareToCircle(r) << 4), ChunkViewMath.PublisherRadiusBlocks(r));
    }
}
