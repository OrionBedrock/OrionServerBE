using Orion.Api.Items;

namespace Orion.Api.Events;

public sealed class PlayerFoodEatSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerFoodEat;
    public IItemStack Food { get; }
    public bool Cancelled { get; private set; }

    public PlayerFoodEatSignal(IPlayer player, IItemStack food) : base(player)
    {
        Food = food ?? throw new ArgumentNullException(nameof(food));
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}

public sealed class PlayerHungerChangeSignal : PlayerSignal
{
    public override ServerEvent Event => ServerEvent.PlayerHungerChange;
    public float OldHunger { get; }
    public float NewHunger { get; }

    public PlayerHungerChangeSignal(IPlayer player, float oldHunger, float newHunger) : base(player)
    {
        OldHunger = oldHunger;
        NewHunger = newHunger;
    }
}
