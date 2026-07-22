using Orion.Protocol.Enums;
using Orion.Protocol.Registry;
using Orion.World.Generation;
using Orion.World.Provider;
using ChunkColumn = Orion.World.Chunk.Chunk;

namespace Orion.World.Tests;

[Collection("GeneratorFactory")]
public sealed class LevelDbRoundTripTests
{
    public LevelDbRoundTripTests()
    {
        GeneratorFactory.ResetForTests();
    }

    [Fact]
    public void LevelDb_RoundTrip_PreservesChunk()
    {
        string path = CreateTempDbPath();
        using LevelDbProvider provider = new(path);

        GeneratorFactory.Register("superflat", typeof(TestSuperFlatWorldGenerator));
        Generator generator = GeneratorFactory.Create("superflat");
        ChunkColumn chunk = generator.Generate(DimensionType.Overworld, 4, 8);
        provider.SaveChunk(chunk);

        ChunkColumn? loaded = provider.LoadChunk(DimensionType.Overworld, 4, 8);
        Assert.NotNull(loaded);
        Assert.Equal(4, loaded!.X);
        Assert.Equal(8, loaded.Z);
        Assert.Equal(
            BedrockBlockStates.GrassBlock,
            loaded.GetPermutation(0, -60, 0).NetworkId);
    }

    private static string CreateTempDbPath()
    {
        string path = Path.Combine(Path.GetTempPath(), "orion-world-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
