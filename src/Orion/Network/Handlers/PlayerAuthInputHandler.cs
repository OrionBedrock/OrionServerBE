using Orion.Permissions;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;

namespace Orion.Network.Handlers;

public enum PermissionGateResult
{
    Allowed,
    Discarded,
}

public static class PlayerAuthInputHandler
{
    public static PermissionGateResult Handle(
        ServerContext context,
        ConnectionSession session,
        PlayerAuthInputPacket packet)
    {
        _ = context;
        if (session.Player is not { } player)
        {
            return PermissionGateResult.Discarded;
        }

        if (!ContainsDestroyAction(packet.BlockActions))
        {
            return PermissionGateResult.Allowed;
        }

        if (!player.HasPermission(PermissionNodes.BlockBreak))
        {
            return PermissionGateResult.Discarded;
        }

        // World mutation arrives later; gate pass is enough for Phase 10.
        return PermissionGateResult.Allowed;
    }

    private static bool ContainsDestroyAction(List<PlayerBlockAction> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            PlayerActionType action = actions[i].Action;
            if (action is PlayerActionType.StartDestroyBlock
                or PlayerActionType.ContinueDestroyBlock
                or PlayerActionType.PredictDestroyBlock
                or PlayerActionType.CreativeDestroyBlock
                or PlayerActionType.CrackBlock
                or PlayerActionType.StopDestroyBlock)
            {
                return true;
            }
        }

        return false;
    }
}

public static class InventoryTransactionHandler
{
    public static PermissionGateResult Handle(
        ServerContext context,
        ConnectionSession session,
        InventoryTransactionPacket packet)
    {
        _ = context;
        if (session.Player is not { } player)
        {
            return PermissionGateResult.Discarded;
        }

        if (packet.TransactionData.Type is not InventoryTransactionType.UseItem)
        {
            return PermissionGateResult.Allowed;
        }

        if (!player.HasPermission(PermissionNodes.BlockPlace))
        {
            return PermissionGateResult.Discarded;
        }

        return PermissionGateResult.Allowed;
    }
}
