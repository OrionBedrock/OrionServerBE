using Orion.Api.Events;

namespace Orion.Events;

/// <summary>Maps signal CLR types to <see cref="ServerEvent"/> for typed <see cref="Orion.PluginContracts.Events.IEventBus"/> subscribe.</summary>
public static class SignalEventMap
{
    static readonly Dictionary<Type, ServerEvent> Map = new()
    {
        [typeof(ServerStartSignal)] = ServerEvent.ServerStart,
        [typeof(EntityHurtSignal)] = ServerEvent.EntityHurt,
        [typeof(EntitySpawnSignal)] = ServerEvent.EntitySpawn,
        [typeof(EntityDieSignal)] = ServerEvent.EntityDie,
        [typeof(PlayerChatSignal)] = ServerEvent.PlayerChat,
        [typeof(PlayerJoinSignal)] = ServerEvent.PlayerJoin,
        [typeof(PlayerSpawnSignal)] = ServerEvent.PlayerSpawn,
        [typeof(PlayerLeaveSignal)] = ServerEvent.PlayerLeave,
        [typeof(PlayerPlaceBlockSignal)] = ServerEvent.PlayerPlaceBlock,
        [typeof(PlayerBreakBlockSignal)] = ServerEvent.PlayerBreakBlock,
        [typeof(PlayerOpenInventorySignal)] = ServerEvent.PlayerOpenInventory,
        [typeof(PlayerOpenContainerSignal)] = ServerEvent.PlayerOpenContainer,
        [typeof(PlayerInteractEntitySignal)] = ServerEvent.PlayerInteractEntity,
        [typeof(PlayerItemUseSignal)] = ServerEvent.PlayerItemUse,
        [typeof(PlayerItemUseCompleteSignal)] = ServerEvent.PlayerItemUseComplete,
        [typeof(PlayerDropItemSignal)] = ServerEvent.PlayerDropItem,
        [typeof(PlayerPickupItemSignal)] = ServerEvent.PlayerPickupItem,
        [typeof(PlayerContainerCloseSignal)] = ServerEvent.PlayerContainerClose,
        [typeof(PlayerInventorySlotChangeSignal)] = ServerEvent.PlayerInventorySlotChange,
        [typeof(PlayerFoodEatSignal)] = ServerEvent.PlayerFoodEat,
        [typeof(PlayerHungerChangeSignal)] = ServerEvent.PlayerHungerChange,
        [typeof(PlayerGamemodeChangeSignal)] = ServerEvent.PlayerGamemodeChange,
        [typeof(BlockExplodeSignal)] = ServerEvent.BlockExplode,
        [typeof(ChunkLoadSignal)] = ServerEvent.ChunkLoad,
        [typeof(ChunkUnloadSignal)] = ServerEvent.ChunkUnload,
    };

    public static ServerEvent For<TSignal>() where TSignal : ISignal => For(typeof(TSignal));

    public static ServerEvent For(Type signalType)
    {
        ArgumentNullException.ThrowIfNull(signalType);
        if (Map.TryGetValue(signalType, out ServerEvent @event))
        {
            return @event;
        }

        throw new InvalidOperationException(
            $"No ServerEvent mapping for signal type '{signalType.FullName}'. Register it in {nameof(SignalEventMap)}.");
    }
}
