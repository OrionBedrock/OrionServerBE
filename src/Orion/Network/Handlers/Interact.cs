namespace Orion.Network.Handlers;

using Orion;
using Orion.Api.Events;
using Orion.Gameplay;
using Orion.Item.Traits.Types;
using Orion.Plugins;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.RakNet;

public static class Interact
{
    public static void Handle(Server server, NetworkConnection connection, ReadOnlySpan<byte> packetBuffer)
    {
        InteractPacket packet = new();
        int offset = 0;
        BinaryReader reader = new(packetBuffer, ref offset);
        packet = (InteractPacket)Protocol.Io.Packet.Deserialize(reader);

        if (!SessionLookup.TryGetPlayer(server, connection, out global::Orion.Player.Player? player))
        {
            return;
        }

        if (packet.ActionType == InteractActionType.OpenInventory)
        {
            PlayerOpenInventorySignal signal = new(player);
            server.Emit(signal);
            if (!signal.Emit())
            {
                return;
            }

            if (PluginHost.Services.TryGet(out IPlayerInventoryService? inventoryService) && inventoryService is not null)
            {
                _ = inventoryService.TryOpenInventory(player);
            }

            return;
        }

        if (packet.ActionType == InteractActionType.MouseOverEntity)
        {
            if (!PluginHost.Services.TryGet(out IPlayerInventoryService? inventoryService) || inventoryService is null)
            {
                return;
            }

            var heldItem = inventoryService.GetHeldItem(player);
            if (heldItem is null || player.Dimension is null)
            {
                return;
            }

            foreach (Orion.Entity.Entity entity in player.Dimension.GetEntities())
            {
                if (entity.RuntimeId != packet.TargetEntityRuntimeId)
                {
                    continue;
                }

                Vec3f clicked = packet.Position.HasValue && packet.Position.Value is Vec3f value ? value : new Vec3f();
                heldItem.OnUseOnEntity(new ItemUseOnEntityDetails(player, entity, 0, player.Position, clicked));
                break;
            }
        }
    }
}
