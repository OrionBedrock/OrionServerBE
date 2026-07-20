namespace Orion.Api.Events;

public abstract class PlayerSignal : EntitySignal
{
    public IPlayer Player { get; }

    protected PlayerSignal(IPlayer player) : base(player)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
    }
}
