using Orion.Gameplay;
using Orion.PluginContracts;
using Orion.PluginContracts.Services;
using Orion.Plugins;

namespace Orion.Game.Tests;

[Collection("PluginHost")]
public sealed class GameplayApiTests
{
    [Fact]
    public void GameplayContracts_LiveInGameplayApiAssembly()
    {
        Assert.Equal("Orion.Gameplay.Api", typeof(IPlayerInventoryService).Assembly.GetName().Name);
        Assert.Equal("Orion.Gameplay.Api", typeof(IBuildingApi).Assembly.GetName().Name);
        Assert.Equal("Orion.Gameplay.Api", typeof(IMiningApi).Assembly.GetName().Name);
        Assert.Equal("Orion.Gameplay.Api", typeof(IAttributesApi).Assembly.GetName().Name);
        Assert.Same(typeof(GameplayApi).Assembly, typeof(IPlayerInventoryService).Assembly);
    }

    [Fact]
    public void TryGet_InventoryService_False_WhenNotRegistered()
    {
        PluginHost.ResetForTests();
        try
        {
            Assert.False(PluginHost.Services.TryGet(out IPlayerInventoryService? inventory));
            Assert.Null(inventory);
        }
        finally
        {
            PluginHost.ResetForTests();
        }
    }

    [Fact]
    public void TryGet_InventoryService_True_WhenRegistered()
    {
        PluginHost.ResetForTests();
        try
        {
            StubOwner owner = new();
            StubInventoryService stub = new();
            PluginHost.Services.Register<IPlayerInventoryService>(stub, owner, ServicePriority.Normal);

            Assert.True(PluginHost.Services.TryGet(out IPlayerInventoryService? inventory));
            Assert.Same(stub, inventory);
        }
        finally
        {
            PluginHost.ResetForTests();
        }
    }

    sealed class StubOwner : IOrionPlugin
    {
        public string Id => "test.gameplay";
        public Version Version => new(1, 0, 0);
        public void Load(IPluginLoadContext context)
        {
        }

        public void OnEnable(IPluginContext context)
        {
        }

        public void OnWorldInitialize(IWorldInitContext context)
        {
        }

        public void OnDisable(IPluginContext context)
        {
        }
    }

    sealed class StubInventoryService : IPlayerInventoryService
    {
        public bool TryOpenInventory(Orion.Api.IPlayer player) => false;
        public bool TryCloseInventory(Orion.Api.IPlayer player, int windowId) => false;
        public bool TryGetAccess(Orion.Api.IPlayer player, out IPlayerInventoryAccess? access)
        {
            access = null;
            return false;
        }

        public Orion.Api.Items.IItemStack? GetHeldItem(Orion.Api.IPlayer player) => null;
        public bool TrySetHeldSlot(Orion.Api.IPlayer player, int slot) => false;
        public bool TryGive(Orion.Api.IPlayer player, Orion.Api.Items.IItemStack stack, out int leftover)
        {
            leftover = stack.Count;
            return false;
        }

        public bool TryClear(Orion.Api.IPlayer player) => false;
        public bool TryCollect(Orion.Api.IPlayer player, Orion.Api.Items.IItemStack stack, out ushort moved)
        {
            moved = 0;
            return false;
        }

        public bool TrySyncToClient(Orion.Api.IPlayer player) => false;
        public void EnableHud(Orion.Api.IPlayer player)
        {
        }

        public Orion.Api.Containers.IContainer? ResolveContainer(Orion.Api.IPlayer player, ContainerNameWire name) =>
            null;

        public bool TryProcessItemStackRequest(
            Orion.Api.IPlayer player,
            ItemStackRequestWire request,
            out ItemStackResponseWire response)
        {
            response = new ItemStackResponseWire(new object());
            return false;
        }
    }
}
