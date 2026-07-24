using Orion.Region;
using Xunit;

namespace Orion.Region.Tests;

public sealed class RegionOwnershipTests
{
    [Fact]
    public void EnsureOwned_SucceedsOnOwningThread()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(2, 3);

        using (RegionOwnership.Enter(region))
        {
            Assert.True(RegionOwnership.IsOwnedByCurrentRegion(regionizer, 2, 3));
            RegionOwnership.EnsureOwnedByCurrentRegion(regionizer, 2, 3);
        }
    }

    [Fact]
    public void EnsureOwned_ThrowsOutsideOwnership()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        regionizer.AddChunk(0, 0);

        Assert.False(RegionOwnership.IsOwnedByCurrentRegion(regionizer, 0, 0));
        Assert.Throws<InvalidOperationException>(
            () => RegionOwnership.EnsureOwnedByCurrentRegion(regionizer, 0, 0));
    }

    [Fact]
    public void EnsureOwned_ThrowsForChunkWithoutRegion()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(0, 0);

        using (RegionOwnership.Enter(region))
        {
            Assert.Throws<InvalidOperationException>(
                () => RegionOwnership.EnsureOwnedByCurrentRegion(regionizer, 9, 9));
        }
    }

    [Fact]
    public void TryMarkTickingWithOwnership_BindsThread()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(1, 1);

        using IDisposable? scope = region.TryMarkTickingWithOwnership();
        Assert.NotNull(scope);
        Assert.Equal(RegionState.Ticking, region.State);
        Assert.True(RegionOwnership.IsOwnedByCurrentRegion(regionizer, 1, 1));
    }

    [Fact]
    public async Task EnsureOwned_FailsOnOtherThread()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(4, 4);
        using var entered = RegionOwnership.Enter(region);

        InvalidOperationException? remote = null;
        await Task.Run(() =>
        {
            try
            {
                RegionOwnership.EnsureOwnedByCurrentRegion(regionizer, 4, 4);
            }
            catch (InvalidOperationException ex)
            {
                remote = ex;
            }
        });

        Assert.NotNull(remote);
    }
}
