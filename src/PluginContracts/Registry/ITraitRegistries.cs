using System.Reflection;

namespace Orion.PluginContracts.Registry;

public interface IBlockTraitRegistry
{
    void RegisterFromAssembly(Assembly assembly, string pluginId);
    void Register(Type traitType, string pluginId);
}

public interface IItemTraitRegistry
{
    void RegisterFromAssembly(Assembly assembly, string pluginId);
    void Register(Type traitType, string pluginId);
}

public interface IEntityTraitRegistry
{
    void RegisterFromAssembly(Assembly assembly, string pluginId);
    void Register(Type traitType, string pluginId);
}

public interface IPlayerTraitRegistry
{
    void RegisterFromAssembly(Assembly assembly, string pluginId);
    void Register(Type traitType, string pluginId);
}
