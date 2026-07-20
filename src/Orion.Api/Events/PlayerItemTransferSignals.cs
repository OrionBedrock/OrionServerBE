using Orion.Api.Items;

namespace Orion.Api.Events;

public sealed class PlayerDropItemSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerDropItem;
    public IItemStack Item { get; }
    public bool Cancelled { get; private set; }

    public PlayerDropItemSignal(IPlayer player, IItemStack item) : base(player)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}

public sealed class PlayerPickupItemSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerPickupItem;
    public IEntity ItemEntity { get; }
    public bool Cancelled { get; private set; }

    public PlayerPickupItemSignal(IPlayer player, IEntity itemEntity) : base(player)
    {
        ItemEntity = itemEntity ?? throw new ArgumentNullException(nameof(itemEntity));
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
