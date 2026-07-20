namespace Orion.Api.Events;

public abstract class EntitySignal : ISignal
{
    public abstract ServerEvent Event { get; }

    public IEntity Entity { get; }

    protected EntitySignal(IEntity entity)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }
}
