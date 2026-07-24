using Orion.Config;
using Orion.Entity.Animation;
using Orion.Entity.Traits;
using Orion.Region;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class AnimationControllerRuntimeTests
{
    [Fact]
    public void IdleActive_TransitionsOnTimer()
    {
        using EntityFixture fixture = CreateFixture();
        var entered = new List<string>();
        var exited = new List<string>();

        AnimationControllerDefinition definition = AnimationController.Create("test:idle_active")
            .Initial("idle")
            .State("idle", s => s
                .OnEntry(ctx => entered.Add(ctx.CurrentState))
                .OnExit(ctx => exited.Add(ctx.CurrentState))
                .OnTick(ctx => ctx.SetLong("t", ctx.TicksInState + 1))
                .When(ctx => ctx.GetLong("t") >= 2, "active"))
            .State("active", s => s
                .OnEntry(ctx => entered.Add(ctx.CurrentState))
                .OnExit(ctx => exited.Add(ctx.CurrentState))
                .When(ctx => ctx.GetBool("done"), "idle"))
            .Build();

        fixture.Registry.Register(definition);
        AnimationControllerTrait trait = fixture.Entity.Traits.GetOrAdd(
            e => new AnimationControllerTrait(e, fixture.Registry, fixture.Regionizer));
        AnimationControllerInstance instance = trait.Attach("test:idle_active");

        using (fixture.Region.TryMarkTickingWithOwnership())
        {
            Assert.Equal("idle", instance.CurrentState);
            trait.Tick(); // enter idle; OnTick t=1; no transition
            trait.Tick(); // OnTick t=2; transition to active (OnEntry deferred to next tick)
            Assert.Equal("active", instance.CurrentState);
            trait.Tick(); // OnEntry active
        }

        Assert.Contains("idle", entered);
        Assert.Contains("idle", exited);
        Assert.Contains("active", entered);
    }

    [Fact]
    public void Tick_WithoutOwnership_IsNoOp()
    {
        using EntityFixture fixture = CreateFixture();
        AnimationControllerDefinition definition = AnimationController.Create("test:guard")
            .Initial("idle")
            .State("idle", s => s
                .OnTick(ctx => ctx.SetLong("ticks", ctx.GetLong("ticks") + 1))
                .When(ctx => ctx.GetLong("ticks") >= 1, "done"))
            .State("done", s => { })
            .Build();

        fixture.Registry.Register(definition);
        AnimationControllerTrait trait = fixture.Entity.Traits.GetOrAdd(
            e => new AnimationControllerTrait(e, fixture.Registry, fixture.Regionizer));
        AnimationControllerInstance instance = trait.Attach("test:guard");

        // No ownership enter — Folia no-op
        trait.Tick();
        Assert.Equal("idle", instance.CurrentState);
        Assert.Equal(0, instance.TicksInState);
    }

    [Fact]
    public void Registry_IdleWithZeroControllers()
    {
        var registry = new AnimationControllerRegistry();
        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet("missing", out _));
    }

    private static EntityFixture CreateFixture()
    {
        var regionizer = new Regionizer(new RegionizerOptions(0, mergeRadiusSections: 0));
        ChunkRegion region = regionizer.AddChunk(0, 0);
        var config = new OrionConfig();
        config.Server.WorldDefaultSettings.Dimensions.Add(new DimensionConfig
        {
            Identifier = "overworld",
            SpawnPosition = [0, 64, 0],
        });
        var provider = new InMemoryWorldProvider();
        var world = Orion.World.World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);
        var entity = new EntityHandle(1, world.GetDimension("overworld"), 0, 0, world.Identifier);
        return new EntityFixture(world, provider, regionizer, region, new AnimationControllerRegistry(), entity);
    }

    private sealed class EntityFixture : IDisposable
    {
        private readonly InMemoryWorldProvider _provider;

        public EntityFixture(
            Orion.World.World world,
            InMemoryWorldProvider provider,
            Regionizer regionizer,
            ChunkRegion region,
            AnimationControllerRegistry registry,
            EntityHandle entity)
        {
            World = world;
            _provider = provider;
            Regionizer = regionizer;
            Region = region;
            Registry = registry;
            Entity = entity;
        }

        public Orion.World.World World { get; }
        public Regionizer Regionizer { get; }
        public ChunkRegion Region { get; }
        public AnimationControllerRegistry Registry { get; }
        public EntityHandle Entity { get; }

        public void Dispose()
        {
            World.Dispose();
            _provider.Dispose();
        }
    }
}
