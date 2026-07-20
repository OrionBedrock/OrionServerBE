namespace Orion.Item;

using Orion.Protocol.Types;
using Orion.Protocol.Nbt;
using Orion.Item.Traits;
using Orion.Item.Traits.Types;

public sealed class ItemStack : Orion.Api.Items.IItemStack {
    private static int _nextNetworkStackId;
    private readonly List<Traits.ItemTrait> _traits = [];

    public ItemType Type { get; }
    public string Identifier => Type.Identifier;
    public ushort StackSize { get; private set; }
    public uint Metadata { get; private set; }
    public int NetworkStackId { get; private set; } = ++_nextNetworkStackId;
    public ItemInstanceUserData? ExtraData { get; private set; }

    Orion.Api.Items.IItemType Orion.Api.Items.IItemStack.Type => Type;
    int Orion.Api.Items.IItemStack.Count => StackSize;
    uint Orion.Api.Items.IItemStack.Metadata => Metadata;
    int Orion.Api.Items.IItemStack.NetworkStackId => NetworkStackId;

    void Orion.Api.Items.IItemStack.SetCount(int count) => SetStackSize((ushort)Math.Clamp(count, 0, ushort.MaxValue));

    void Orion.Api.Items.IItemStack.Increment(int amount) => IncrementStack((ushort)Math.Clamp(amount, 0, ushort.MaxValue));

    void Orion.Api.Items.IItemStack.Decrement(int amount) => DecrementStack((ushort)Math.Clamp(amount, 0, ushort.MaxValue));

    bool Orion.Api.Items.IItemStack.CanStackWith(Orion.Api.Items.IItemStack other) =>
        other is ItemStack stack && CanStackWith(stack);

    Orion.Api.Items.IItemStack Orion.Api.Items.IItemStack.Clone(int? count) =>
        Clone(count is null ? null : (ushort)Math.Clamp(count.Value, 0, ushort.MaxValue));


    public ItemStack(ItemType type, ushort stackSize = 1, uint metadata = 0, ItemInstanceUserData? extraData = null)
    {
        Type = type;
        StackSize = (ushort)Math.Min(stackSize, type.MaxStackSize);
        Metadata = metadata;
        ExtraData = extraData;

        foreach (Type traitType in Type.Traits.Values)
        {
            object? created = Activator.CreateInstance(traitType, this);
            if (created is Traits.ItemTrait trait)
            {
                AddTrait(trait);
            }
            else if (created is Orion.Api.Traits.ItemTraitBase)
            {
                // Plugin traits that only subclass ItemTraitBase are tracked via Type.Traits
                // for registration; host ItemStack currently only hosts ItemTrait instances.
                // ItemTraitBase-only plugins should use gameplay services instead of stack traits
                // until full TraitBase hosting lands.
            }
        }
    }
    
    public ItemStack(string identifier, ushort stackSize = 1, uint metadata = 0, ItemInstanceUserData? extraData = null)
        : this(ItemType.Get(identifier) ?? throw new InvalidOperationException($"Unknown item type '{identifier}'."), stackSize, metadata, extraData)
    {
    }

    public void SetStackSize(ushort value)
    {
        StackSize = (ushort)Math.Min(value, Type.MaxStackSize);
    }

    public void IncrementStack(ushort value = 1)
    {
        SetStackSize((ushort)(StackSize + value));
    }

    public void DecrementStack(ushort value = 1)
    {
        StackSize = value >= StackSize ? (ushort)0 : (ushort)(StackSize - value);
    }

    public void SetMetadata(uint value)
    {
        Metadata = value;
    }

    public void SetExtraData(ItemInstanceUserData? extraData)
    {
        ExtraData = extraData;
    }

    /// <summary>
    /// Adopts the stack network id predicted by the client for cursor placements.
    /// Required when server-authoritative inventory is enabled.
    /// </summary>
    internal void SetNetworkStackId(int networkStackId)
    {
        NetworkStackId = networkStackId;
    }

    public bool Equals(ItemStack other)
    {
        return Type.Identifier == other.Type.Identifier
               && StackSize == other.StackSize
               && Metadata == other.Metadata
               && Equals(ExtraData, other.ExtraData);
    }

    public bool CanStackWith(ItemStack other)
    {
        return Type.Identifier == other.Type.Identifier
               && Metadata == other.Metadata
               && HasSameTraits(other);
    }

    public LegacyItem ToNetworkStack()
    {
        LegacyItem descriptor = ItemType.ToNetworkStack(Type, StackSize, Metadata);
        descriptor.ItemStackId = NetworkStackId;
        descriptor.ExtraData = ExtraData;
        return descriptor;
    }

    public LegacyItem ToLegacyInventoryItem()
    {
        if (StackSize == 0 || Type.NetworkId == 0)
        {
            return new LegacyItem();
        }

        int networkBlockId = ItemBlockRuntimeIds.Resolve(Type);
        CompoundTag? nbt = GetSerializedNbt();
        bool hasExtraData = (ExtraData?.CanPlaceOn.Count ?? 0) > 0
            || (ExtraData?.CanDestroy.Count ?? 0) > 0
            || ExtraData?.Ticking is not null
            || (nbt is not null && nbt.Values.Count > 0);

        return new LegacyItem
        {
            NetworkId = Type.NetworkId,
            StackSize = StackSize,
            Metadata = unchecked((int)Metadata),
            ItemStackId = NetworkStackId,
            NetworkBlockId = networkBlockId,
            ExtraData = hasExtraData
                ? new ItemInstanceUserData
                {
                    Nbt = nbt,
                    CanPlaceOn = ExtraData?.CanPlaceOn ?? [],
                    CanDestroy = ExtraData?.CanDestroy ?? [],
                    Ticking = ExtraData?.Ticking
                }
                : null
        };
    }

    public NetworkItemStackDescriptor ToNetworkItemStackDescriptor()
    {
        if (StackSize == 0 || Type.NetworkId == 0)
        {
            return new NetworkItemStackDescriptor();
        }

        int runtimeId = ItemBlockRuntimeIds.Resolve(Type);

        return new NetworkItemStackDescriptor
        {
            NetworkId = Type.NetworkId,
            Count = StackSize,
            Metadata = Metadata,
            StackNetworkId = NetworkStackId,
            BlockRuntimeId = runtimeId,
            Nbt = GetSerializedNbt(),
            CanPlaceOn = ExtraData?.CanPlaceOn ?? [],
            CanDestroy = ExtraData?.CanDestroy ?? [],
            BlockingTick = ExtraData?.Ticking ?? 0
        };
    }

    public static ItemStack FromNetworkStack(LegacyItem descriptor)
    {
        ItemType type = ItemType.GetByNetwork(descriptor.NetworkId)
                        ?? throw new InvalidOperationException($"Unknown item network id '{descriptor.NetworkId}'.");

        ItemStack stack = new(type, descriptor.StackSize, unchecked((uint)descriptor.Metadata), descriptor.ExtraData);
        if (descriptor.ItemStackId is int stackId && stackId != 0)
        {
            stack.SetNetworkStackId(stackId);
        }

        return stack;
    }

    public static ItemStack FromNetworkStack(NetworkItemStackDescriptor descriptor)
    {
        ItemType type = ItemType.GetByNetwork(descriptor.NetworkId)
                        ?? throw new InvalidOperationException($"Unknown item network id '{descriptor.NetworkId}'.");

        bool hasExtra = descriptor.Nbt is not null
            || descriptor.CanPlaceOn.Count > 0
            || descriptor.CanDestroy.Count > 0
            || descriptor.BlockingTick != 0;

        ItemInstanceUserData? extraData = hasExtra
            ? new ItemInstanceUserData
            {
                Nbt = descriptor.Nbt,
                CanPlaceOn = descriptor.CanPlaceOn,
                CanDestroy = descriptor.CanDestroy,
                Ticking = descriptor.BlockingTick
            }
            : null;

        ItemStack stack = new(type, descriptor.Count, descriptor.Metadata, extraData);
        if (descriptor.StackNetworkId != 0)
        {
            stack.SetNetworkStackId(descriptor.StackNetworkId);
        }

        return stack;
    }

    public static ItemStack Empty()
    {
        return new ItemStack(ItemType.Air, 0, 0);
    }

    public CompoundTag Serialize()
    {
        CompoundTag tag = new();
        tag.Set("id", new StringTag { Value = Identifier });
        tag.Set("count", new IntTag { Value = StackSize });
        tag.Set("meta", new IntTag { Value = unchecked((int)Metadata) });

        CompoundTag? nbt = GetSerializedNbt();
        if (nbt is not null && nbt.Values.Count > 0)
        {
            tag.Set("nbt", nbt);
        }

        return tag;
    }

    public static ItemStack? Deserialize(CompoundTag tag)
    {
        StringTag? idTag = tag.Get<StringTag>("id");
        if (idTag is null || string.IsNullOrWhiteSpace(idTag.Value))
        {
            return null;
        }

        ItemType? type = ItemType.Get(idTag.Value);
        if (type is null)
        {
            return null;
        }

        ushort stackSize = (ushort)Math.Max(0, tag.Get<IntTag>("count")?.Value ?? 0);
        uint metadata = unchecked((uint)(tag.Get<IntTag>("meta")?.Value ?? 0));
        CompoundTag? nbt = tag.Get<CompoundTag>("nbt");
        ItemInstanceUserData? extraData = null;
        if (nbt is not null)
        {
            extraData = new ItemInstanceUserData
            {
                Nbt = nbt,
                CanPlaceOn = [],
                CanDestroy = [],
                Ticking = null
            };
        }

        ItemStack stack = new(type, stackSize, metadata, extraData);
        if (nbt is not null)
        {
            stack.ReadTraits(nbt);
        }
        return stack;
    }

    public T AddTrait<T>(T trait) where T : Traits.ItemTrait
    {
        ArgumentNullException.ThrowIfNull(trait);
        if (GetTrait(trait.Identifier) is not null)
        {
            return trait;
        }

        _traits.Add(trait);
        trait.OnAdd();
        return trait;
    }

    public CompoundTag? GetSerializedNbt()
    {
        CompoundTag nbt = ExtraData?.Nbt ?? new CompoundTag();
        WriteTraits(nbt);
        return nbt.Values.Count > 0 ? nbt : null;
    }

    public ItemStack Clone(ushort? stackSize = null)
    {
        CompoundTag serialized = Serialize();
        if (stackSize.HasValue)
        {
            serialized.Set("count", new IntTag { Value = stackSize.Value });
        }

        return Deserialize(serialized) ?? throw new InvalidOperationException("Failed to clone item stack.");
    }

    public void OnUseOnAir(ItemUseOnAirDetails details)
    {
        for (int i = 0; i < _traits.Count; i++)
        {
            _traits[i].OnUseOnAir(details);
        }
    }

    public void OnUseOnBlock(ItemUseOnBlockDetails details)
    {
        for (int i = 0; i < _traits.Count; i++)
        {
            _traits[i].OnUseOnBlock(details);
        }
    }

    public void OnPlace(ItemPlaceDetails details)
    {
        for (int i = 0; i < _traits.Count; i++)
        {
            _traits[i].OnPlace(details);
        }
    }

    public void OnUseOnEntity(ItemUseOnEntityDetails details)
    {
        for (int i = 0; i < _traits.Count; i++)
        {
            _traits[i].OnUseOnEntity(details);
        }
    }

    public void OnUseAttack(ItemUseAttackDetails details)
    {
        for (int i = 0; i < _traits.Count; i++)
        {
            _traits[i].OnUseAttack(details);
        }
    }

    public void OnBreakBlock(ItemBreakBlockDetails details)
    {
        for (int i = 0; i < _traits.Count; i++)
        {
            _traits[i].OnBreakBlock(details);
        }
    }


    public bool HasTrait<T>() where T : Traits.ItemTrait
    {
        return GetTrait<T>() is not null;
    }

    public T? GetTrait<T>() where T : Traits.ItemTrait
    {
        for (int i = 0; i < _traits.Count; i++)
        {
            if (_traits[i] is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    private void WriteTraits(CompoundTag nbt)
    {
        if (_traits.Count == 0) return;

        ListTag traitsTag = new() { Name = "traits" };
        foreach (var trait in _traits)
        {
            CompoundTag traitEntry = new();
            traitEntry.Set("id", new StringTag { Value = trait.Identifier });

            CompoundTag traitData = new();
            trait.OnWrite(traitData);
            traitEntry.Set("data", traitData);

            traitsTag.Values.Add(traitEntry);
        }

        nbt.Set("traits", traitsTag);
    }

    private void ReadTraits(CompoundTag nbt)
    {
        ListTag? traitsTag = nbt.Get<ListTag>("traits");
        if (traitsTag is null) return;

        foreach (BaseTag tag in traitsTag.Values)
        {
            if (tag is not CompoundTag traitEntry) continue;

            string? identifier = traitEntry.Get<StringTag>("id")?.Value;
            CompoundTag? traitData = traitEntry.Get<CompoundTag>("data");

            if (identifier == null || traitData == null) continue;

            Traits.ItemTrait? trait = GetTrait(identifier);
            if (trait == null)
            {
                if (ItemTraitRegistry.RegisteredTraits.TryGetValue(identifier, out Type? traitType))
                {
                    if (Activator.CreateInstance(traitType, this) is Traits.ItemTrait newTrait)
                    {
                        AddTrait(newTrait);
                        trait = newTrait;
                    }
                }
            }

            trait?.OnRead(traitData);
        }
    }

    // AddTrait methods are now defined above

    public Traits.ItemTrait? GetTrait(string identifier)
    {
        for (int i = 0; i < _traits.Count; i++)
        {
            if (string.Equals(_traits[i].Identifier, identifier, StringComparison.Ordinal))
            {
                return _traits[i];
            }
        }

        return null;
    }

    private bool HasSameTraits(ItemStack other)
    {
        if (_traits.Count != other._traits.Count)
        {
            return false;
        }

        HashSet<string> thisTraits = new(_traits.Count, StringComparer.Ordinal);
        for (int i = 0; i < _traits.Count; i++)
        {
            thisTraits.Add(_traits[i].Identifier);
        }

        for (int i = 0; i < other._traits.Count; i++)
        {
            if (!thisTraits.Contains(other._traits[i].Identifier))
            {
                return false;
            }
        }

        return true;
    }
}






