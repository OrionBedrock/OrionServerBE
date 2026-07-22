namespace Orion.PluginContracts.Registry;

/// <summary>
/// Registers plugin world generators. <paramref name="generatorType"/> must be a concrete
/// subclass of Orion.Api.Worldgen.WorldGeneratorBase with a public parameterless constructor.
/// Call during plugin Load (before world bootstrap freezes generators).
/// </summary>
public interface IGeneratorRegistry
{
    void Register(string name, Type generatorType);
}
