using Orion.Api.Events;
using Orion.PluginContracts;
using Orion.PluginContracts.Events;
using Orion.Plugins;
using PlayerEntity = Orion.Player.Player;

namespace Orion.Game.Tests;

[Collection("PluginHost")]
public sealed class EventBusTests
{
    [Fact]
    public void Emit_InvokesHigherPriorityBeforeLower()
    {
        Server server = new();
        List<string> order = [];
        PlayerEntity player = new("tester", "0", Guid.NewGuid());

        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, _ => order.Add("low"), EventPriority.Low);
        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, _ => order.Add("high"), EventPriority.High);
        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, _ => order.Add("normal"), EventPriority.Normal);

        PlayerChatSignal signal = new(player, "hi", "<tester> hi");
        server.Emit(signal);

        Assert.Equal(["high", "normal", "low"], order);
    }

    [Fact]
    public void Emit_Cancel_SetsCancelled_AndEmitReturnsFalse()
    {
        Server server = new();
        PlayerEntity player = new("tester", "0", Guid.NewGuid());

        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, s => s.Cancel(), EventPriority.High);
        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, _ => { }, EventPriority.Low);

        PlayerChatSignal signal = new(player, "hi", "<tester> hi");
        server.Emit(signal);

        Assert.True(signal.Cancelled);
        Assert.False(signal.Emit());
    }

    [Fact]
    public void Off_RemovesHandler()
    {
        Server server = new();
        PlayerEntity player = new("tester", "0", Guid.NewGuid());
        int calls = 0;
        Action<PlayerChatSignal> handler = _ => calls++;

        server.On(ServerEvent.PlayerChat, handler, EventPriority.Normal);
        server.Off(ServerEvent.PlayerChat, handler);

        server.Emit(new PlayerChatSignal(player, "hi", "<tester> hi"));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Monitor_Cancel_IsIgnored()
    {
        Server server = new();
        PlayerEntity player = new("tester", "0", Guid.NewGuid());

        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, s => s.Cancel(), EventPriority.Monitor);

        PlayerChatSignal signal = new(player, "hi", "<tester> hi");
        server.Emit(signal);

        Assert.False(signal.Cancelled);
        Assert.True(signal.Emit());
    }

    [Fact]
    public void Monitor_RunsAfterMutatingPriorities()
    {
        Server server = new();
        PlayerEntity player = new("tester", "0", Guid.NewGuid());
        List<string> order = [];

        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, _ => order.Add("monitor"), EventPriority.Monitor);
        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, _ => order.Add("highest"), EventPriority.Highest);
        server.On<PlayerChatSignal>(ServerEvent.PlayerChat, _ => order.Add("lowest"), EventPriority.Lowest);

        server.Emit(new PlayerChatSignal(player, "hi", "<tester> hi"));
        Assert.Equal(["highest", "lowest", "monitor"], order);
    }

    [Fact]
    public void ServerEventBus_SubscribeUnsubscribe_Works()
    {
        Server server = new();
        IEventBus bus = new ServerEventBus(server);
        PlayerEntity player = new("tester", "0", Guid.NewGuid());
        int calls = 0;
        Action<PlayerChatSignal> handler = _ => calls++;

        bus.Subscribe(handler, EventPriority.Normal);
        server.Emit(new PlayerChatSignal(player, "a", "a"));
        Assert.Equal(1, calls);

        bus.Unsubscribe(handler);
        server.Emit(new PlayerChatSignal(player, "b", "b"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TrackingEventBus_UnsubscribeAll_RemovesHandlers()
    {
        Server server = new();
        TrackingEventBus bus = new(new ServerEventBus(server));
        PlayerEntity player = new("tester", "0", Guid.NewGuid());
        int calls = 0;

        bus.Subscribe<PlayerChatSignal>(_ => calls++, EventPriority.Normal);
        bus.UnsubscribeAll();
        server.Emit(new PlayerChatSignal(player, "hi", "hi"));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void EnableAll_ProvidesRealEventBus_NotNoOp()
    {
        PluginHost.ResetForTests();
        try
        {
            CapturingPlugin plugin = new();
            PluginHost.RegisterLoadedForTests(
                plugin,
                new PluginManifest
                {
                    Id = "test.events",
                    Version = new Version(1, 0, 0),
                    Main = "CapturingPlugin"
                });

            Server server = new();
            PluginHost.EnableAll(server);

            Assert.NotNull(plugin.Events);
            Assert.IsNotType<NoOpEventBus>(plugin.Events);
            Assert.IsType<TrackingEventBus>(plugin.Events);

            int calls = 0;
            plugin.Events.Subscribe<PlayerChatSignal>(_ => calls++, EventPriority.High);
            server.Emit(new PlayerChatSignal(new PlayerEntity("t", "0", Guid.NewGuid()), "x", "x"));
            Assert.Equal(1, calls);

            PluginHost.DisableAll();
            server.Emit(new PlayerChatSignal(new PlayerEntity("t", "0", Guid.NewGuid()), "y", "y"));
            Assert.Equal(1, calls);
        }
        finally
        {
            PluginHost.ResetForTests();
        }
    }

    sealed class CapturingPlugin : IOrionPlugin
    {
        public string Id => "test.events";
        public Version Version => new(1, 0, 0);
        public IEventBus? Events { get; private set; }

        public void Load(IPluginLoadContext context)
        {
        }

        public void OnEnable(IPluginContext context) => Events = context.Events;

        public void OnWorldInitialize(IWorldInitContext context)
        {
        }

        public void OnDisable(IPluginContext context)
        {
        }
    }
}
