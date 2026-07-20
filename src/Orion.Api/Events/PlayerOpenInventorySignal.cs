namespace Orion.Api.Events;

public sealed class PlayerOpenInventorySignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerOpenInventory;
    public bool Cancelled { get; private set; }

    public PlayerOpenInventorySignal(IPlayer player) : base(player)
    {
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
