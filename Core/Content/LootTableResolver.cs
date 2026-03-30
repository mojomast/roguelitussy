using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed record LootRollResult(string ItemId, int Count);

public static class LootTableResolver
{
    public static IReadOnlyList<LootRollResult> RollTable(ContentLoader content, string tableId, Random rng, int? depth = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(rng);

        if (!content.LootTables.TryGetValue(tableId, out var table))
        {
            throw new KeyNotFoundException($"Loot table '{tableId}' was not found.");
        }

        return RollTable(table, rng, itemId => !depth.HasValue || content.IsItemAvailableAtDepth(itemId, depth.Value));
    }

    public static IReadOnlyList<LootEntryDefinition> GetEligibleEntries(ContentLoader content, string tableId, int? depth = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.LootTables.TryGetValue(tableId, out var table))
        {
            throw new KeyNotFoundException($"Loot table '{tableId}' was not found.");
        }

        return table.Entries
            .Where(entry => entry.ItemId is null || !depth.HasValue || content.IsItemAvailableAtDepth(entry.ItemId, depth.Value))
            .ToArray();
    }

    public static IReadOnlyList<LootRollResult> RollTable(LootTableDefinition table, Random rng, Func<string, bool>? itemFilter = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(rng);

        var results = new List<LootRollResult>();

        for (var rollIndex = 0; rollIndex < table.Rolls; rollIndex++)
        {
            var eligibleEntries = table.Entries
                .Where(entry => entry.ItemId is null || itemFilter?.Invoke(entry.ItemId) != false)
                .ToArray();

            if (eligibleEntries.Length == 0)
            {
                continue;
            }

            var selectedEntry = RollEntry(eligibleEntries, rng);
            if (selectedEntry.ItemId is null)
            {
                continue;
            }

            var count = selectedEntry.CountMin == selectedEntry.CountMax
                ? selectedEntry.CountMin
                : rng.Next(selectedEntry.CountMin, selectedEntry.CountMax + 1);

            if (count > 0)
            {
                results.Add(new LootRollResult(selectedEntry.ItemId, count));
            }
        }

        return results;
    }

    private static LootEntryDefinition RollEntry(IReadOnlyList<LootEntryDefinition> entries, Random rng)
    {
        var totalWeight = entries.Sum(entry => entry.Weight);
        var roll = rng.Next(totalWeight);
        var cumulative = 0;

        foreach (var entry in entries)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
            {
                return entry;
            }
        }

        return entries[^1];
    }
}