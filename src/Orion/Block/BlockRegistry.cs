using System.Reflection;
using Orion.Block.Traits;
using Orion.Block.Types;
using Orion.Protocol.Registry;

namespace Orion.Block;

public static class BlockRegistry
{
    private static readonly object LoadLock = new();
    private static bool _loaded;
    private static readonly List<PendingBlockRegistration> PendingPluginBlocks = [];

    public static bool IsLoaded
    {
        get
        {
            lock (LoadLock)
            {
                return _loaded;
            }
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (LoadLock)
        {
            if (_loaded)
            {
                return;
            }

            RegisterFromBedrockStates();
            FlushPendingPluginBlocks();
            BlockTraitRegistry.RegisterFromAssembly(Assembly.GetExecutingAssembly());
            _loaded = true;
        }
    }

    /// <summary>
    /// Queues a plugin block registration until <see cref="EnsureLoaded"/> (must be before freeze).
    /// </summary>
    public static void RegisterPluginBlock(
        string identifier,
        int defaultStateHash,
        bool solid = true,
        bool air = false,
        float hardness = 0f,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<Orion.PluginContracts.Registry.BlockStateDefinition>? states = null,
        IReadOnlyDictionary<string, string>? components = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        lock (LoadLock)
        {
            if (_loaded)
            {
                throw new InvalidOperationException(
                    "Blocks must be registered before BlockRegistry.EnsureLoaded (plugin Load / pre-catalog).");
            }

            PendingPluginBlocks.Add(new PendingBlockRegistration(
                identifier,
                defaultStateHash,
                solid,
                air,
                hardness,
                tags is null ? [] : [.. tags],
                states is null ? [] : [.. states],
                components is null
                    ? []
                    : components.Select(static kv => (kv.Key, kv.Value)).ToArray()));
        }
    }

    internal static void ResetForTests()
    {
        lock (LoadLock)
        {
            _loaded = false;
            PendingPluginBlocks.Clear();
        }
    }

    static void FlushPendingPluginBlocks()
    {
        foreach (PendingBlockRegistration pending in PendingPluginBlocks)
        {
            RegisterBlock(
                pending.Identifier,
                pending.DefaultStateHash,
                air: pending.Air,
                solid: pending.Solid,
                hardness: pending.Hardness);

            BlockType? type = BlockType.Get(pending.Identifier);
            if (type is null)
            {
                continue;
            }

            foreach (string tag in pending.Tags)
            {
                type.EnsureTag(tag);
            }

            foreach (Orion.PluginContracts.Registry.BlockStateDefinition state in pending.States)
            {
                type.EnsureState(state.Name);
            }

            foreach ((string key, string _) in pending.Components)
            {
                type.EnsureComponent(key);
            }
        }

        PendingPluginBlocks.Clear();
    }

    static void RegisterBlock(string identifier, int hash, bool air = false, bool solid = true, float hardness = 0f)
    {
        BlockType type = BlockType.Get(identifier) ?? new BlockType(identifier);
        type.Air = air;
        type.Solid = solid;
        type.Hardness = hardness;
        BlockState state = [];
        BlockPermutation permutation = new(hash, state, type);
        BlockPermutation.Permutations[hash] = permutation;
        type.RegisterPermutation(permutation);
    }

    static void RegisterFromBedrockStates()
    {
        RegisterBlock("minecraft:air", BedrockBlockStates.Air, air: true, solid: false, hardness: 0f);
        RegisterBlock("minecraft:structure_void", BedrockBlockStates.StructureVoid, solid: false, hardness: 0f);
        RegisterBlock("minecraft:bedrock", BedrockBlockStates.Bedrock, hardness: -1f);
        RegisterBlock("minecraft:dirt", BedrockBlockStates.Dirt, hardness: 0.5f);
        RegisterBlock("minecraft:grass_block", BedrockBlockStates.GrassBlock, hardness: 0.6f);
        RegisterBlock("minecraft:barrier", BedrockBlockStates.Barrier, solid: false, hardness: -1f);
    }

    private readonly record struct PendingBlockRegistration(
        string Identifier,
        int DefaultStateHash,
        bool Solid,
        bool Air,
        float Hardness,
        string[] Tags,
        Orion.PluginContracts.Registry.BlockStateDefinition[] States,
        (string Key, string Value)[] Components);
}
