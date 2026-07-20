namespace Orion.Api.Events;

public sealed class PlayerGamemodeChangeSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerGamemodeChange;
    public Gamemode OldGamemode { get; }
    public Gamemode NewGamemode { get; }
    public bool Cancelled { get; private set; }

    public PlayerGamemodeChangeSignal(IPlayer player, Gamemode oldGamemode, Gamemode newGamemode) : base(player)
    {
        OldGamemode = oldGamemode;
        NewGamemode = newGamemode;
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
