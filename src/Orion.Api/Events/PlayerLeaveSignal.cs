namespace Orion.Api.Events;

public sealed class PlayerLeaveSignal : PlayerSignal
{
    public override ServerEvent Event => ServerEvent.PlayerLeave;

    public PlayerLeaveSignal(IPlayer player) : base(player)
    {
    }
}
