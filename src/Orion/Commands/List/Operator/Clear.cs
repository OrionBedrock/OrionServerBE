namespace Orion.Commands.List.Operator;

using Orion.Commands;
using Orion;
using Orion.Api.Containers;
using Orion.Entity;
using Orion.Gameplay;
using Orion.Plugins;
using Player = global::Orion.Player.Player;

public class ClearCommand : Command
{
    public ClearCommand() : base("clear", "Clear an inventory")
    {
        Permissions.Add("basalt.op");

        CreateOverload()
            .Set<TargetEnum>("target", false);
    }

    public override CommandResult Execute(CommandExecutionState state)
    {
        var target = state.Get<TargetEnum>("target");

        if (target != null)
            return ClearTargetInventory(target);

        if (state.Executor is PlayerExecutor executor)
            return ClearEntityInventory(executor.Player);

        return CommandResult.Empty(true);
    }

    private static CommandResult ClearTargetInventory(TargetEnum target)
    {
        var entity = target.Entities.FirstOrDefault();

        if (entity == null)
            return CommandResult.Message("No entities matched the target selector", false);

        return ClearEntityInventory(entity);
    }

    private static CommandResult ClearEntityInventory(Entity entity)
    {
        if (entity is not Player player)
        {
            return CommandResult.Message("§cOnly players have clearable inventories.", false);
        }

        if (!PluginHost.Services.TryGet(out IPlayerInventoryService? inventory) || inventory is null)
        {
            return CommandResult.Message("§cInventory plugin is not loaded.", false);
        }

        if (!inventory.TryGetAccess(player, out IPlayerInventoryAccess? access) || access is null)
        {
            return CommandResult.Empty(true);
        }

        int size = 0;
        IContainer container = access.Container;
        for (int i = 0; i < container.GetSize(); i++)
        {
            size += container.GetItem(i)?.Count ?? 0;
        }

        _ = inventory.TryClear(player);

        player.SendMessage("§7Your inventory has been cleared.");
        return CommandResult.Message($"§7Cleared §a{size} §7items from §a{player.Username}'s inventory", true);
    }
}






