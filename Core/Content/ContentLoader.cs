using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Roguelike.Core.Content;

public sealed class ContentLoader : IContentDatabase
{
    private readonly string _basePath;
    private readonly Dictionary<string, ItemTemplate> _items = new();
    private readonly Dictionary<string, EnemyTemplate> _enemies = new();

    public ContentLoader(string basePath)
    {
        _basePath = basePath;
    }

    public IReadOnlyList<ItemTemplate> AllItems => new List<ItemTemplate>(_items.Values);
    public IReadOnlyList<EnemyTemplate> AllEnemies => new List<EnemyTemplate>(_enemies.Values);

    public ItemTemplate? GetItem(string templateId)
    {
        _items.TryGetValue(templateId, out var item);
        return item;
    }

    public EnemyTemplate? GetEnemy(string templateId)
    {
        _enemies.TryGetValue(templateId, out var enemy);
        return enemy;
    }

    public IReadOnlyList<EnemyTemplate> GetEnemiesForDepth(int depth)
    {
        var result = new List<EnemyTemplate>();
        foreach (var enemy in _enemies.Values)
        {
            if (enemy.MinDepth <= depth && (enemy.MaxDepth == -1 || depth <= enemy.MaxDepth))
                result.Add(enemy);
        }
        return result;
    }

    public IReadOnlyList<ItemTemplate> GetItemsForDepth(int depth)
    {
        return AllItems;
    }

    public void LoadAll()
    {
        LoadItems();
        LoadEnemies();
    }

    private void LoadItems()
    {
        var path = Path.Combine(_basePath, "items.json");
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var templateId = elem.GetProperty("templateId").GetString()!;
            var displayName = elem.GetProperty("displayName").GetString()!;
            var description = elem.GetProperty("description").GetString()!;
            var category = Enum.Parse<ItemCategory>(elem.GetProperty("category").GetString()!);
            var slot = Enum.Parse<EquipSlot>(elem.GetProperty("slot").GetString()!);

            var modifiers = new Dictionary<string, int>();
            foreach (var mod in elem.GetProperty("statModifiers").EnumerateObject())
            {
                modifiers[mod.Name] = mod.Value.GetInt32();
            }

            string? useEffect = null;
            if (elem.TryGetProperty("useEffect", out var ue) && ue.ValueKind != JsonValueKind.Null)
                useEffect = ue.GetString();

            var maxCharges = elem.GetProperty("maxCharges").GetInt32();
            var maxStack = elem.GetProperty("maxStack").GetInt32();

            var item = new ItemTemplate(
                templateId, displayName, description,
                category, slot, modifiers,
                useEffect, maxCharges, maxStack
            );

            _items[templateId] = item;
        }
    }

    private void LoadEnemies()
    {
        var path = Path.Combine(_basePath, "enemies.json");
        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        foreach (var elem in doc.RootElement.EnumerateArray())
        {
            var templateId = elem.GetProperty("templateId").GetString()!;
            var displayName = elem.GetProperty("displayName").GetString()!;
            var description = elem.GetProperty("description").GetString()!;

            var bs = elem.GetProperty("baseStats");
            var stats = new Stats
            {
                HP = bs.GetProperty("HP").GetInt32(),
                MaxHP = bs.GetProperty("MaxHP").GetInt32(),
                Attack = bs.GetProperty("Attack").GetInt32(),
                Defense = bs.GetProperty("Defense").GetInt32(),
                Accuracy = bs.GetProperty("Accuracy").GetInt32(),
                Evasion = bs.GetProperty("Evasion").GetInt32(),
                Speed = bs.GetProperty("Speed").GetInt32(),
                ViewRadius = bs.GetProperty("ViewRadius").GetInt32(),
            };

            var brainType = elem.GetProperty("brainType").GetString()!;
            var faction = Enum.Parse<Faction>(elem.GetProperty("faction").GetString()!);
            var minDepth = elem.GetProperty("minDepth").GetInt32();
            var maxDepth = elem.GetProperty("maxDepth").GetInt32();
            var spawnWeight = elem.GetProperty("spawnWeight").GetInt32();

            string? lootTableId = null;
            if (elem.TryGetProperty("lootTableId", out var lt) && lt.ValueKind != JsonValueKind.Null)
                lootTableId = lt.GetString();

            var enemy = new EnemyTemplate(
                templateId, displayName, description,
                stats, brainType, faction,
                minDepth, maxDepth, spawnWeight, lootTableId
            );

            _enemies[templateId] = enemy;
        }
    }
}
