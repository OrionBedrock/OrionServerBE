namespace Orion.Block.Traits;

using System.Reflection;
using Orion.Api.Traits;
using Orion.Protocol.Nbt;
using Orion.Block.Traits.Types;

public abstract class BlockTrait : BlockTraitBase
{
    public static readonly string[] Types = [];
    public static readonly string[] Tags = [];
    public static readonly Type? Component = null;
    public static readonly Type[] Components = [];

    protected Orion.Block.Block Block { get; }

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

    protected BlockTrait(Orion.Block.Block block)
    {
        Block = block;
    }

    public virtual void OnRead(CompoundTag tag)
    {
    }

    public virtual void OnWrite(CompoundTag tag)
    {
    }

    public virtual void OnPlace(BlockPlaceDetails details)
    {
    }

    public virtual void OnBreak(BlockBreakDetails details)
    {
    }

    public virtual void OnInteract(BlockInteractDetails details)
    {
    }

    public virtual void OnTick(BlockTickDetails details)
    {
    }

    public virtual void OnRandomTick(BlockRandomTickDetails details)
    {
    }

    public virtual void OnLandOn(BlockLandOnDetails details)
    {
    }

    public virtual void OnRender(Orion.Player.Player player, int x, int y, int z)
    {
    }
}
