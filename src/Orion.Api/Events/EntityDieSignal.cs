namespace Orion.Api.Events;

public sealed class EntityDieSignal : EntitySignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.EntityDie;
    public bool Cancelled { get; private set; }

    public EntityDieSignal(IEntity entity) : base(entity)
    {
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
