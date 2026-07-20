namespace Orion.Api.Events;

public sealed class PlayerInteractEntitySignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerInteractEntity;
    public IEntity Target { get; }
    public bool Cancelled { get; private set; }

    public PlayerInteractEntitySignal(IPlayer player, IEntity target) : base(player)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
