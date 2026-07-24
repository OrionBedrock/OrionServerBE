using Orion.Protocol.Nbt;
using Orion.Protocol.Types;
using Orion.Traits;

namespace Orion.Registries;

/// <summary>Core content registries — immutable after <see cref="CreateMinimal"/>.</summary>
public sealed class ServerRegistries
{
    public const string Air = "minecraft:air";
    public const string Dirt = "minecraft:dirt";
    public const string GrassBlock = "minecraft:grass_block";
    public const string Cobblestone = "minecraft:cobblestone";
    public const string Bedrock = "minecraft:bedrock";
    public const string Barrier = "minecraft:barrier";
    public const string StructureVoid = "minecraft:structure_void";
    public const string WoodenSword = "minecraft:wooden_sword";

    /// <summary>The seven roadmap ids (excludes air).</summary>
    public static readonly string[] MinimalContentIds =
    [
        Dirt,
        GrassBlock,
        Cobblestone,
        Bedrock,
        Barrier,
        StructureVoid,
        WoodenSword,
    ];

    private static readonly string[] MinimalBlockIds =
    [
        Dirt,
        GrassBlock,
        Cobblestone,
        Bedrock,
        Barrier,
        StructureVoid,
    ];

    public Registry<BlockRegistration> Blocks { get; } = new();

    public Registry<ItemRegistration> Items { get; } = new();

    public BlockTraitRegistry BlockTraits { get; } = new();

    public ItemTraitRegistry ItemTraits { get; } = new();

    public EntityTraitRegistry EntityTraits { get; } = new();

    public static ServerRegistries CreateMinimal()
    {
        var registries = new ServerRegistries();
        registries.Blocks.Register(Air, new BlockRegistration(Air, BlockNetworkId.Air));
        foreach (string blockId in MinimalBlockIds)
        {
            registries.Blocks.Register(blockId, new BlockRegistration(blockId, BlockNetworkId.ForIdentifier(blockId)));
        }

        short nextItemId = 0;
        registries.Items.Register(Air, new ItemRegistration(Air, nextItemId++));
        foreach (string blockId in MinimalBlockIds)
        {
            registries.Items.Register(blockId, new ItemRegistration(blockId, nextItemId++));
        }

        registries.Items.Register(WoodenSword, new ItemRegistration(WoodenSword, nextItemId));
        return registries;
    }

    public List<BlockEntry> ToBlockEntries()
    {
        var entries = new List<BlockEntry>(Blocks.Count);
        foreach (KeyValuePair<string, BlockRegistration> pair in Blocks.Snapshot())
        {
            if (string.Equals(pair.Key, Air, StringComparison.Ordinal))
            {
                continue;
            }

            entries.Add(new BlockEntry
            {
                Name = pair.Value.Id,
                Properties = new CompoundTag(),
            });
        }

        return entries;
    }

    public List<ItemEntry> ToItemEntries()
    {
        var entries = new List<ItemEntry>(Items.Count);
        foreach (KeyValuePair<string, ItemRegistration> pair in Items.Snapshot())
        {
            entries.Add(new ItemEntry
            {
                Name = pair.Value.Id,
                RuntimeId = pair.Value.RuntimeId,
                ComponentBased = false,
                Version = 0,
                Data = new CompoundTag(),
            });
        }

        return entries;
    }
}
