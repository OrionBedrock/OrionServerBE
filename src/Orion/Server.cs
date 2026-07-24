using Orion.Config;
using Orion.Entity.Animation;
using Orion.Network;
using Orion.Permissions;
using Orion.Player;
using Orion.Player.Traits;
using Orion.Region;
using Orion.Registries;
using Orion.Runtime;
using Orion.Scheduler;
using Orion.World.Generation;
using Orion.World.Persistence;
using Orion.World.Provider;
using Orion.World.Provider.LevelDb;
using RakNet;

namespace Orion;

public sealed class Server : IAsyncDisposable
{
    private readonly OrionConfig _config;
    private readonly SessionManager _sessions = new();
    private readonly SessionPacketQueue _packetQueue = new();
    private readonly SessionWorkQueue _workQueue = new();
    private readonly PlayerManager _players = new();
    private readonly GlobalRegion _globalRegion = new();
    private readonly GlobalRegionScheduler _globalScheduler;
    private Regionizer? _regionizer;
    private Orion.World.World? _world;
    private GeneratorRegistry? _generators;
    private WorldPersistence? _persistence;
    private PlayerPersistence? _playerPersistence;
    private PermissionService? _permissions;
    private ServerRegistries? _registries;
    private AnimationControllerRegistry? _animationControllers;
    private PacketSender? _sender;
    private ServerContext? _context;
    private SessionDispatcher? _dispatcher;
    private NetworkServer? _raknet;
    private OrionThreadPools? _threadPools;
    private RegionTickScheduler? _regionTickScheduler;
    private CancellationTokenSource? _lifetime;

    public Server(OrionConfig config)
    {
        _config = config;
        Name = config.Server.Name;
        _globalScheduler = new GlobalRegionScheduler(_globalRegion);
    }

    public string Name { get; }

    public OrionConfig Config => _config;

    public NetworkServer? RakNet => _raknet;

    public SessionManager Sessions => _sessions;

    public PlayerManager Players => _players;

    public PermissionService? Permissions => _permissions;

    public ServerRegistries? Registries => _registries;

    public AnimationControllerRegistry? AnimationControllers => _animationControllers;

    public GlobalRegion GlobalRegion => _globalRegion;

    public GlobalRegionScheduler GlobalScheduler => _globalScheduler;

    public Regionizer? Regionizer => _regionizer;

    public Orion.World.World? World => _world;

    public GeneratorRegistry? Generators => _generators;

    public PlayerPersistence? PlayerPersistence => _playerPersistence;

    public OrionThreadPools? ThreadPools => _threadPools;

    public ThreadPoolBudget? ThreadBudget => _threadPools?.Budget;


    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_raknet is not null)
        {
            throw new InvalidOperationException("Server already started.");
        }

        var rak = _config.Server.Raknet;
        var options = RaknetServerOptions.Default.With(
            address: rak.Address,
            portIpv4: rak.PortIPV4,
            portIpv6: rak.PortIPV6,
            message: rak.Message,
            maxConnections: rak.MaxConnections,
            mtuMaxSize: rak.MtuMaxSize,
            mtuMinSize: rak.MtuMinSize,
            validatePort: rak.ValidatePort,
            motd: _config.Server.Motd,
            edition: _config.Server.Edition);

        var budget = ThreadPoolBudget.Resolve(_config.Runtime.Threads);
        _threadPools = new OrionThreadPools(budget);
        _regionTickScheduler = new RegionTickScheduler(_threadPools, _config.Runtime.Regions.Scheduler);
        _regionizer = new Regionizer(RegionizerOptions.FromGridExponent(_config.Runtime.Regions.GridExponent));
        _generators = GeneratorRegistry.CreateDefault();
        var regionScheduler = new RegionScheduler(_regionizer);
        var pipeline = new ChunkLoadPipeline(
            _regionizer,
            regionScheduler,
            _generators,
            _threadPools);

        string worldId = _config.Server.WorldDefaultSettings.Identifier;
        string dbPath = Path.Combine("worlds", worldId, "db");
        IWorldProvider provider = new LevelDbWorldProvider(dbPath);
        _persistence = new WorldPersistence(provider, _threadPools);
        _playerPersistence = new PlayerPersistence(provider, _threadPools);
        _players.BindPersistence(provider, _playerPersistence);
        _world = Orion.World.World.CreateFromConfig(
            _config.Server.WorldDefaultSettings,
            _regionizer,
            provider,
            pipeline,
            _persistence);

        _sender = new PacketSender(_config);
        PacketSendGate.Bind(_sender);
        _permissions = LoadPermissions(_config.Server.Orion.Permissions);
        _registries = ServerRegistries.CreateMinimal();
        _animationControllers = new AnimationControllerRegistry();
        _context = new ServerContext(
            _config,
            _sessions,
            _sender,
            _packetQueue,
            _workQueue,
            _players,
            _world,
            _regionizer,
            _permissions,
            _registries);
        _dispatcher = new SessionDispatcher(_context);

        _raknet = new NetworkServer(options);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Folia check: RakNet I/O only creates sessions / enqueues bytes.
        _raknet.OnConnected += connection => _sessions.Create(connection);
        _raknet.OnDisconnected += connection =>
        {
            _players.Remove(connection);
            _sessions.Remove(connection);
        };
        _raknet.OnMessage += (connection, payload) => _packetQueue.Enqueue(connection, payload);

        await _raknet.Start(_lifetime.Token).ConfigureAwait(false);

        _regionTickScheduler.Start(
            _globalRegion,
            ExecuteGlobalTick,
            _config.Server.Orion.TicksPerSecond,
            _lifetime.Token);
    }

    public async ValueTask StopAsync()
    {
        if (_lifetime is not null)
        {
            await _lifetime.CancelAsync().ConfigureAwait(false);
        }

        _regionTickScheduler?.Stop();
        _regionTickScheduler?.Dispose();
        _regionTickScheduler = null;

        try
        {
            _players.FlushDirtyPlayers();
            _persistence?.Flush(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Best-effort flush before tearing down pools.
        }

        _threadPools?.Dispose();
        _threadPools = null;

        _world?.Dispose();
        _world = null;
        _playerPersistence?.Dispose();
        _playerPersistence = null;
        _persistence = null;
        _generators = null;
        _permissions = null;
        _animationControllers = null;
        _registries = null;

        _raknet?.Stop();
        _raknet?.Dispose();
        _raknet = null;

        _lifetime?.Dispose();
        _lifetime = null;
        _dispatcher = null;
        _context = null;
        _sender = null;
        _regionizer = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private void ExecuteGlobalTick()
    {
        // Global schedulers + RakNet timers on the global tick thread.
        // In-game packet dispatch + region scheduler drain happen on player regions.
        _globalScheduler.Tick();
        _globalRegion.Drain();
        _raknet?.Tick();
        _dispatcher?.Drain();
        _players.TickAllRegions();
    }

    private static PermissionService LoadPermissions(string path)
    {
        try
        {
            return PermissionService.Load(path);
        }
        catch (FileNotFoundException)
        {
            return PermissionService.CreateEmpty();
        }
    }
}
