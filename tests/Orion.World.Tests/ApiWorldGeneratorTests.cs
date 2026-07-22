using Orion.Api.Worldgen;
using Orion.Protocol.Enums;
using Orion.Protocol.Registry;
using Orion.World.Generation;
using ChunkColumn = Orion.World.Chunk.Chunk;

namespace Orion.World.Tests;

[Collection("GeneratorFactory")]
public sealed class ApiWorldGeneratorTests
{
    public ApiWorldGeneratorTests()
    {
        GeneratorFactory.ResetForTests();
    }

    [Fact]
    public void Create_UnknownOrSuperflatWithoutPlugin_ReturnsVoid()
    {
        GeneratorFactory.ResetForTests();
        Assert.IsType<VoidGenerator>(GeneratorFactory.Create("void"));
        Assert.IsType<VoidGenerator>(GeneratorFactory.Create("superflat"));
        Assert.IsType<VoidGenerator>(GeneratorFactory.Create("unknown-gen"));
    }

    [Fact]
    public void Registered_ApiSuperflat_ProducesExpectedLayers()
    {
        GeneratorFactory.ResetForTests();
        GeneratorFactory.Register("superflat", typeof(TestSuperFlatWorldGenerator));
        Generator generator = GeneratorFactory.Create("superflat");

        ChunkColumn chunk = generator.Generate(DimensionType.Overworld, 0, 0);

        Assert.Equal("superflat", generator.Identifier);
        Assert.Equal(BedrockBlockStates.Bedrock, chunk.GetPermutation(0, -64, 0).NetworkId);
        Assert.Equal(BedrockBlockStates.Dirt, chunk.GetPermutation(0, -63, 0).NetworkId);
        Assert.Equal(BedrockBlockStates.Dirt, chunk.GetPermutation(0, -62, 0).NetworkId);
        Assert.Equal(BedrockBlockStates.Dirt, chunk.GetPermutation(0, -61, 0).NetworkId);
        Assert.Equal(BedrockBlockStates.GrassBlock, chunk.GetPermutation(0, -60, 0).NetworkId);
        Assert.False(chunk.Dirty);
    }

    [Fact]
    public void Register_InternalGeneratorType_Throws()
    {
        GeneratorFactory.ResetForTests();
        Assert.Throws<ArgumentException>(() =>
            GeneratorFactory.Register("bad", typeof(VoidGenerator)));
    }
}

/// <summary>Mirrors orion:superflat layer layout for host tests without the plugin assembly.</summary>
internal sealed class TestSuperFlatWorldGenerator : WorldGeneratorBase
{
    private const int BaseY = -64;
    private const int PlainsBiomeId = 1;

    public override string Identifier => "superflat";

    public override void Generate(IChunkGenerationContext context, int chunkX, int chunkZ)
    {
        context.FillLayer(BaseY, "minecraft:bedrock");
        context.SetSubChunkBiome(BaseY, PlainsBiomeId);
        context.FillLayer(BaseY + 1, "minecraft:dirt");
        context.SetSubChunkBiome(BaseY + 1, PlainsBiomeId);
        context.FillLayer(BaseY + 2, "minecraft:dirt");
        context.SetSubChunkBiome(BaseY + 2, PlainsBiomeId);
        context.FillLayer(BaseY + 3, "minecraft:dirt");
        context.SetSubChunkBiome(BaseY + 3, PlainsBiomeId);
        context.FillLayer(BaseY + 4, "minecraft:grass_block");
        context.SetSubChunkBiome(BaseY + 4, PlainsBiomeId);
        context.MarkClean();
    }
}
