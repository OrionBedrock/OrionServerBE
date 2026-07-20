namespace Orion.Api.Events;

public sealed class PlayerContainerCloseSignal : PlayerSignal
{
    public override ServerEvent Event => ServerEvent.PlayerContainerClose;
    public int WindowId { get; }

    public PlayerContainerCloseSignal(IPlayer player, int windowId) : base(player)
    {
        WindowId = windowId;
    }
}

public sealed class PlayerInventorySlotChangeSignal : PlayerSignal
{
    public override ServerEvent Event => ServerEvent.PlayerInventorySlotChange;
    public int Slot { get; }

    public PlayerInventorySlotChangeSignal(IPlayer player, int slot) : base(player)
    {
        Slot = slot;
    }
}
