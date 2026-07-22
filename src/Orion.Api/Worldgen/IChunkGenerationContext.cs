namespace Orion.Api.Worldgen;

/// <summary>
/// Host-backed chunk write surface for plugin world generators.
/// DimensionType is the numeric Protocol DimensionType value (Overworld = 0).
/// </summary>
public interface IChunkGenerationContext
{
    int DimensionType { get; }

    void FillLayer(int y, string blockIdentifier);

    void SetSubChunkBiome(int y, int biomeId);

    void MarkClean();
}
