using Orion.Api.Items;

namespace Orion.Api.Events;

public sealed class PlayerItemUseSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerItemUse;
    public IItemStack Item { get; }
    public bool Cancelled { get; private set; }

    public PlayerItemUseSignal(IPlayer player, IItemStack item) : base(player)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}

public sealed class PlayerItemUseCompleteSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerItemUseComplete;
    public IItemStack Item { get; }
    public bool Cancelled { get; private set; }

    public PlayerItemUseCompleteSignal(IPlayer player, IItemStack item) : base(player)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
