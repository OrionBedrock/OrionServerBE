using Orion.Api.Worldgen;
using Orion.Protocol.Enums;
using Orion.World.Block;
using Orion.World.Chunk;
using ChunkColumn = Orion.World.Chunk.Chunk;

namespace Orion.World.Generation;

/// <summary>
/// Bridges <see cref="WorldGeneratorBase"/> (plugin SDK) to the internal <see cref="Generator"/> pipeline.
/// </summary>
internal sealed class ApiGeneratorAdapter : Generator
{
    private readonly WorldGeneratorBase _api;

    public ApiGeneratorAdapter(WorldGeneratorBase api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public override string Identifier => _api.Identifier;

    public override ChunkColumn Generate(DimensionType dimensionType, int x, int z)
    {
        ChunkColumn chunk = new(x, z, dimensionType);
        ChunkGenerationContext context = new(chunk);
        _api.Generate(context, x, z);
        return chunk;
    }

    sealed class ChunkGenerationContext(ChunkColumn chunk) : IChunkGenerationContext
    {
        public int DimensionType => (int)chunk.Type;

        public void FillLayer(int y, string blockIdentifier)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(blockIdentifier);

            BlockPermutation permutation = BlockPermutation.Resolve(blockIdentifier);
            SubChunk subChunk = chunk.GetSubChunk(y >> 4);
            BlockStorage storage = subChunk.GetLayer(0);
            int by = y & 0xF;
            int networkId = permutation.NetworkId;

            for (int lx = 0; lx < 16; lx++)
            {
                for (int lz = 0; lz < 16; lz++)
                {
                    storage.SetState(lx, by, lz, networkId);
                }
            }
        }

        public void SetSubChunkBiome(int y, int biomeId)
        {
            SubChunk subChunk = chunk.GetSubChunk(y >> 4);
            subChunk.Biomes = new BiomeStorage([biomeId]);
        }

        public void MarkClean()
        {
            chunk.Dirty = false;
        }
    }
}
