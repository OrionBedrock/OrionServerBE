using System.Runtime.InteropServices;
using Orion;
using Orion.Config;

var configPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "config", "server.json");

if (!File.Exists(configPath))
{
    var cwdFallback = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "config", "server.json"));
    if (File.Exists(cwdFallback))
    {
        configPath = cwdFallback;
    }
}

if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Config not found: {configPath}");
    return 1;
}

var config = OrionConfig.Load(configPath);
await using var server = new Server(config);

using var cts = new CancellationTokenSource();
void RequestStop()
{
    try
    {
        cts.Cancel();
    }
    catch (ObjectDisposedException)
    {
    }
}

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    RequestStop();
};

using PosixSignalRegistration? sigTerm = OperatingSystem.IsWindows()
    ? null
    : PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
    {
        ctx.Cancel = true;
        RequestStop();
    });

await server.StartAsync(cts.Token).ConfigureAwait(false);

var endpoint = server.RakNet?.LocalEndPoint;
Console.WriteLine($"{server.Name} listening on {endpoint} (UDP). Press Ctrl+C to stop.");
Console.Out.Flush();

try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
}

await server.StopAsync().ConfigureAwait(false);
Console.WriteLine($"{server.Name} stopped.");
return 0;
