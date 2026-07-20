namespace Orion.Api.Events;

public sealed class PlayerJoinSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerJoin;
    public bool Cancelled { get; private set; }

    public PlayerJoinSignal(IPlayer player) : base(player)
    {
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
