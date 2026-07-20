using Orion.Api;
using Orion.Api.Events;
using Orion.Api.Items;
using Orion.Api.Math;
using Orion.PluginContracts.Events;
using Orion.Plugins;
using PlayerEntity = Orion.Player.Player;

namespace Orion.Game.Tests;

[Collection("PluginHost")]
public sealed class ApiEventsTests
{
    [Fact]
    public void GameplaySignals_LiveInOrionApiAssembly()
    {
        Assert.Equal("Orion.Api", typeof(PlayerPlaceBlockSignal).Assembly.GetName().Name);
        Assert.Equal("Orion.Api", typeof(PlayerFoodEatSignal).Assembly.GetName().Name);
        Assert.Equal("Orion.Api", typeof(EntityHurtSignal).Assembly.GetName().Name);
        Assert.Same(typeof(IServer).Assembly, typeof(PlayerPlaceBlockSignal).Assembly);
    }

    [Fact]
    public void Cancel_PlayerPlaceBlock_PreventsApplication()
    {
        Server server = new();
        PlayerEntity player = new("tester", "0", Guid.NewGuid());
        bool applied = false;

        server.On<PlayerPlaceBlockSignal>(ServerEvent.PlayerPlaceBlock, s => s.Cancel(), EventPriority.High);

        PlayerPlaceBlockSignal signal = new(player, new BlockPos(1, 64, 2), blockFace: 1);
        server.Emit(signal);
        if (signal.Emit())
        {
            applied = true;
        }

        Assert.True(signal.Cancelled);
        Assert.False(signal.Emit());
        Assert.False(applied);
    }

    [Fact]
    public void Cancel_PlayerFoodEat_PreventsHungerChange()
    {
        Server server = new();
        PlayerEntity player = new("tester", "0", Guid.NewGuid());
        IItemStack food = new StubFoodStack();

        float hunger = 10f;
        server.On<PlayerFoodEatSignal>(ServerEvent.PlayerFoodEat, s => s.Cancel(), EventPriority.High);

        PlayerFoodEatSignal signal = new(player, food);
        server.Emit(signal);
        if (signal.Emit())
        {
            hunger = Math.Clamp(hunger + 4f, 0f, 20f);
        }

        Assert.True(signal.Cancelled);
        Assert.False(signal.Emit());
        Assert.Equal(10f, hunger);
    }

    [Fact]
    public void EventBus_Subscribe_PlaceAndEat_WithoutOrionEventsNamespace()
    {
        Server server = new();
        IEventBus bus = new ServerEventBus(server);
        PlayerEntity player = new("tester", "0", Guid.NewGuid());
        int placeCalls = 0;
        int eatCalls = 0;

        bus.Subscribe<PlayerPlaceBlockSignal>(_ => placeCalls++, EventPriority.Normal);
        bus.Subscribe<PlayerFoodEatSignal>(_ => eatCalls++, EventPriority.Normal);

        server.Emit(new PlayerPlaceBlockSignal(player, new BlockPos(0, 0, 0), 0));
        server.Emit(new PlayerFoodEatSignal(player, new StubFoodStack()));

        Assert.Equal(1, placeCalls);
        Assert.Equal(1, eatCalls);
    }

    sealed class StubFoodType : IItemType
    {
        public string Identifier => "test:food";
    }

    sealed class StubFoodStack : IItemStack
    {
        public IItemType Type { get; } = new StubFoodType();
        public int Count => 1;
    }
}
