using Orion.Api;
using Orion.Registries;
using Orion.Scheduler;

namespace Orion.Plugins;

internal sealed class GlobalSchedulerAdapter(GlobalRegionScheduler inner) : IGlobalScheduler
{
    public void Execute(Action action) => inner.Execute(action);
}

internal sealed class RegionSchedulerAdapter(RegionScheduler inner) : IRegionScheduler
{
    public void Execute(string worldId, int chunkX, int chunkZ, Action action)
        => inner.Execute(worldId, chunkX, chunkZ, action);
}

internal sealed class ContentRegistriesAdapter(ServerRegistries inner) : IContentRegistries
{
    public bool HasBlock(string id) => inner.Blocks.Contains(id);

    public bool HasItem(string id) => inner.Items.Contains(id);
}
