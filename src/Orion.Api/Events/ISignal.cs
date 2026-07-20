namespace Orion.Api.Events;

public interface ISignal
{
    ServerEvent Event { get; }
}
