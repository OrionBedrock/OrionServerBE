namespace Orion.Api.Events;

public sealed class EntityHurtSignal : EntitySignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.EntityHurt;
    public float Amount { get; set; }
    public int? DamageCause { get; }
    public IEntity? Damager { get; }
    public bool Cancelled { get; private set; }

    public EntityHurtSignal(IEntity entity, float amount, int? damageCause = null, IEntity? damager = null)
        : base(entity)
    {
        Amount = amount;
        DamageCause = damageCause;
        Damager = damager;
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
