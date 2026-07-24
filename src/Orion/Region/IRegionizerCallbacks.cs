namespace Orion.Region;

/// <summary>
/// Optional per-region payload. Split/Merge stubs for later world data (Phase 07+).
/// </summary>
public interface IRegionData
{
    void MergeInto(IRegionData target);

    IRegionData Split();
}

public sealed class NullRegionData : IRegionData
{
    public static NullRegionData Instance { get; } = new();

    public void MergeInto(IRegionData target)
    {
    }

    public IRegionData Split() => Instance;
}

/// <summary>
/// Folia-style callbacks. Must not perform IO or mutate world state.
/// </summary>
public interface IRegionizerCallbacks
{
    IRegionData CreateNewData();

    void OnRegionCreate(ChunkRegion region);

    void OnRegionDestroy(ChunkRegion region);

    void OnRegionActive(ChunkRegion region);

    void OnRegionInactive(ChunkRegion region);

    void PreMerge(ChunkRegion from, ChunkRegion into);

    void PreSplit(ChunkRegion region);
}

public sealed class NullRegionizerCallbacks : IRegionizerCallbacks
{
    public static NullRegionizerCallbacks Instance { get; } = new();

    public IRegionData CreateNewData() => NullRegionData.Instance;

    public void OnRegionCreate(ChunkRegion region)
    {
    }

    public void OnRegionDestroy(ChunkRegion region)
    {
    }

    public void OnRegionActive(ChunkRegion region)
    {
    }

    public void OnRegionInactive(ChunkRegion region)
    {
    }

    public void PreMerge(ChunkRegion from, ChunkRegion into)
    {
    }

    public void PreSplit(ChunkRegion region)
    {
    }
}
