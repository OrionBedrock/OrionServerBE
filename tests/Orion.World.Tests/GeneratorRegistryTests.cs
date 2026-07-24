using Orion.World.Chunk;
using Orion.World.Generation;
using Xunit;

namespace Orion.World.Tests;

public sealed class GeneratorRegistryTests
{
    [Fact]
    public void CreateDefault_ResolvesVoid()
    {
        GeneratorRegistry registry = GeneratorRegistry.CreateDefault();
        IChunkGenerator generator = registry.Get(VoidGenerator.Id);
        Assert.Equal(VoidGenerator.Id, generator.Identifier);
        Assert.True(registry.IsRegistered("void"));
    }

    [Fact]
    public void Get_UnknownId_ThrowsClearError()
    {
        GeneratorRegistry registry = GeneratorRegistry.CreateDefault();
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => registry.Get("flat"));
        Assert.Contains("flat", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Register_CustomGenerator_IsResolvable()
    {
        var registry = new GeneratorRegistry();
        registry.Register(new StubGenerator("stub"));
        Assert.Equal("stub", registry.Get("stub").Identifier);
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        GeneratorRegistry registry = GeneratorRegistry.CreateDefault();
        Assert.Throws<InvalidOperationException>(() => registry.Register(new VoidGenerator()));
    }

    private sealed class StubGenerator(string id) : IChunkGenerator
    {
        public string Identifier { get; } = id;

        public void Generate(ChunkColumn chunk) => chunk.IsGenerated = true;
    }
}
