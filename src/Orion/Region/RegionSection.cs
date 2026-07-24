namespace Orion.Region;

/// <summary>
/// A grid section holding zero or more loaded chunk coordinates.
/// </summary>
public sealed class RegionSection
{
    private readonly HashSet<(int X, int Z)> _chunks = new();

    public RegionSection(int sectionX, int sectionZ)
    {
        SectionX = sectionX;
        SectionZ = sectionZ;
    }

    public int SectionX { get; }
    public int SectionZ { get; }

    public ChunkRegion? Region { get; internal set; }

    public int ChunkCount => _chunks.Count;

    public bool IsEmpty => _chunks.Count == 0;

    public IReadOnlyCollection<(int X, int Z)> Chunks => _chunks;

    public bool ContainsChunk(int chunkX, int chunkZ) => _chunks.Contains((chunkX, chunkZ));

    internal bool AddChunk(int chunkX, int chunkZ) => _chunks.Add((chunkX, chunkZ));

    internal bool RemoveChunk(int chunkX, int chunkZ) => _chunks.Remove((chunkX, chunkZ));
}
