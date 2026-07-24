namespace Orion.Region;

/// <summary>
/// Folia-inspired chunk section regionizer (no world IO).
/// Commit 1: section membership + lookup. Merge/split land in follow-up commits.
/// </summary>
public sealed class Regionizer
{
    private readonly RegionizerOptions _options;
    private readonly IRegionizerCallbacks _callbacks;
    private readonly Dictionary<(int X, int Z), RegionSection> _sections = new();
    private readonly HashSet<ChunkRegion> _regions = new();
    private long _nextRegionId = 1;
    private readonly object _sync = new();

    public Regionizer(RegionizerOptions options, IRegionizerCallbacks? callbacks = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _callbacks = callbacks ?? NullRegionizerCallbacks.Instance;
    }

    public RegionizerOptions Options => _options;

    public int RegionCount
    {
        get
        {
            lock (_sync)
            {
                return _regions.Count(static r => r.IsAlive);
            }
        }
    }

    public IReadOnlyCollection<ChunkRegion> SnapshotRegions()
    {
        lock (_sync)
        {
            return _regions.Where(static r => r.IsAlive).ToArray();
        }
    }

    public ChunkRegion? GetRegionAt(int chunkX, int chunkZ)
    {
        lock (_sync)
        {
            var (sx, sz) = _options.ToSection(chunkX, chunkZ);
            return _sections.TryGetValue((sx, sz), out RegionSection? section) ? section.Region : null;
        }
    }

    public ChunkRegion AddChunk(int chunkX, int chunkZ)
    {
        lock (_sync)
        {
            var (sx, sz) = _options.ToSection(chunkX, chunkZ);
            if (!_sections.TryGetValue((sx, sz), out RegionSection? section))
            {
                section = new RegionSection(sx, sz);
                _sections[(sx, sz)] = section;
            }

            if (section.ContainsChunk(chunkX, chunkZ))
            {
                throw new InvalidOperationException($"Chunk ({chunkX},{chunkZ}) is already loaded in the regionizer.");
            }

            section.AddChunk(chunkX, chunkZ);

            if (section.Region is { IsAlive: true } existing)
            {
                return existing;
            }

            ChunkRegion region = CreateRegion();
            region.AddSection(section);
            region.SetReady();
            _callbacks.OnRegionActive(region);
            return region;
        }
    }

    public void RemoveChunk(int chunkX, int chunkZ)
    {
        lock (_sync)
        {
            var (sx, sz) = _options.ToSection(chunkX, chunkZ);
            if (!_sections.TryGetValue((sx, sz), out RegionSection? section)
                || !section.ContainsChunk(chunkX, chunkZ))
            {
                throw new InvalidOperationException($"Chunk ({chunkX},{chunkZ}) is not present in the regionizer.");
            }

            section.RemoveChunk(chunkX, chunkZ);
            if (!section.IsEmpty)
            {
                return;
            }

            ChunkRegion? region = section.Region;
            if (region is null)
            {
                _sections.Remove((sx, sz));
                return;
            }

            region.RemoveSection(section);
            _sections.Remove((sx, sz));

            if (region.SectionCount == 0)
            {
                DestroyRegion(region);
            }
        }
    }

    private ChunkRegion CreateRegion()
    {
        var region = new ChunkRegion(_nextRegionId++, _callbacks.CreateNewData());
        _regions.Add(region);
        _callbacks.OnRegionCreate(region);
        return region;
    }

    private void DestroyRegion(ChunkRegion region)
    {
        if (!region.IsAlive)
        {
            return;
        }

        _callbacks.OnRegionInactive(region);
        _callbacks.OnRegionDestroy(region);
        region.MarkDead();
        _regions.Remove(region);
    }
}
