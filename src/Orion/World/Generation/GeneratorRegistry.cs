namespace Orion.World.Generation;

/// <summary>
/// Public SPI for registering chunk generators (plugins later).
/// </summary>
public sealed class GeneratorRegistry
{
    private readonly Dictionary<string, IChunkGenerator> _generators = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public void Register(IChunkGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);
        if (string.IsNullOrWhiteSpace(generator.Identifier))
        {
            throw new ArgumentException("Generator identifier is required.", nameof(generator));
        }

        lock (_sync)
        {
            if (!_generators.TryAdd(generator.Identifier, generator))
            {
                throw new InvalidOperationException($"Generator '{generator.Identifier}' is already registered.");
            }
        }
    }

    public bool TryGet(string identifier, out IChunkGenerator? generator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        lock (_sync)
        {
            return _generators.TryGetValue(identifier, out generator);
        }
    }

    public IChunkGenerator Get(string identifier)
    {
        if (!TryGet(identifier, out IChunkGenerator? generator) || generator is null)
        {
            throw new InvalidOperationException($"Unknown chunk generator '{identifier}'.");
        }

        return generator;
    }

    public bool IsRegistered(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        lock (_sync)
        {
            return _generators.ContainsKey(identifier);
        }
    }

    public static GeneratorRegistry CreateDefault()
    {
        var registry = new GeneratorRegistry();
        registry.Register(new VoidGenerator());
        return registry;
    }
}
