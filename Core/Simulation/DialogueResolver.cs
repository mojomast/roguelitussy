using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class DialogueResolver
{
    public static bool IsAvailable(DialogueOption option, IEntity? player)
    {
        if (option.Condition is not { } condition)
        {
            return true;
        }

        if (player is null || player.Stats.HP <= 0)
        {
            return false;
        }

        return condition.Type switch
        {
            "injured" => player.Stats.HP < player.Stats.MaxHP,
            "missing_item" => condition.ItemId is not null
                && player.GetComponent<InventoryComponent>() is { } inventory
                && !inventory.Items.Any(item => item.TemplateId == condition.ItemId && item.StackCount > 0),
            "reputation_at_least" => condition.FactionId is not null && condition.Value is { } value
                && (player.GetComponent<FactionComponent>()?.Get(condition.FactionId) ?? 0) >= value,
            _ => false,
        };
    }

    public static IReadOnlyList<DialogueOption> GetOptions(DialogueNode node, IEntity? player)
    {
        var options = node.Options.Where(option => IsAvailable(option, player)).ToArray();
        return options.Length > 0 ? options : new[] { new DialogueOption("Leave.", null, "close") };
    }

    public static string BuildExpeditionReport(IWorldState world)
    {
        var player = world.Player;
        var progression = player.GetComponent<ProgressionComponent>();
        var recovery = player.GetComponent<InventoryComponent>()?.Items
            .Where(item => item.TemplateId == "potion_health")
            .Sum(item => (long)Math.Max(0, item.StackCount)) ?? 0;
        return $"Your record: depth {world.Depth}, level {progression?.Level ?? 1}, {progression?.Kills ?? 0} kills. "
            + $"You have {player.Stats.HP}/{player.Stats.MaxHP} HP and {recovery} health potions. "
            + (player.Stats.HP < player.Stats.MaxHP ? "Recover before taking another risk. " : "You are uninjured. ")
            + (recovery == 0 ? "Carry an emergency heal; a retreat is not a failure." : "Keep one potion in reserve for a failed retreat.");
    }
}
