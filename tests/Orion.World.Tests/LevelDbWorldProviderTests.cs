using Orion.Config;
using Orion.Region;
using Orion.Runtime;
using Orion.Scheduler;
using Orion.World;
using Orion.World.Chunk;
using Orion.World.Generation;
using Orion.World.Persistence;
using Orion.World.Provider.LevelDb;
using Xunit;

namespace Orion.World.Tests;

public sealed class LevelDbWorldProviderTests
{
    [Fact]
    public void RoundTrip_SaveAndLoadChunk()
    {
        string path = Path.Combine(Path.GetTempPath(), "orion-leveldb-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var provider = new LevelDbWorldProvider(path))
            {
                var chunk = new ChunkColumn(12, -3) { IsGenerated = true, IsDirty = true };
                provider.SaveChunk("overworld", chunk);
                Assert.False(chunk.IsDirty);
                Assert.True(provider.HasChunk("overworld", 12, -3));
            }

            using (var provider = new LevelDbWorldProvider(path))
            {
                ChunkColumn? loaded = provider.LoadChunk("overworld", 12, -3);
                Assert.NotNull(loaded);
                Assert.Equal(12, loaded.ChunkX);
                Assert.Equal(-3, loaded.ChunkZ);
                Assert.True(loaded.IsGenerated);
            }
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public void DirtySave_DoesNotBlockOnIoPool()
    {
        string path = Path.Combine(Path.GetTempPath(), "orion-leveldb-" + Guid.NewGuid().ToString("N"));
        var budget = ThreadPoolBudget.Resolve(new ThreadPoolsConfig
        {
            RegionTick = new ThreadLimitConfig { Max = 1 },
            Raknet = new ThreadLimitConfig { Max = 1 },
            ChunkIo = new ThreadLimitConfig { Max = 1 },
            ChunkWorkers = new ThreadLimitConfig { Max = 1 },
            IoPersistence = new ThreadLimitConfig { Max = 1 },
            AsyncScheduler = new ThreadLimitConfig { Max = 1 },
        }, processorCount: 8);

        try
        {
            using var pools = new OrionThreadPools(budget);
            using var provider = new LevelDbWorldProvider(path);
            using var persistence = new WorldPersistence(provider, pools);

            var chunk = new ChunkColumn(1, 2) { IsGenerated = true, IsDirty = true };
            long started = Environment.TickCount64;
            persistence.ScheduleSave("overworld", chunk);
            long elapsed = Environment.TickCount64 - started;

            // Scheduling must return immediately (IO runs on IoPersistence workers).
            Assert.True(elapsed < 200, $"ScheduleSave blocked for {elapsed}ms");

            persistence.Flush(TimeSpan.FromSeconds(5));
            Assert.True(provider.HasChunk("overworld", 1, 2));
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public void GeneratedChunk_PersistsViaPipeline()
    {
        string path = Path.Combine(Path.GetTempPath(), "orion-leveldb-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var provider = new LevelDbWorldProvider(path);
            using var persistence = new WorldPersistence(provider);
            var regionizer = new Regionizer(RegionizerOptions.FromGridExponent(4));
            var regionScheduler = new RegionScheduler(regionizer);
            var pipeline = new ChunkLoadPipeline(regionizer, regionScheduler, GeneratorRegistry.CreateDefault());
            using World world = World.CreateFromConfig(
                new WorldDefaultSettingsConfig
                {
                    Identifier = "default",
                    Dimensions = [new DimensionConfig { Identifier = "overworld", Generator = "void" }],
                },
                regionizer,
                provider,
                pipeline,
                persistence);

            using var ticket = world.GetDimension("overworld").AcquireTicket(9, 9);
            persistence.Flush(TimeSpan.FromSeconds(5));

            Assert.True(provider.HasChunk("overworld", 9, 9));
            ChunkColumn? loaded = provider.LoadChunk("overworld", 9, 9);
            Assert.NotNull(loaded);
            Assert.True(loaded.IsGenerated);
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
