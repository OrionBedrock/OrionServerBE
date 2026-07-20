namespace Orion.Api.Events;

public interface ICancellable
{
    bool Cancelled { get; }

    void Cancel();
}
