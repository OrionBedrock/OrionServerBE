using Orion.Config;
using Orion.Entity.Animation;
using Orion.Entity.Traits;
using Orion.Region;
using Orion.World.Provider;
using Xunit;
using EntityHandle = Orion.Entity.Entity;

namespace Orion.World.Tests;

public sealed class AnimationControllerEffectTests
{
    [Fact]
    public void OnEntry_EmitsAnimateAndSound()
    {
        using EntityFixture fixture = CreateFixture();
        var sink = new RecordingAnimationEffectSink();

        AnimationControllerDefinition definition = AnimationController.Create("test:fx")
            .Initial("strike")
            .State("strike", s => s
                .OnEntry(ctx =>
                {
                    ctx.EmitAnimate("animation.orion.strike");
                    ctx.EmitSound("mob.player.attack.strong");
                    ctx.EmitParticle("minecraft:critical_hit_emitter");
                })
                .When(_ => false, "strike"))
            .Build();

        fixture.Registry.Register(definition);
        AnimationControllerTrait trait = fixture.Entity.Traits.GetOrAdd(
            e => new AnimationControllerTrait(e, fixture.Registry, fixture.Regionizer, sink));
        trait.Attach("test:fx");

        using (fixture.Region.TryMarkTickingWithOwnership())
        {
            trait.Tick();
        }

        Assert.Equal(["animation.orion.strike"], sink.Animations);
        Assert.Single(sink.Sounds);
        Assert.StartsWith("mob.player.attack.strong@", sink.Sounds[0]);
        Assert.Single(sink.Particles);
        Assert.StartsWith("minecraft:critical_hit_emitter@", sink.Particles[0]);
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
