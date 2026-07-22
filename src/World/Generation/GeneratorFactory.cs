using Orion.Api.Worldgen;

namespace Orion.World.Generation;

public static class GeneratorFactory
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Type> PluginGenerators = new(StringComparer.OrdinalIgnoreCase);
    private static bool _frozen;

    public static bool IsFrozen
    {
        get
        {
            lock (Sync)
            {
                return _frozen;
            }
        }
    }

    public static void Register(string name, Type generatorType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(generatorType);

        if (!typeof(WorldGeneratorBase).IsAssignableFrom(generatorType) || generatorType.IsAbstract)
        {
            throw new ArgumentException(
                $"Generator type '{generatorType.FullName}' must be a concrete subclass of {nameof(WorldGeneratorBase)}.",
                nameof(generatorType));
        }

        if (generatorType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new ArgumentException(
                $"Generator type '{generatorType.FullName}' must have a public parameterless constructor.",
                nameof(generatorType));
        }

        lock (Sync)
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    "Generators must be registered before world bootstrap (plugin Load).");
            }

            PluginGenerators[name] = generatorType;
        }
    }

    public static void Freeze()
    {
        lock (Sync)
        {
            _frozen = true;
        }
    }

    public static Generator Create(string identifier)
    {
        lock (Sync)
        {
            if (PluginGenerators.TryGetValue(identifier, out Type? type))
            {
                WorldGeneratorBase api = (WorldGeneratorBase)Activator.CreateInstance(type)!;
                return new ApiGeneratorAdapter(api);
            }
        }

        return identifier.ToLowerInvariant() switch
        {
            "void" => new VoidGenerator(),
            _ => new VoidGenerator()
        };
    }

    public static void ResetForTests()
    {
        lock (Sync)
        {
            PluginGenerators.Clear();
            _frozen = false;
        }
    }
}
