using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Roguelike.Core.Content;

public sealed class LootTableResolver
{
    private readonly Dictionary<string, List<LootEntry>> _tables = new();
    private readonly IContentDatabase _content;

    public LootTableResolver(IContentDatabase content)
    {
        _content = content;
    }

    public void Load(string basePath)
    {
        var path = Path.Combine(basePath, "loot_tables.json");
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        foreach (var table in doc.RootElement.EnumerateObject())
        {
            var entries = new List<LootEntry>();
            foreach (var entry in table.Value.EnumerateArray())
            {
                var itemId = entry.GetProperty("item_id").GetString()!;
                var weight = entry.GetProperty("weight").GetInt32();
                entries.Add(new LootEntry(itemId, weight));
            }
            _tables[table.Name] = entries;
        }
    }

    public ItemTemplate? Resolve(string tableId, Random rng)
    {
        if (!_tables.TryGetValue(tableId, out var entries) || entries.Count == 0)
            return null;

        var totalWeight = 0;
        foreach (var e in entries)
            totalWeight += e.Weight;

        var roll = rng.Next(totalWeight);
        var cumulative = 0;
        foreach (var e in entries)
        {
            cumulative += e.Weight;
            if (roll < cumulative)
                return _content.GetItem(e.ItemId);
        }

        return null;
    }

    private readonly record struct LootEntry(string ItemId, int Weight);
}
