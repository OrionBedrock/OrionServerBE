using Orion.Api.Events;
using Orion.PluginContracts;
using Orion.PluginContracts.Events;
using Orion.PluginContracts.Network;
using Orion.Plugins;
using Orion.Plugins.Network;

namespace Orion.Game.Tests;

[Collection("PluginHost")]
public sealed class PacketPipelineTests
{
    public PacketPipelineTests()
    {
        PluginHost.ResetForTests();
    }

    [Fact]
    public void HasReceiveInterest_FalseWhenEmpty()
    {
        PacketPipeline pipeline = new();
        Assert.False(pipeline.HasReceiveInterest(9));
        Assert.False(pipeline.HasSendInterest(9));
    }

    [Fact]
    public void DispatchReceive_Cancel_ReturnsFalse()
    {
        PacketPipeline pipeline = new();
        FakePlugin plugin = new("p1");
        pipeline.OnReceive(new PacketReceiveHook
        {
            Plugin = plugin,
            PacketIdFilter = 42,
            Priority = EventPriority.High,
            Handler = ctx => ctx.Cancel()
        });

        PacketReceiveContext context = new()
        {
            Connection = new StubConnection(),
            PacketId = 42,
            Payload = new byte[] { 1, 2, 3 }
        };

        Assert.False(pipeline.DispatchReceive(context));
        Assert.True(context.Cancelled);
    }

    [Fact]
    public void TryOwnHandler_SecondOwnerRejected_FirstHandledSkipsCore()
    {
        PacketPipeline pipeline = new();
        FakePlugin first = new("owner");
        FakePlugin second = new("other");

        Assert.True(pipeline.TryOwnHandler(7, first, ctx => ctx.Handled = true));
        Assert.False(pipeline.TryOwnHandler(7, second, _ => { }));

        PacketReceiveContext context = new()
        {
            Connection = new StubConnection(),
            PacketId = 7,
            Payload = ReadOnlyMemory<byte>.Empty
        };

        Assert.False(pipeline.DispatchReceive(context));
        Assert.True(context.Handled);
    }

    [Fact]
    public void SubscribeAll_SetsInterestForArbitraryIds()
    {
        PacketPipeline pipeline = new();
        FakePlugin plugin = new("all");
        pipeline.OnReceive(new PacketReceiveHook
        {
            Plugin = plugin,
            PacketIdFilter = null,
            Handler = _ => { }
        });

        Assert.True(pipeline.HasReceiveInterest(1));
        Assert.True(pipeline.HasReceiveInterest(999));
    }

    [Fact]
    public void DispatchSend_Cancel_ReturnsFalse()
    {
        PacketPipeline pipeline = new();
        FakePlugin plugin = new("s1");
        pipeline.OnSend(new PacketSendHook
        {
            Plugin = plugin,
            PacketIdFilter = 10,
            Handler = ctx => ctx.Cancel()
        });

        PacketSendContext context = new()
        {
            Connection = new StubConnection(),
            PacketId = 10,
            Payload = new byte[] { 9 }
        };

        Assert.False(pipeline.DispatchSend(context, out _));
        Assert.True(context.Cancelled);
    }

    [Fact]
    public void DispatchSend_Replacement_ChangesPayload()
    {
        PacketPipeline pipeline = new();
        FakePlugin plugin = new("s2");
        byte[] replacement = [0xAA, 0xBB];
        pipeline.OnSend(new PacketSendHook
        {
            Plugin = plugin,
            PacketIdFilter = 11,
            Handler = ctx => ctx.ReplacementPayload = replacement
        });

        PacketSendContext context = new()
        {
            Connection = new StubConnection(),
            PacketId = 11,
            Payload = new byte[] { 1 }
        };

        Assert.True(pipeline.DispatchSend(context, out ReadOnlyMemory<byte> payload));
        Assert.True(payload.Span.SequenceEqual(replacement));
    }

    [Fact]
    public void RemovePlugin_ClearsHooksAndOwners()
    {
        PacketPipeline pipeline = new();
        FakePlugin plugin = new("gone");
        pipeline.OnReceive(new PacketReceiveHook
        {
            Plugin = plugin,
            PacketIdFilter = 5,
            Handler = _ => { }
        });
        Assert.True(pipeline.TryOwnHandler(5, plugin, _ => { }));
        Assert.True(pipeline.HasReceiveInterest(5));

        pipeline.RemovePlugin(plugin.Id);
        Assert.False(pipeline.HasReceiveInterest(5));
    }

    [Fact]
    public void EnableAll_ExposesPacketsOnContext()
    {
        CapturingPlugin plugin = new();
        PluginHost.RegisterLoadedForTests(
            plugin,
            new PluginManifest
            {
                Id = "pkt",
                Version = new Version(1, 0, 0),
                Main = "CapturingPlugin"
            });

        Server server = new();
        PluginHost.EnableAll(server);

        Assert.NotNull(plugin.Packets);
        Assert.Same(server.PacketPipeline, plugin.Packets);

        PluginHost.DisableAll();
    }

    sealed class StubConnection : IPlayerConnection
    {
        public object? Native => null;
    }

    sealed class FakePlugin(string id) : IOrionPlugin
    {
        public string Id { get; } = id;
        public Version Version => new(1, 0, 0);
        public void Load(IPluginLoadContext context) { }
        public void OnEnable(IPluginContext context) { }
        public void OnWorldInitialize(IWorldInitContext context) { }
        public void OnDisable(IPluginContext context) { }
    }

    sealed class CapturingPlugin : IOrionPlugin
    {
        public string Id => "pkt";
        public Version Version => new(1, 0, 0);
        public IPacketPipeline? Packets { get; private set; }

        public void Load(IPluginLoadContext context) { }
        public void OnEnable(IPluginContext context) => Packets = context.Packets;
        public void OnWorldInitialize(IWorldInitContext context) { }
        public void OnDisable(IPluginContext context) { }
    }
}
