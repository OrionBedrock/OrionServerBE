namespace Orion.Region;

public sealed class RegionizerOptions
{
    public RegionizerOptions(int sectionChunkShift, int mergeRadiusSections = 1, int createRadiusSections = 0)
    {
        SectionChunkShift = Math.Clamp(sectionChunkShift, 0, 31);
        MergeRadiusSections = Math.Max(0, mergeRadiusSections);
        CreateRadiusSections = Math.Max(0, createRadiusSections);
    }

    public int SectionChunkShift { get; }

    public int SectionSizeChunks => 1 << SectionChunkShift;

    /// <summary>
    /// Chebyshev distance in section coordinates within which non-empty sections merge.
    /// </summary>
    public int MergeRadiusSections { get; }

    public int CreateRadiusSections { get; }

    public static RegionizerOptions FromGridExponent(int gridExponent, int mergeRadiusSections = 1)
        => new(Math.Clamp(gridExponent, 0, 31), mergeRadiusSections);

    public (int SectionX, int SectionZ) ToSection(int chunkX, int chunkZ)
        => (chunkX >> SectionChunkShift, chunkZ >> SectionChunkShift);
}
