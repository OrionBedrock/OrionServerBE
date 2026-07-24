using Orion.Config;
using RakNet;

namespace Orion;

public sealed class Server : IAsyncDisposable
{
    private readonly OrionConfig _config;
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

        _raknet = new NetworkServer(options);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Folia check: RakNet I/O stays off world mutation paths. Tick only drains RakNet timers/acks.
        _raknet.OnConnected += _ => { };
        _raknet.OnDisconnected += _ => { };
        _raknet.OnMessage += (_, _) => { };

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
