namespace Orion.Region;

/// <summary>
/// Folia-inspired chunk section regionizer (no world IO).
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

            bool sectionWasEmpty = section.IsEmpty;
            section.AddChunk(chunkX, chunkZ);

            if (section.Region is { IsAlive: true } existing && !sectionWasEmpty)
            {
                return existing;
            }

            if (section.Region is { IsAlive: true } sameSectionRegion && sectionWasEmpty)
            {
                // Section re-acquired region somehow — should not happen.
                return sameSectionRegion;
            }

            ChunkRegion region = CreateRegion();
            region.AddSection(section);
            region.SetReady();
            _callbacks.OnRegionActive(region);
            return MergeWithNeighbors(region);
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
                return;
            }

            if (region.State != RegionState.Ticking)
            {
                SplitIfNeeded(region);
            }
        }
    }

    /// <summary>
    /// Marks the region not ticking and flushes deferred merges / splits.
    /// Folia check: growth merges only happen when the region is not ticking.
    /// </summary>
    public void MarkNotTicking(ChunkRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        lock (_sync)
        {
            if (!region.IsAlive)
            {
                return;
            }

            region.MarkNotTicking();
            FlushDeferredMerges(region);
            if (region.IsAlive && region.State != RegionState.Ticking)
            {
                SplitIfNeeded(region);
            }
        }
    }

    public bool TryMarkTicking(ChunkRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        lock (_sync)
        {
            return region.IsAlive && region.TryMarkTicking();
        }
    }

    /// <summary>
    /// Immediately merges <paramref name="from"/> into <paramref name="into"/> when safe.
    /// If <paramref name="into"/> is ticking, schedules a deferred merge instead.
    /// </summary>
    public void KillAndMergeInto(ChunkRegion from, ChunkRegion into)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(into);
        lock (_sync)
        {
            KillAndMergeIntoLocked(from, into);
        }
    }

    private ChunkRegion MergeWithNeighbors(ChunkRegion region)
    {
        foreach (RegionSection section in region.Sections.ToArray())
        {
            foreach (RegionSection neighbor in EnumerateNeighborSections(section.SectionX, section.SectionZ))
            {
                ChunkRegion? other = neighbor.Region;
                if (other is null || !other.IsAlive || ReferenceEquals(other, region))
                {
                    continue;
                }

                if (other.State == RegionState.Ticking)
                {
                    // Folia: ticking region must not grow — defer merge until release.
                    region.ExpectMergeLater(other);
                    continue;
                }

                if (region.State == RegionState.Ticking)
                {
                    other.ExpectMergeLater(region);
                    continue;
                }

                KillAndMergeIntoLocked(region, other);
                return other;
            }
        }

        return region;
    }

    private void KillAndMergeIntoLocked(ChunkRegion from, ChunkRegion into)
    {
        if (!from.IsAlive || !into.IsAlive || ReferenceEquals(from, into))
        {
            return;
        }

        if (from.State == RegionState.Ticking)
        {
            throw new InvalidOperationException("Cannot kill a ticking region.");
        }

        if (into.State == RegionState.Ticking)
        {
            from.ExpectMergeLater(into);
            return;
        }

        _callbacks.PreMerge(from, into);
        into.TakeDataFrom(from);

        foreach (RegionSection section in from.Sections.ToArray())
        {
            from.RemoveSection(section);
            into.AddSection(section);
        }

        from.ClearMergeLater();
        DestroyRegion(from);
    }

    private void FlushDeferredMerges(ChunkRegion region)
    {
        // Regions waiting to merge into this one.
        foreach (ChunkRegion other in _regions.Where(r => r.IsAlive && r.MergeIntoLater == region).ToArray())
        {
            other.ClearMergeLater();
            KillAndMergeIntoLocked(other, region);
        }

        // This region waiting to merge into another.
        ChunkRegion? target = region.MergeIntoLater;
        if (target is { IsAlive: true } && !ReferenceEquals(target, region))
        {
            region.ClearMergeLater();
            if (target.State == RegionState.Ticking)
            {
                region.ExpectMergeLater(target);
            }
            else
            {
                KillAndMergeIntoLocked(region, target);
            }
        }
    }

    private void SplitIfNeeded(ChunkRegion region)
    {
        if (!region.IsAlive || region.State == RegionState.Ticking || region.SectionCount <= 1)
        {
            return;
        }

        List<List<RegionSection>> components = ComputeConnectedComponents(region.Sections);
        if (components.Count <= 1)
        {
            return;
        }

        _callbacks.PreSplit(region);
        _callbacks.OnRegionInactive(region);

        // Keep the first component on the existing region.
        HashSet<RegionSection> keep = components[0].ToHashSet();
        foreach (RegionSection section in region.Sections.ToArray())
        {
            if (!keep.Contains(section))
            {
                region.RemoveSection(section);
            }
        }

        _callbacks.OnRegionActive(region);

        for (int i = 1; i < components.Count; i++)
        {
            ChunkRegion split = CreateRegion();
            foreach (RegionSection section in components[i])
            {
                split.AddSection(section);
            }

            split.SetReady();
            _callbacks.OnRegionActive(split);
            MergeWithNeighbors(split);
        }
    }

    private List<List<RegionSection>> ComputeConnectedComponents(IReadOnlyCollection<RegionSection> sections)
    {
        var remaining = sections.ToHashSet();
        var components = new List<List<RegionSection>>();

        while (remaining.Count > 0)
        {
            RegionSection start = remaining.First();
            var component = new List<RegionSection>();
            var queue = new Queue<RegionSection>();
            queue.Enqueue(start);
            remaining.Remove(start);

            while (queue.Count > 0)
            {
                RegionSection current = queue.Dequeue();
                component.Add(current);

                foreach (RegionSection candidate in remaining.ToArray())
                {
                    if (SectionDistance(current, candidate) <= _options.MergeRadiusSections)
                    {
                        remaining.Remove(candidate);
                        queue.Enqueue(candidate);
                    }
                }
            }

            components.Add(component);
        }

        return components;
    }

    private IEnumerable<RegionSection> EnumerateNeighborSections(int sectionX, int sectionZ)
    {
        int radius = _options.MergeRadiusSections;
        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                if (_sections.TryGetValue((sectionX + dx, sectionZ + dz), out RegionSection? section)
                    && !section.IsEmpty)
                {
                    yield return section;
                }
            }
        }
    }

    private static int SectionDistance(RegionSection a, RegionSection b)
        => Math.Max(Math.Abs(a.SectionX - b.SectionX), Math.Abs(a.SectionZ - b.SectionZ));

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
