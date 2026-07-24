using Orion.Player;
using Orion.World.Persistence;
using Orion.World.Provider;
using Orion.World.Provider.LevelDb;
using Xunit;

namespace Orion.World.Tests;

public sealed class PlayerKvStoreTests
{
    [Fact]
    public void InMemory_SetGetSurvivesReload()
    {
        using var provider = new InMemoryWorldProvider();
        using var persistence = new PlayerPersistence(provider);

        var store = new PlayerDataStore();
        store.SetString("myplugin:kingdom", "north");
        store.SetLong("myplugin:playtime_minutes", 120);
        persistence.ScheduleSave("offline-xuid-abc", store);
        persistence.Flush();

        var reloaded = new PlayerDataStore();
        Assert.True(provider.TryLoadPlayerBlob("offline-xuid-abc", out byte[]? blob));
        Assert.NotNull(blob);
        reloaded.LoadFromSnapshot(blob);

        Assert.True(reloaded.TryGetString("myplugin:kingdom", out string? kingdom));
        Assert.Equal("north", kingdom);
        Assert.True(reloaded.TryGetLong("myplugin:playtime_minutes", out long playtime));
        Assert.Equal(120, playtime);
    }

    [Fact]
    public void LevelDb_SetGetSurvivesReopen()
    {
        string path = Path.Combine(Path.GetTempPath(), "orion-player-kv-" + Guid.NewGuid().ToString("N"));
        string xuid = Guid.NewGuid().ToString("N");
        try
        {
            using (var provider = new LevelDbWorldProvider(path))
            using (var persistence = new PlayerPersistence(provider))
            {
                var store = new PlayerDataStore();
                store.SetString("myplugin:kingdom", "south");
                store.SetLong("myplugin:playtime_minutes", 42);
                persistence.ScheduleSave(xuid, store);
                persistence.Flush();
            }

            using (var provider = new LevelDbWorldProvider(path))
            {
                Assert.True(provider.TryLoadPlayerBlob(xuid, out byte[]? blob));
                Assert.NotNull(blob);
                var reloaded = new PlayerDataStore();
                reloaded.LoadFromSnapshot(blob);
                Assert.True(reloaded.TryGetString("myplugin:kingdom", out string? kingdom));
                Assert.Equal("south", kingdom);
                Assert.True(reloaded.TryGetLong("myplugin:playtime_minutes", out long playtime));
                Assert.Equal(42, playtime);
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
    public void Delete_RemovesKeyAcrossReload()
    {
        using var provider = new InMemoryWorldProvider();
        using var persistence = new PlayerPersistence(provider);
        const string xuid = "delete-xuid";

        var store = new PlayerDataStore();
        store.SetString("myplugin:flag", "yes");
        persistence.ScheduleSave(xuid, store);
        persistence.Flush();

        Assert.True(store.Delete("myplugin:flag"));
        persistence.ScheduleSave(xuid, store);
        persistence.Flush();

        var reloaded = new PlayerDataStore();
        Assert.True(provider.TryLoadPlayerBlob(xuid, out byte[]? blob));
        Assert.NotNull(blob);
        reloaded.LoadFromSnapshot(blob);
        Assert.False(reloaded.TryGetString("myplugin:flag", out _));
    }

    [Fact]
    public void PlayerManager_LoadsAndSavesOnRemove()
    {
        using var provider = new InMemoryWorldProvider();
        using var persistence = new PlayerPersistence(provider);
        var managers = new PlayerManager();
        managers.BindPersistence(provider, persistence);

        var config = new Orion.Config.OrionConfig();
        config.Server.WorldDefaultSettings.Dimensions.Add(new Orion.Config.DimensionConfig
        {
            Identifier = "overworld",
            SpawnPosition = [0, 64, 0],
        });
        var regionizer = new Orion.Region.Regionizer(Orion.Region.RegionizerOptions.FromGridExponent(0));
        using var world = World.CreateFromConfig(config.Server.WorldDefaultSettings, regionizer, provider);

        var sessions = new Orion.Network.SessionManager();
        var connection = new StubNet();
        var session = sessions.Create(connection);
        session.Username = "Steve";
        session.Xuid = Guid.NewGuid().ToString("N");

        Player.Player player = managers.Create(
            session,
            world,
            regionizer,
            config.Server.WorldDefaultSettings.Dimensions[0]);
        player.Data.SetString("myplugin:kingdom", "east");
        managers.Remove(connection);
        persistence.Flush();

        var session2 = sessions.Create(new StubNet());
        session2.Username = "Steve";
        session2.Xuid = session.Xuid;
        Player.Player rejoined = managers.Create(
            session2,
            world,
            regionizer,
            config.Server.WorldDefaultSettings.Dimensions[0]);
        Assert.True(rejoined.Data.TryGetString("myplugin:kingdom", out string? kingdom));
        Assert.Equal("east", kingdom);
    }

    private sealed class StubNet : RakNet.NetworkConnection
    {
        protected override void SendMessage(ReadOnlySpan<byte> payload)
        {
        }
    }
}
