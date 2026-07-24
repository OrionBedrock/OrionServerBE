using Orion.Config;
using Orion.Network;
using Orion.Region;
using Orion.Runtime;
using RakNet;

namespace Orion;

public sealed class Server : IAsyncDisposable
{
    private readonly OrionConfig _config;
    private readonly SessionManager _sessions = new();
    private readonly SessionPacketQueue _packetQueue = new();
    private readonly SessionWorkQueue _workQueue = new();
    private readonly GlobalRegion _globalRegion = new();
    private Regionizer? _regionizer;
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
    }

    public string Name { get; }

    public OrionConfig Config => _config;

    public NetworkServer? RakNet => _raknet;

    public SessionManager Sessions => _sessions;

    public GlobalRegion GlobalRegion => _globalRegion;

    /// <summary>
    /// Chunk section regionizer (idle until world/tickets land in later phases).
    /// </summary>
    public Regionizer? Regionizer => _regionizer;

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

        _sender = new PacketSender(_config);
        _context = new ServerContext(_config, _sessions, _sender, _packetQueue, _workQueue);
        _dispatcher = new SessionDispatcher(_context);

        _raknet = new NetworkServer(options);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Folia check: RakNet I/O only creates sessions / enqueues bytes. Drain runs on the global tick.
        _raknet.OnConnected += connection => _sessions.Create(connection);
        _raknet.OnDisconnected += connection => _sessions.Remove(connection);
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

        _threadPools?.Dispose();
        _threadPools = null;

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
        // Folia check: session drain + RakNet timers run on the global region tick thread (RegionTick pool).
        _globalRegion.Drain();
        _raknet?.Tick();
        _dispatcher?.Drain();
    }
}
