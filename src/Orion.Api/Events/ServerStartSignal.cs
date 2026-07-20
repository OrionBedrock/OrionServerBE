namespace Orion.Api.Events;

public sealed class ServerStartSignal : ServerSignal
{
    public override ServerEvent Event => ServerEvent.ServerStart;
}
