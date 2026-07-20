namespace Orion.Network.Handlers;

using Orion;
using Orion.Gameplay;
using Orion.Plugins;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.RakNet;

/// <summary>
/// Fallback ItemStackRequest path when VanillaInventory is not owning the packet.
/// Prefer the plugin owner handler when Plugins.Enabled and VanillaInventory is loaded.
/// </summary>
public static class ItemStackRequest
{
    public static void Handle(Server server, NetworkConnection connection, ReadOnlySpan<byte> packetBuffer)
    {
        int offset = 0;
        Basalt.Binary.BinaryReader reader = new(packetBuffer, ref offset);
        ItemStackRequestPacket packet;
        try
        {
            packet = (ItemStackRequestPacket)Protocol.Io.Packet.Deserialize(reader);
        }
        catch (Exception exception)
        {
            CreativeInventoryLog.LogItemStackAction("?", "deserialize-fail", exception.ToString());
            throw;
        }

        if (!SessionLookup.TryGetPlayer(server, connection, out global::Orion.Player.Player? player) ||
            packet.Requests.Count == 0)
        {
            return;
        }

        if (!PluginHost.Services.TryGet(out IPlayerInventoryService? inventory) || inventory is null)
        {
            return;
        }

        List<ItemStackResponse> responses = new(packet.Requests.Count);
        foreach (Protocol.Types.ItemStackRequest request in packet.Requests)
        {
            if (inventory.TryProcessItemStackRequest(
                    player,
                    new ItemStackRequestWire(request),
                    out ItemStackResponseWire responseWire)
                && responseWire.Value is ItemStackResponse response)
            {
                responses.Add(response);
            }
            else
            {
                responses.Add(new ItemStackResponse
                {
                    RequestId = request.RequestId,
                    Status = ItemStackResponseStatus.Error
                });
            }
        }

        ItemStackResponsePacket responsePacket = new() { Responses = responses };
        if (player.Session is not null)
        {
            player.Session.Send(responsePacket);
        }
        else
        {
            server.Network.SendPacket(connection, responsePacket);
        }
    }

    public static ItemStackResponse Process(
        global::Orion.Player.Player player,
        Protocol.Types.ItemStackRequest request)
    {
        if (PluginHost.Services.TryGet(out IPlayerInventoryService? inventory)
            && inventory is not null
            && inventory.TryProcessItemStackRequest(
                player,
                new ItemStackRequestWire(request),
                out ItemStackResponseWire responseWire)
            && responseWire.Value is ItemStackResponse response)
        {
            return response;
        }

        return new ItemStackResponse
        {
            RequestId = request.RequestId,
            Status = ItemStackResponseStatus.Error
        };
    }
}
