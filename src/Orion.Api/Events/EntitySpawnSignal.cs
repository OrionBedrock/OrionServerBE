namespace Orion.Api.Events;

public sealed class EntitySpawnSignal : EntitySignal
{
    public override ServerEvent Event => ServerEvent.EntitySpawn;
    public EntitySpawnOptions Options { get; }

    public EntitySpawnSignal(IEntity entity, EntitySpawnOptions options) : base(entity)
    {
        Options = options;
    }
}
