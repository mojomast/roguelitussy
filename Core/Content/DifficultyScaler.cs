using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class DifficultyScaler
{
    public static int GetRecommendedEnemyCount(int depth)
    {
        var floor = Math.Max(1, depth + 1);
        return Math.Min(20, 3 + (floor * 2));
    }

    public static int GetRecommendedItemCount(int depth)
    {
        var floor = Math.Max(1, depth + 1);
        return Math.Min(10, 2 + floor);
    }

    public static int EstimateDamagePerHit(int attack, int defense)
    {
        return Math.Max(1, attack - defense);
    }

    public static double EstimateTurnsToKill(int attack, int defenderHp, int defenderDefense)
    {
        return (double)defenderHp / EstimateDamagePerHit(attack, defenderDefense);
    }

    public static IReadOnlyList<string> ValidateBalance(
        IReadOnlyDictionary<string, ItemDefinition> items,
        IReadOnlyDictionary<string, EnemyDefinition> enemies,
        IReadOnlyDictionary<string, LootTableDefinition> lootTables)
    {
        var errors = new List<string>();

        if (!items.Values.Any(item => item.Type == "consumable" && GetRequiredLevel(item) <= 1))
        {
            errors.Add("Balance: at least one consumable must be available from depth 0.");
        }

        if (!items.Values.Any(item => item.Type == "weapon" && GetRequiredLevel(item) <= 1))
        {
            errors.Add("Balance: at least one weapon must be available from depth 0.");
        }

        if (!items.Values.Any(item => item.Type == "armor" && GetRequiredLevel(item) <= 1))
        {
            errors.Add("Balance: at least one armor item must be available from depth 0.");
        }

        var previousMaxHp = 0;
        var previousMaxAttack = 0;
        for (var depth = 0; depth <= 5; depth++)
        {
            var availableEnemies = enemies.Values
                .Where(enemy => depth >= enemy.MinDepth && depth <= enemy.MaxDepth)
                .ToArray();

            if (availableEnemies.Length == 0)
            {
                errors.Add($"Balance: no enemies available at depth {depth}.");
                continue;
            }

            var currentMaxHp = availableEnemies.Max(enemy => enemy.Stats.HP);
            var currentMaxAttack = availableEnemies.Max(enemy => enemy.Stats.Attack);
            if (depth > 0 && currentMaxHp < previousMaxHp)
            {
                errors.Add($"Balance: enemy HP budget regresses at depth {depth}.");
            }

            if (depth > 0 && currentMaxAttack < previousMaxAttack)
            {
                errors.Add($"Balance: enemy attack budget regresses at depth {depth}.");
            }

            previousMaxHp = currentMaxHp;
            previousMaxAttack = currentMaxAttack;
        }

        if (!lootTables.TryGetValue("floor_loot", out var floorLoot))
        {
            errors.Add("Balance: floor_loot table is required.");
            return errors;
        }

        for (var depth = 0; depth <= 5; depth++)
        {
            var eligibleCount = floorLoot.Entries.Count(entry => entry.ItemId is null || IsItemAvailableAtDepth(items, entry.ItemId, depth));
            if (eligibleCount == 0)
            {
                errors.Add($"Balance: floor_loot has no eligible entries at depth {depth}.");
            }
        }

        return errors;
    }

    private static bool IsItemAvailableAtDepth(IReadOnlyDictionary<string, ItemDefinition> items, string itemId, int depth)
    {
        return items.TryGetValue(itemId, out var item) && GetRequiredLevel(item) <= Math.Max(1, depth + 1);
    }

    private static int GetRequiredLevel(ItemDefinition item)
    {
        return item.Requirements.TryGetValue("level", out var level) ? level : 1;
    }
}