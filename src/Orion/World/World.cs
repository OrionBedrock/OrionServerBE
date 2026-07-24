using Orion.Config;
using Orion.Region;
using Orion.World.Chunk;
using Orion.World.Provider;
using Orion.World.Tickets;

namespace Orion.World;

public sealed class Dimension
{
    private readonly IWorldProvider _provider;
    private readonly ChunkTicketManager _tickets;

    public Dimension(string identifier, int type, string generatorId, Regionizer regionizer, IWorldProvider provider)
    {
        Identifier = identifier;
        Type = type;
        GeneratorId = generatorId;
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tickets = new ChunkTicketManager(regionizer ?? throw new ArgumentNullException(nameof(regionizer)));
    }

    public string Identifier { get; }

    public int Type { get; }

    public string GeneratorId { get; }

    public ChunkTicketManager Tickets => _tickets;

    public IWorldProvider Provider => _provider;

    public SimulationTicket AcquireTicket(int chunkX, int chunkZ)
        => _tickets.Acquire(Identifier, chunkX, chunkZ, LoadOrCreatePlaceholder);

    private ChunkColumn LoadOrCreatePlaceholder(string dimensionId, int chunkX, int chunkZ)
    {
        ChunkColumn? existing = _provider.LoadChunk(dimensionId, chunkX, chunkZ);
        if (existing is not null)
        {
            existing.IsLoaded = true;
            return existing;
        }

        return new ChunkColumn(chunkX, chunkZ) { IsLoaded = true };
    }
}

public sealed class World : IDisposable
{
    private readonly Dictionary<string, Dimension> _dimensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWorldProvider _provider;
    private bool _disposed;

    public World(string identifier, long seed, IWorldProvider provider)
    {
        Identifier = identifier;
        Seed = seed;
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public string Identifier { get; }

    public long Seed { get; }

    public IWorldProvider Provider => _provider;

    public IReadOnlyDictionary<string, Dimension> Dimensions => _dimensions;

    public Dimension GetDimension(string identifier)
    {
        if (!_dimensions.TryGetValue(identifier, out Dimension? dimension))
        {
            throw new InvalidOperationException($"Unknown dimension '{identifier}'.");
        }

        return dimension;
    }

    public Dimension AddDimension(DimensionConfig config, Regionizer regionizer)
    {
        ArgumentNullException.ThrowIfNull(config);
        var dimension = new Dimension(config.Identifier, config.Type, config.Generator, regionizer, _provider);
        _dimensions[config.Identifier] = dimension;
        return dimension;
    }

    public static World CreateFromConfig(WorldDefaultSettingsConfig settings, Regionizer regionizer, IWorldProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        provider ??= new InMemoryWorldProvider();
        var world = new World(settings.Identifier, settings.Seed, provider);

        IEnumerable<DimensionConfig> dims = settings.Dimensions.Count > 0
            ? settings.Dimensions
            : [new DimensionConfig()];

        foreach (DimensionConfig dim in dims)
        {
            world.AddDimension(dim, regionizer);
        }

        return world;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _provider.Dispose();
    }
}
