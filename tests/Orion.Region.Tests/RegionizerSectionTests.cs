using Orion.Region;
using Xunit;

namespace Orion.Region.Tests;

public sealed class RegionizerSectionTests
{
    [Fact]
    public void AddChunk_Isolated_CreatesOneReadyRegion()
    {
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
        ChunkRegion region = regionizer.AddChunk(0, 0);

        Assert.Equal(RegionState.Ready, region.State);
        Assert.Equal(1, regionizer.RegionCount);
        Assert.Same(region, regionizer.GetRegionAt(0, 0));
    }

    [Fact]
    public void AddChunk_SameSection_SharesRegion()
    {
        // shift 4 => section size 16; chunks (0,0) and (1,1) share section (0,0)
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
        ChunkRegion a = regionizer.AddChunk(0, 0);
        ChunkRegion b = regionizer.AddChunk(1, 1);

        Assert.Same(a, b);
        Assert.Equal(1, regionizer.RegionCount);
    }

    [Fact]
    public void AddChunk_DistantSections_CreatesTwoRegions()
    {
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
        ChunkRegion a = regionizer.AddChunk(0, 0);
        // Section (2,0) is far beyond merge for commit 1 (no merge yet) — still separate sections
        ChunkRegion b = regionizer.AddChunk(32, 0);

        Assert.NotSame(a, b);
        Assert.Equal(2, regionizer.RegionCount);
    }

    [Fact]
    public void GridExponent_ChangesSectionGrouping()
    {
        var fine = new Regionizer(RegionizerOptions.FromGridExponent(0));
        ChunkRegion f0 = fine.AddChunk(0, 0);
        ChunkRegion f1 = fine.AddChunk(1, 0);
        Assert.NotSame(f0, f1);
        Assert.Equal(2, fine.RegionCount);

        var coarse = new Regionizer(RegionizerOptions.FromGridExponent(4));
        ChunkRegion c0 = coarse.AddChunk(0, 0);
        ChunkRegion c1 = coarse.AddChunk(1, 0);
        Assert.Same(c0, c1);
        Assert.Equal(1, coarse.RegionCount);
    }

    [Fact]
    public void RemoveChunk_LastInRegion_DestroysRegion()
    {
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
        regionizer.AddChunk(0, 0);
        regionizer.RemoveChunk(0, 0);

        Assert.Equal(0, regionizer.RegionCount);
        Assert.Null(regionizer.GetRegionAt(0, 0));
    }

    [Fact]
    public void AddChunk_Duplicate_Throws()
    {
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(0));
        regionizer.AddChunk(1, 1);
        Assert.Throws<InvalidOperationException>(() => regionizer.AddChunk(1, 1));
    }

    [Fact]
    public void RemoveChunk_Missing_Throws()
    {
        var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(0));
        Assert.Throws<InvalidOperationException>(() => regionizer.RemoveChunk(0, 0));
    }
}
