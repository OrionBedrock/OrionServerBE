using Orion.Config;
using Orion.Network;
using RakNet;

namespace Orion;

public sealed class Server : IAsyncDisposable
{
    private readonly OrionConfig _config;
    private readonly SessionManager _sessions = new();
    private readonly SessionPacketQueue _packetQueue = new();
    private readonly SessionWorkQueue _workQueue = new();
    private PacketSender? _sender;
    private ServerContext? _context;
    private SessionDispatcher? _dispatcher;
    private NetworkServer? _raknet;
    private CancellationTokenSource? _lifetime;
    private Task? _tickLoop;

    public Server(OrionConfig config)
    {
        _config = config;
        Name = config.Server.Name;
    }

    public string Name { get; }

    public OrionConfig Config => _config;

    public NetworkServer? RakNet => _raknet;

    public SessionManager Sessions => _sessions;

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

        _sender = new PacketSender(_config);
        _context = new ServerContext(_config, _sessions, _sender, _packetQueue, _workQueue);
        _dispatcher = new SessionDispatcher(_context);

        _raknet = new NetworkServer(options);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Folia check: RakNet I/O only creates sessions / enqueues bytes. Drain runs on the tick loop.
        _raknet.OnConnected += connection => _sessions.Create(connection);
        _raknet.OnDisconnected += connection => _sessions.Remove(connection);
        _raknet.OnMessage += (connection, payload) => _packetQueue.Enqueue(connection, payload);

        await _raknet.Start(_lifetime.Token).ConfigureAwait(false);

        _tickLoop = Task.Run(() => RunRakNetTickLoop(_lifetime.Token), CancellationToken.None);
    }

    public async ValueTask StopAsync()
    {
        if (_lifetime is not null)
        {
            await _lifetime.CancelAsync().ConfigureAwait(false);
        }

        if (_tickLoop is not null)
        {
            try
            {
                await _tickLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _raknet?.Stop();
        _raknet?.Dispose();
        _raknet = null;

        _lifetime?.Dispose();
        _lifetime = null;
        _tickLoop = null;
        _dispatcher = null;
        _context = null;
        _sender = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private void RunRakNetTickLoop(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromMilliseconds(50);
        while (!cancellationToken.IsCancellationRequested)
        {
            var started = Environment.TickCount64;
            try
            {
                _raknet?.Tick();
                // Folia check: session drain is the Phase 03 stand-in for global/region schedule.
                _dispatcher?.Drain();
            }
            catch (Exception)
            {
                // Keep the host alive; detailed logging arrives in a later phase.
            }

            var elapsed = Environment.TickCount64 - started;
            var remaining = interval.TotalMilliseconds - elapsed;
            if (remaining > 0)
            {
                Thread.Sleep((int)remaining);
            }
        }
    }
}
