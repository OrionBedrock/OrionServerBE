namespace Orion.Item.Traits;

using System.Reflection;
using Orion.Api.Traits;
using Orion.Protocol.Nbt;
using Orion.Item.Traits.Types;

public abstract class ItemTrait : ItemTraitBase
{
    public static readonly string[] Types = [];
    public static readonly string[] Tags = [];
    public static readonly Type? Component = null;
    public static readonly Type[] Components = [];

    protected ItemStack ItemStack { get; }

    public override string Identifier
    {
        get
        {
            if (GetType().GetProperty("Identifier", BindingFlags.Public | BindingFlags.Static) is PropertyInfo property &&
                property.PropertyType == typeof(string) &&
                property.GetValue(null) is string identifier &&
                !string.IsNullOrWhiteSpace(identifier))
            {
                return identifier;
            }

            return GetType().FullName ?? GetType().Name;
        }
    }

    protected ItemTrait(ItemStack itemStack)
    {
        ItemStack = itemStack;
    }

    public virtual void OnRead(CompoundTag tag)
    {
    }

    public virtual void OnWrite(CompoundTag tag)
    {
    }

    public virtual void OnUseOnAir(ItemUseOnAirDetails details)
    {
    }

    public virtual void OnUseOnBlock(ItemUseOnBlockDetails details)
    {
    }

    public virtual void OnPlace(ItemPlaceDetails details)
    {
    }

    public virtual void OnUseOnEntity(ItemUseOnEntityDetails details)
    {
    }

    public virtual void OnUseAttack(ItemUseAttackDetails details)
    {
    }

    public virtual void OnBreakBlock(ItemBreakBlockDetails details)
    {
    }
}
