namespace Orion.Entity.Traits;

using Orion.Commands;
using Orion.Entity.Traits.Enums;
using Orion.Entity.Traits.Types;
using Orion.Protocol.Nbt;
using Orion.Protocol.Enums;
using Orion.Api.Traits;

using System.Reflection;
using Player = Orion.Player.Player;

public abstract class EntityTrait : EntityTraitBase
{
    public static readonly EntityIdentifier[] Types = [];
    public static readonly string[] Components = [];

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

            return base.Identifier;
        }
    }

    protected Entity Entity { get; }

    protected Dimension Dimension => Entity.Dimension
        ?? throw new InvalidOperationException("Entity is not in a dimension.");

    protected EntityTrait(Entity entity)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public virtual void OnSpawn(EntitySpawnOptions details)
    {
    }

    public virtual void OnDespawn(EntityDespawnOptions details)
    {
    }

    public virtual void OnDeath(EntityDeathOptions details)
    {
    }

    public virtual void OnTeleport(EntityTeleportOptions details)
    {
    }

    public virtual void OnMove(EntityMoveOptions details)
    {
    }

    public virtual void OnInteract(Player player, EntityInteractMethod method)
    {
    }

    public virtual bool OnCommand(CommandExecutionState state)
    {
        return true;
    }

    public virtual void OnContainerUpdate(Orion.Api.Containers.IContainer container)
    {
    }

    public virtual void OnFallOnBlock(EntityFallOnBlockTraitEvent @event)
    {
    }

    public virtual void OnRendered(EntityRenderedOptions details)
    {
    }

    public virtual void OnRead(CompoundTag entityTag, CompoundTag traitTag)
    {
        OnRead(traitTag);
    }

    public virtual void OnWrite(CompoundTag entityTag, CompoundTag traitTag)
    {
        OnWrite(traitTag);
    }

    public virtual void OnRead(CompoundTag tag)
    {
    }

    public virtual void OnWrite(CompoundTag tag)
    {
    }

    public abstract EntityTrait Clone(Entity entity);
}
