namespace Orion.Region;

/// <summary>
/// A threaded chunk region owning one or more non-empty sections.
/// </summary>
public sealed class ChunkRegion
{
    private readonly HashSet<RegionSection> _sections = new();
    private int _tickThreadId = -1;
    private ChunkRegion? _mergeIntoLater;

    internal ChunkRegion(long id, IRegionData data)
    {
        Id = id;
        Data = data;
        State = RegionState.Transient;
    }

    public long Id { get; }

    public RegionState State { get; private set; }

    public IRegionData Data { get; private set; }

    public IReadOnlyCollection<RegionSection> Sections => _sections;

    public int SectionCount => _sections.Count;

    public bool IsAlive => State is not RegionState.Dead;

    internal ChunkRegion? MergeIntoLater => _mergeIntoLater;

    internal void SetReady()
    {
        EnsureNotDead();
        if (State == RegionState.Transient)
        {
            State = RegionState.Ready;
        }
    }

    public bool TryMarkTicking()
    {
        if (State != RegionState.Ready)
        {
            return false;
        }

        State = RegionState.Ticking;
        _tickThreadId = Environment.CurrentManagedThreadId;
        return true;
    }

    public void MarkNotTicking()
    {
        if (State != RegionState.Ticking)
        {
            return;
        }

        State = RegionState.Ready;
        _tickThreadId = -1;
    }

    public bool IsCurrentTickThread
        => State == RegionState.Ticking && _tickThreadId == Environment.CurrentManagedThreadId;

    internal void AddSection(RegionSection section)
    {
        EnsureNotDead();
        if (State == RegionState.Ticking)
        {
            throw new InvalidOperationException("Cannot grow a ticking region.");
        }

        _sections.Add(section);
        section.Region = this;
    }

    internal void RemoveSection(RegionSection section)
    {
        _sections.Remove(section);
        if (section.Region == this)
        {
            section.Region = null;
        }
    }

    internal void ExpectMergeLater(ChunkRegion target)
    {
        if (ReferenceEquals(this, target))
        {
            return;
        }

        _mergeIntoLater = target;
    }

    internal void ClearMergeLater() => _mergeIntoLater = null;

    internal void MarkDead()
    {
        State = RegionState.Dead;
        _tickThreadId = -1;
        _mergeIntoLater = null;
        _sections.Clear();
    }

    internal void TakeDataFrom(ChunkRegion other)
    {
        other.Data.MergeInto(Data);
    }

    private void EnsureNotDead()
    {
        if (State == RegionState.Dead)
        {
            throw new InvalidOperationException("Region is dead.");
        }
    }
}
