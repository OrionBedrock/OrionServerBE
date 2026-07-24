using Orion.Region;
using Xunit;

namespace Orion.Region.Tests;

public sealed class RegionizerMergeSplitTests
{
    [Fact]
    public void AdjacentSections_WithinMergeRadius_ShareRegion()
    {
        // shift 0 => each chunk is its own section; merge radius 1 merges neighbors.
        var regionizer = new Regionizer(new RegionizerOptions(sectionChunkShift: 0, mergeRadiusSections: 1));
        ChunkRegion a = regionizer.AddChunk(0, 0);
        ChunkRegion b = regionizer.AddChunk(1, 0);

        Assert.Same(a, b);
        Assert.Equal(1, regionizer.RegionCount);
    }

    [Fact]
    public void DistantSections_RemainSeparate()
    {
        var regionizer = new Regionizer(new RegionizerOptions(sectionChunkShift: 0, mergeRadiusSections: 1));
        ChunkRegion a = regionizer.AddChunk(0, 0);
        ChunkRegion b = regionizer.AddChunk(3, 0);

        Assert.NotSame(a, b);
        Assert.Equal(2, regionizer.RegionCount);
    }

    [Fact]
    public void MergeDeferred_WhileNeighborTicking()
    {
        var regionizer = new Regionizer(new RegionizerOptions(sectionChunkShift: 0, mergeRadiusSections: 1));
        ChunkRegion a = regionizer.AddChunk(0, 0);
        Assert.True(regionizer.TryMarkTicking(a));

        ChunkRegion b = regionizer.AddChunk(1, 0);
        Assert.NotSame(a, b);
        Assert.Equal(2, regionizer.RegionCount);
        Assert.Equal(RegionState.Ticking, a.State);
        Assert.Equal(RegionState.Ready, b.State);
        Assert.Same(a, b.MergeIntoLater);

        regionizer.MarkNotTicking(a);

        Assert.Equal(1, regionizer.RegionCount);
        Assert.Same(a, regionizer.GetRegionAt(0, 0));
        Assert.Same(a, regionizer.GetRegionAt(1, 0));
        Assert.False(b.IsAlive);
    }

    [Fact]
    public void KillAndMergeInto_TickingSource_Throws()
    {
        var regionizer = new Regionizer(new RegionizerOptions(sectionChunkShift: 0, mergeRadiusSections: 1));
        ChunkRegion a = regionizer.AddChunk(0, 0);
        ChunkRegion b = regionizer.AddChunk(5, 5);
        Assert.True(regionizer.TryMarkTicking(a));

        Assert.Throws<InvalidOperationException>(() => regionizer.KillAndMergeInto(a, b));
        Assert.Equal(RegionState.Ticking, a.State);
    }

    [Fact]
    public void RemoveBridge_SplitsIntoTwoRegions()
    {
        var regionizer = new Regionizer(new RegionizerOptions(sectionChunkShift: 0, mergeRadiusSections: 1));
        regionizer.AddChunk(0, 0);
        regionizer.AddChunk(1, 0);
        regionizer.AddChunk(2, 0);
        Assert.Equal(1, regionizer.RegionCount);

        regionizer.RemoveChunk(1, 0);

        Assert.Equal(2, regionizer.RegionCount);
        ChunkRegion? left = regionizer.GetRegionAt(0, 0);
        ChunkRegion? right = regionizer.GetRegionAt(2, 0);
        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.NotSame(left, right);
    }

    [Fact]
    public void TickingRegion_DoesNotSplitUntilReleased()
    {
        var regionizer = new Regionizer(new RegionizerOptions(sectionChunkShift: 0, mergeRadiusSections: 1));
        ChunkRegion region = regionizer.AddChunk(0, 0);
        regionizer.AddChunk(1, 0);
        regionizer.AddChunk(2, 0);
        Assert.True(regionizer.TryMarkTicking(region));

        regionizer.RemoveChunk(1, 0);
        Assert.Equal(1, regionizer.RegionCount);

        regionizer.MarkNotTicking(region);
        Assert.Equal(2, regionizer.RegionCount);
    }
}
