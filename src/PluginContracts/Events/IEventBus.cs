using Orion.Api.Events;

namespace Orion.PluginContracts.Events;

public interface IEventBus
{
    void Subscribe<TSignal>(Action<TSignal> handler, EventPriority priority = EventPriority.Normal)
        where TSignal : ISignal;

    void Unsubscribe<TSignal>(Action<TSignal> handler) where TSignal : ISignal;

    IDisposable SubscribeDisposable<TSignal>(
        Action<TSignal> handler,
        EventPriority priority = EventPriority.Normal) where TSignal : ISignal;
}
