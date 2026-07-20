using System.Collections.Concurrent;
using Orion.Api;
using Orion.Commands;
using Orion.Config;
using Orion.Events;
using Orion.Network;
using Orion.Player;
using Orion.Plugins.Api;
using Orion.Plugins.Network;
using Orion.Protocol.Packets;
using Orion.RakNet;
using Orion.Scheduling;
using WorldInstance = Orion.World.World;

namespace Orion;

public sealed class Server : IServer
{
    private readonly INetworkScheduler _scheduler;
    private readonly IAreaScheduler _areaScheduler;
    private readonly ConnectionCoordinator? _connectionCoordinator;
    private readonly Dictionary<ServerEvent, List<HandlerEntry>> _signalHandlers = [];
    private readonly object _handlersLock = new();

    public ServerProperties Properties { get; }

    public WorldInstance? World { get; private set; }

    public ConcurrentDictionary<NetworkConnection, PlayerSession> Sessions { get; } = new();

    public NetworkHandler Network { get; }

    public CommandRegistry Commands { get; } = new();

    public INetworkScheduler Scheduler => _scheduler;

    public IAreaScheduler AreaScheduler => _areaScheduler;

    public ConnectionCoordinator? ConnectionCoordinator => _connectionCoordinator;

    public PacketIngress PacketIngress { get; }

    /// <summary>Plugin packet hooks; no-op empty pipeline until PluginHost assigns one.</summary>
    public PacketPipeline PacketPipeline { get; set; } = new();

    public double Tps { get; private set; } = 20.0;

    public IReadOnlyCollection<IPlayer> OnlinePlayers
    {
        get
        {
            List<IPlayer> players = [];
            foreach (PlayerSession session in Sessions.Values)
            {
                if (session.ActiveEntity is { } player)
                {
                    players.Add(player);
                }
            }

            return players;
        }
    }

    public IWorld? DefaultWorld => World is null ? null : WorldApi.For(World);

    public IWorld? GetWorld(string name)
    {
        if (World is null)
        {
            return null;
        }

        return string.Equals(World.Name, name, StringComparison.OrdinalIgnoreCase)
            ? WorldApi.For(World)
            : null;
    }

    public IReadOnlyCollection<IWorld> Worlds =>
        World is null ? [] : [WorldApi.For(World)];

    public Server(ServerProperties? properties = null)
    {
        Properties = properties ?? new ServerProperties();

        if (Properties.AreaThreadCount < 1)
        {
            throw new InvalidOperationException("area-thread-count must be >= 1.");
        }

        Network = new NetworkHandler(this);
        _scheduler = new SingleThreadScheduler(this);
        _areaScheduler = Properties.AreaThreadingEnabled
            ? new AreaScheduler(this)
            : new NoopAreaScheduler();

        if (Properties.SessionThreadingEnabled)
        {
            if (!Properties.AreaThreadingEnabled)
            {
                throw new InvalidOperationException("session-threading-enabled requires area-threading-enabled.");
            }

            if (Properties.SessionThreadCount < 1)
            {
                throw new InvalidOperationException("session-thread-count must be >= 1.");
            }

            _connectionCoordinator = new ConnectionCoordinator(this, new SessionWorkerPool(Properties.SessionThreadCount, this));
        }

        PacketIngress = new PacketIngress(this);
        Commands.RegisterDefaultCommands();
    }

    public bool TryGetWorld(string identifier, out WorldInstance? world)
    {
        world = World;
        if (world is null)
        {
            return false;
        }

        return string.Equals(world.Name, identifier, StringComparison.OrdinalIgnoreCase);
    }

    public void SetWorld(WorldInstance world)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        world.Server = this;
    }

    public WorldInstance GetWorld() => World ?? throw new InvalidOperationException("World has not been set.");

    public void On<TSignal>(ServerEvent @event, Action<TSignal> handler) where TSignal : ISignal =>
        On(@event, handler, EventPriority.Normal);

    public void On<TSignal>(ServerEvent @event, Action<TSignal> handler, EventPriority priority)
        where TSignal : ISignal
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_handlersLock)
        {
            if (!_signalHandlers.TryGetValue(@event, out List<HandlerEntry>? handlers))
            {
                handlers = [];
                _signalHandlers[@event] = handlers;
            }

            handlers.Add(new HandlerEntry(priority, handler, priority == EventPriority.Monitor));
        }
    }

    public void Off<TSignal>(ServerEvent @event, Action<TSignal> handler) where TSignal : ISignal
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_handlersLock)
        {
            if (!_signalHandlers.TryGetValue(@event, out List<HandlerEntry>? handlers))
            {
                return;
            }

            handlers.RemoveAll(entry => ReferenceEquals(entry.Handler, handler));
            if (handlers.Count == 0)
            {
                _signalHandlers.Remove(@event);
            }
        }
    }

    public void Emit(ServerEvent @event, ISignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (SignalAffinity.IsGlobalEvent(signal))
        {
            EmitHandlersInline(@event, signal);
            return;
        }

        AreaHandle? area = SignalAffinity.TryResolveArea(this, signal);
        if (area.HasValue && Properties.AreaThreadingEnabled && AreaScheduler.IsActive)
        {
            AreaScheduler.RunOnAreaThread(area.Value, () => EmitHandlersInline(@event, signal));
            return;
        }

        EmitHandlersInline(@event, signal);
    }

    public void Emit(ISignal signal) => Emit(signal.Event, signal);

    public void RunOnWorldThread(WorldInstance world, Action action)
    {
        _ = world;
        action();
    }

    public void RunOnAreaThread(AreaHandle area, Action action) =>
        AreaScheduler.RunOnAreaThread(area, action);

    public void RunOnPlayerAreaThread(Player.Player player, Action action)
    {
        if (player.Dimension is not Orion.World.Dimension dimension)
        {
            action();
            return;
        }

        int areaIndex = dimension.ShardManager.ResolveShard((int)player.Position.X >> 4, (int)player.Position.Z >> 4).AreaIndex;
        RunOnAreaThread(new AreaHandle(dimension, areaIndex), action);
    }

    public void Broadcast(DataPacket packet, params Player.Player[]? exclude)
    {
        WorldInstance world = GetWorld();
        Orion.Entity.Entity[]? exceptEntities = exclude is null ? null : [.. exclude];
        foreach (Orion.World.Dimension dimension in world.Dimensions)
        {
            BroadcastService.Broadcast(dimension, packet, exceptEntities is null ? null : new Orion.World.BroadcastOptions { Except = exceptEntities });
        }
    }

    internal void SetTps(double tps) => Tps = tps;

    void EmitHandlersInline(ServerEvent @event, ISignal signal)
    {
        HandlerEntry[] snapshot;
        lock (_handlersLock)
        {
            if (!_signalHandlers.TryGetValue(@event, out List<HandlerEntry>? handlers) || handlers.Count == 0)
            {
                return;
            }

            snapshot = handlers.ToArray();
        }

        Array.Sort(snapshot, static (a, b) =>
        {
            int cmp = ComparePriority(a.Priority, b.Priority);
            return cmp != 0 ? cmp : a.Sequence.CompareTo(b.Sequence);
        });

        for (int i = 0; i < snapshot.Length; i++)
        {
            HandlerEntry entry = snapshot[i];
            Type? signalType = entry.Handler.Method.GetParameters().FirstOrDefault()?.ParameterType;
            if (signalType is null || !signalType.IsInstanceOfType(signal))
            {
                continue;
            }

            if (entry.IsMonitor && signal is ICancellable cancellable)
            {
                bool before = cancellable.Cancelled;
                entry.Handler.DynamicInvoke(signal);
                if (!before && cancellable.Cancelled)
                {
                    RestoreCancelled(signal, cancelled: false);
                    Warn(
                        LogCategory.System,
                        "Monitor handler for {0} called Cancel(); cancel was ignored.",
                        @event);
                }

                continue;
            }

            entry.Handler.DynamicInvoke(signal);
        }
    }

    static int ComparePriority(EventPriority a, EventPriority b)
    {
        // Highest → Lowest, then Monitor last.
        int rankA = a == EventPriority.Monitor ? int.MinValue : (int)a;
        int rankB = b == EventPriority.Monitor ? int.MinValue : (int)b;
        return rankB.CompareTo(rankA);
    }

    static void RestoreCancelled(ISignal signal, bool cancelled)
    {
        switch (signal)
        {
            case PlayerChatSignal chat:
                chat.SetCancelled(cancelled);
                break;
            case PlayerJoinSignal join:
                join.SetCancelled(cancelled);
                break;
            case PlayerSpawnSignal spawn:
                spawn.SetCancelled(cancelled);
                break;
            case PlayerPlaceBlockSignal place:
                place.SetCancelled(cancelled);
                break;
            case PlayerBreakBlockSignal brk:
                brk.SetCancelled(cancelled);
                break;
            case PlayerOpenInventorySignal openInv:
                openInv.SetCancelled(cancelled);
                break;
            case PlayerOpenContainerSignal openCtr:
                openCtr.SetCancelled(cancelled);
                break;
            case EntityHurtSignal hurt:
                hurt.SetCancelled(cancelled);
                break;
            case EntityDieSignal die:
                die.SetCancelled(cancelled);
                break;
        }
    }

    private sealed class HandlerEntry
    {
        static int _nextSequence;

        public HandlerEntry(EventPriority priority, Delegate handler, bool isMonitor)
        {
            Priority = priority;
            Handler = handler;
            IsMonitor = isMonitor;
            Sequence = Interlocked.Increment(ref _nextSequence);
        }

        public EventPriority Priority { get; }
        public Delegate Handler { get; }
        public bool IsMonitor { get; }
        public int Sequence { get; }
    }
}
