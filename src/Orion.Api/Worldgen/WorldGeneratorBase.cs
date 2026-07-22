namespace Orion.Api.Worldgen;

/// <summary>
/// Stable base for plugin world generators registered via <c>IGeneratorRegistry</c>.
/// Generation is synchronous; register during plugin <c>Load</c> (before world bootstrap freeze).
/// </summary>
public abstract class WorldGeneratorBase
{
    public abstract string Identifier { get; }

    public abstract void Generate(IChunkGenerationContext context, int chunkX, int chunkZ);
}
