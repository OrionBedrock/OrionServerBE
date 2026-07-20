using System.Reflection;
using Orion.Item.Traits;
using Orion.Protocol.Nbt;
using Orion.Protocol.Registry;
using Orion.Config;
using Log = Orion.Logger.Logger;

namespace Orion.Item;

public static class ItemRegistry
{
    private static readonly object LoadLock = new();
    private static bool _loaded;

    private static Dictionary<uint, ItemStack>? _creativeItems;
    private static HashSet<string> _giveableIdentifiers = new(StringComparer.Ordinal);

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

            Block.BlockRegistry.EnsureLoaded();
            CuratedItemCatalog.EnsureInitialized();

            _giveableIdentifiers = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyCollection<string> allowlist = CuratedItemCatalog.GetAllowlistedIdentifiers();
            bool restrictGive = allowlist.Count > 0;

            foreach (string identifier in CuratedItemCatalog.GetRegisteredIdentifiers())
            {
                if (!CuratedItemCatalog.TryGetByIdentifier(identifier, out CuratedItem curated))
                {
                    continue;
                }

                if (!restrictGive || allowlist.Contains(identifier))
                {
                    _giveableIdentifiers.Add(identifier);
                }

                if (ItemType.Get(identifier) is not null)
                {
                    continue;
                }

                // Only materialize ItemType for allowlisted / fallback curated entries to keep
                // server item space aligned with Orion policy (vanilla palette stays on the wire).
                if (restrictGive && !allowlist.Contains(identifier))
                {
                    continue;
                }

                _ = new ItemType(
                    curated.Identifier,
                    curated.NetworkId,
                    maxStackSize: ResolveMaxStack(curated),
                    tags: null,
                    isComponentBased: curated.ComponentBased,
                    version: curated.ItemVersion,
                    properties: curated.PropertiesNbt);
            }

            _ = ItemType.GetOrAir("minecraft:air");
            LoadCreativeItems();
            ItemTraitRegistry.RegisterFromAssembly(Assembly.GetExecutingAssembly());
            WarnIfCreativeTabsSparse();
            Orion.Api.Items.Items.SetFactory(new ItemStackFactory());
            _loaded = true;
        }
    }

    static void WarnIfCreativeTabsSparse()
    {
        if (!CuratedItemCatalog.NonNatureCreativeTabsEmpty)
        {
            return;
        }

        Log.Warn(
            LogCategory.Orion,
            "Creative inventory: Construction / Equipment / Items have no items. " +
            "Bedrock may show an empty creative menu. See docs/pt_br/first-run.md or docs/en_us/first-run.md " +
            "(enable Plugins and build plugins/orion:creative-fillers, or call CuratedItemCatalog.RegisterCreativeTabEntries).");
    }

    public static ItemStack? TryGetCreativeItem(uint creativeItemNetworkId)
    {
        EnsureLoaded();
        return _creativeItems is not null && _creativeItems.TryGetValue(creativeItemNetworkId, out ItemStack? item)
            ? item.Clone()
            : null;
    }

    public static ItemStack? GetCreativeItem(uint creativeItemNetworkId) =>
        TryGetCreativeItem(creativeItemNetworkId);

    public static ItemStack? TryGetCreativePickFromSlotByte(byte slotByte, out string resolution)
    {
        EnsureLoaded();
        resolution = "none";

        ItemType? bySignedNetwork = ItemType.GetByNetwork((sbyte)slotByte);
        if (bySignedNetwork is not null && bySignedNetwork != ItemType.Air)
        {
            resolution = $"networkId(sbyte)={(sbyte)slotByte} -> {bySignedNetwork.Identifier}";
            return new ItemStack(bySignedNetwork, 1);
        }

        ItemType? byNetwork = ItemType.GetByNetwork(slotByte);
        if (byNetwork is not null && byNetwork != ItemType.Air)
        {
            resolution = $"networkId={slotByte} -> {byNetwork.Identifier}";
            return new ItemStack(byNetwork, 1);
        }

        if (_creativeItems is not null)
        {
            foreach (ItemStack item in _creativeItems.Values)
            {
                int networkId = item.Type.NetworkId;
                if (networkId == slotByte
                    || networkId == (sbyte)slotByte
                    || unchecked((byte)networkId) == slotByte)
                {
                    resolution = $"creativeCatalog networkId={networkId} slotByte={slotByte} -> {item.Type.Identifier}";
                    return item.Clone();
                }
            }
        }

        ItemStack? fromIndex = TryGetCreativeItem(slotByte);
        if (fromIndex is not null)
        {
            resolution = $"creativeIndex={slotByte} -> {fromIndex.Type.Identifier}";
            return fromIndex;
        }

        return null;
    }

    public static ItemStack? TryGetCreativePickFromSlotByte(byte slotByte) =>
        TryGetCreativePickFromSlotByte(slotByte, out _);

    public static IReadOnlyCollection<string> GetGiveableIdentifiers()
    {
        EnsureLoaded();
        return _giveableIdentifiers;
    }

    public static bool IsGiveable(string identifier)
    {
        EnsureLoaded();
        return _giveableIdentifiers.Contains(identifier);
    }

    static void LoadCreativeItems()
    {
        Dictionary<uint, ItemStack> creative = [];
        IReadOnlyList<CuratedItem> menu = CuratedItemCatalog.GetCreativeMenuItems();
        for (int i = 0; i < menu.Count; i++)
        {
            CuratedItem curated = menu[i];
            ItemType? type = ItemType.GetByNetwork(curated.NetworkId) ?? ItemType.Get(curated.Identifier);
            if (type is null)
            {
                continue;
            }

            creative[checked((uint)(i + 1))] = new ItemStack(type, checked((ushort)type.MaxStackSize));
        }

        _creativeItems = creative;
    }

    static int ResolveMaxStack(CuratedItem curated)
    {
        if (curated.PropertiesNbt.Get<CompoundTag>("components") is CompoundTag components
            && components.Get<CompoundTag>("item_properties") is CompoundTag itemProperties
            && itemProperties.Get<IntTag>("max_stack_size") is IntTag maxStack
            && maxStack.Value > 0)
        {
            return maxStack.Value;
        }

        return 64;
    }
}
