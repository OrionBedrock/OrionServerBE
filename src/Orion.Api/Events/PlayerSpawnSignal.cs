namespace Orion.Api.Events;

public sealed class PlayerSpawnSignal : PlayerSignal, ICancellable
{
    public override ServerEvent Event => ServerEvent.PlayerSpawn;
    public EntitySpawnOptions Options { get; set; }
    public bool Cancelled { get; private set; }

    public PlayerSpawnSignal(IPlayer player, EntitySpawnOptions options) : base(player)
    {
        Options = options;
    }

    public bool Emit() => !Cancelled;

    public void Cancel() => Cancelled = true;

    internal void SetCancelled(bool cancelled) => Cancelled = cancelled;
}
