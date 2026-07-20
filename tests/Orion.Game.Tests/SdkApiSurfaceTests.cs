using Orion.Api;
using Orion.Gameplay;
using Xunit;
using PlayerEntity = Orion.Player.Player;

namespace Orion.Game.Tests;

public sealed class SdkApiSurfaceTests
{
    [Fact]
    public void IPlayer_lives_in_Orion_Api_assembly()
    {
        Assert.Equal("Orion.Api", typeof(IPlayer).Assembly.GetName().Name);
        Assert.Equal("Orion.Api", typeof(IServer).Assembly.GetName().Name);
        Assert.Equal("Orion.Api", typeof(IEntity).Assembly.GetName().Name);
    }

    [Fact]
    public void Player_implements_IPlayer()
    {
        PlayerEntity player = new("sdk", "0", Guid.NewGuid());
        Assert.IsAssignableFrom<IPlayer>(player);
        Assert.IsAssignableFrom<IEntity>(player);
        Assert.Equal("sdk", ((IPlayer)player).Username);
    }

    [Fact]
    public void GameplayApi_marker_lives_in_Gameplay_Api_assembly()
    {
        Assert.Equal("Orion.Gameplay.Api", typeof(GameplayApi).Assembly.GetName().Name);
        Assert.Equal("Orion.Gameplay.Api", GameplayApi.PackageId);
    }

    [Fact]
    public void Server_implements_IServer()
    {
        Server server = new();
        Assert.IsAssignableFrom<IServer>(server);
        Assert.Empty(server.OnlinePlayers);
        Assert.Null(server.DefaultWorld);
    }
}
