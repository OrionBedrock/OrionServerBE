namespace Orion.Entity.Traits;

using Orion.Entity.Traits.Types;
using Orion.Gameplay;
using Orion.Plugins;
using Orion.Protocol.Enums;
using Orion.Protocol.Types;
using Orion.Api.Traits;
using Orion.World;


public sealed class EntityAirSupplyTrait : EntityTrait
{
    private const int MaxAirTicks = 300;

    public new static string Identifier => "air_supply";
    public new static readonly EntityIdentifier[] Types = [EntityIdentifier.Player];

    private int _airTicks = MaxAirTicks;

    public EntityAirSupplyTrait(Entity entity) : base(entity)
    {
    }

    public override void OnSpawn(EntitySpawnOptions details)
    {
        Entity.Flags.SetActorFlag(ActorFlag.Breathing, true);
        _airTicks = MaxAirTicks;
    }

    public override void OnTick(TraitOnTickDetails details)
    {
        if (!Entity.IsAlive || !Entity.Flags.GetActorFlag(ActorFlag.Breathing))
        {
            return;
        }

        if (Entity is Orion.Player.Player player &&
            player.GetGamemode() is not (Gamemode.Survival or Gamemode.Adventure))
        {
            return;
        }

        if (CanBreathe())
        {
            if (_airTicks < MaxAirTicks)
            {
                _airTicks += 5;

                if (_airTicks > MaxAirTicks)
                {
                    _airTicks = MaxAirTicks;
                }
            }

            return;
        }

        _airTicks--;

        if (_airTicks > -20)
        {
            return;
        }

        _airTicks = 0;

        if (Entity.Dimension?.Gamerules.DrowningDamage == false)
        {
            return;
        }

        if (!PluginHost.Services.TryGet(out IEntityHealthService? health) || health is null)
        {
            return;
        }

        _ = health.TryApplyDamage(
            Entity,
            0.5f,
            null,
            Entity.IsSwimming ? ActorDamageCause.Drowning : ActorDamageCause.Suffocation);
    }

    public int GetAirSupplyTicks()
    {
        return _airTicks;
    }

    public void SetAirSupplyTicks(int ticks)
    {
        _airTicks = ticks;
    }

    public override EntityTrait Clone(Entity entity)
    {
        return new EntityAirSupplyTrait(entity);
    }

    private bool CanBreathe()
    {
        if (Entity.Dimension is null || Entity.HasEffect(EffectType.WaterBreathing))
        {
            return true;
        }

        Vec3f head = Entity.GetHeadLocation();

        string block = Entity.Dimension
            .GetGameplayPermutation(
                (int)MathF.Floor(head.X),
                (int)MathF.Floor(head.Y),
                (int)MathF.Floor(head.Z))
            .Type
            .Identifier;

        if (block.Contains("water", StringComparison.Ordinal) ||
            block.Contains("lava", StringComparison.Ordinal))
        {
            return false;
        }

        return block == "minecraft:air";
    }
}





