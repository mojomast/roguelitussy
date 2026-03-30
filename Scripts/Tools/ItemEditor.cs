using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class ItemEditor : Control
{
    private static readonly Regex StableIdPattern = new("^[a-z0-9_]+$", RegexOptions.Compiled);
    private ItemsDocument _itemsDocument = new() { Schema = "roguelike-items-v1", Version = 1 };
    private EnemiesDocument _enemiesDocument = new() { Schema = "roguelike-enemies-v1", Version = 1 };
    private string? _loadedContentDirectory;

    public ItemEditor()
    {
        Name = "ItemEditor";
        CustomMinimumSize = new Vector2(900f, 620f);
        StatusText = "Ready.";
    }

    public string StatusText { get; private set; }

    public ItemDefinition? SelectedItem { get; private set; }

    public EnemyDefinition? SelectedEnemy { get; private set; }

    public IReadOnlyList<string> ItemIds => _itemsDocument.Items.Select(item => item.Id).ToArray();

    public IReadOnlyList<string> EnemyIds => _enemiesDocument.Enemies.Select(enemy => enemy.Id).ToArray();

    public override void _Ready()
    {
        try
        {
            Load();
        }
        catch
        {
        }
    }

    public void Load(string? contentDirectory = null)
    {
        _loadedContentDirectory = ToolPaths.ResolveContentDirectory(contentDirectory);
        _itemsDocument = ToolJson.Read<ItemsDocument>(ToolPaths.ResolveContentFile("items.json", _loadedContentDirectory));
        _enemiesDocument = ToolJson.Read<EnemiesDocument>(ToolPaths.ResolveContentFile("enemies.json", _loadedContentDirectory));
        _itemsDocument.Items = _itemsDocument.Items.OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
        _enemiesDocument.Enemies = _enemiesDocument.Enemies.OrderBy(enemy => enemy.Id, StringComparer.Ordinal).ToList();
        SelectedItem = _itemsDocument.Items.FirstOrDefault();
        SelectedEnemy = _enemiesDocument.Enemies.FirstOrDefault();
        StatusText = $"Loaded {_itemsDocument.Items.Count} items and {_enemiesDocument.Enemies.Count} enemies.";
    }

    public bool SelectItem(string id)
    {
        SelectedItem = _itemsDocument.Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        return SelectedItem is not null;
    }

    public bool SelectEnemy(string id)
    {
        SelectedEnemy = _enemiesDocument.Enemies.FirstOrDefault(enemy => string.Equals(enemy.Id, id, StringComparison.Ordinal));
        return SelectedEnemy is not null;
    }

    public ItemDefinition CreateItem(string id)
    {
        var item = new ItemDefinition
        {
            Id = id,
            Name = ToDisplayName(id),
            Description = "New item.",
            Type = "consumable",
            Slot = "none",
            Stats = new Dictionary<string, int>(),
            Effects = new List<ItemEffectDefinition>(),
            Rarity = "common",
            Value = 0,
            Weight = 0.1,
            Requirements = new Dictionary<string, int>(),
            Stackable = true,
            MaxStack = 1,
            SpritePath = "res://Assets/Sprites/items/placeholder.png",
            SpriteAtlasCoords = new List<int> { 0, 0 },
        };

        UpsertItem(item);
        return item;
    }

    public EnemyDefinition CreateEnemy(string id)
    {
        var enemy = new EnemyDefinition
        {
            Id = id,
            Name = ToDisplayName(id),
            Description = "New enemy.",
            Stats = new EnemyStatsDefinition
            {
                HP = 10,
                Attack = 3,
                Defense = 1,
                Accuracy = 70,
                Evasion = 5,
                Speed = 100,
                FovRange = 7,
                XpValue = 5,
            },
            AiType = "melee_rush",
            AiParams = new Dictionary<string, JsonElement>(),
            Faction = "Enemy",
            MinDepth = 1,
            MaxDepth = 1,
            SpawnWeight = 1,
            Abilities = new List<EnemyAbilityReference>(),
            LootTableId = null,
            Tags = new List<string>(),
            SpritePath = "res://Assets/Sprites/enemies/placeholder.png",
            SpriteAtlasCoords = new List<int> { 0, 0 },
        };

        UpsertEnemy(enemy);
        return enemy;
    }

    public void UpsertItem(ItemDefinition item)
    {
        ReplaceOrAppend(_itemsDocument.Items, item, entry => entry.Id);
        _itemsDocument.Items = _itemsDocument.Items.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToList();
        SelectedItem = _itemsDocument.Items.First(entry => string.Equals(entry.Id, item.Id, StringComparison.Ordinal));
        StatusText = $"Prepared item '{item.Id}'.";
    }

    public void UpsertEnemy(EnemyDefinition enemy)
    {
        ReplaceOrAppend(_enemiesDocument.Enemies, enemy, entry => entry.Id);
        _enemiesDocument.Enemies = _enemiesDocument.Enemies.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToList();
        SelectedEnemy = _enemiesDocument.Enemies.First(entry => string.Equals(entry.Id, enemy.Id, StringComparison.Ordinal));
        StatusText = $"Prepared enemy '{enemy.Id}'.";
    }

    public void SaveItems(string? contentDirectory = null)
    {
        var resolvedDirectory = ResolveDirectory(contentDirectory);
        ToolJson.Write(ToolPaths.ResolveContentFile("items.json", resolvedDirectory), _itemsDocument);
        StatusText = $"Saved {_itemsDocument.Items.Count} items.";
    }

    public void SaveEnemies(string? contentDirectory = null)
    {
        var resolvedDirectory = ResolveDirectory(contentDirectory);
        ToolJson.Write(ToolPaths.ResolveContentFile("enemies.json", resolvedDirectory), _enemiesDocument);
        StatusText = $"Saved {_enemiesDocument.Enemies.Count} enemies.";
    }

    public IReadOnlyList<string> ValidateAll()
    {
        var errors = new List<string>();
        ValidateItems(errors);
        ValidateEnemies(errors);

        if (errors.Count == 0)
        {
            StatusText = "Validation passed.";
        }
        else
        {
            StatusText = $"Validation failed with {errors.Count} issue(s).";
        }

        return errors;
    }

    private void ValidateItems(List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in _itemsDocument.Items)
        {
            if (!StableIdPattern.IsMatch(item.Id))
            {
                errors.Add($"Item '{item.Id}' has an invalid id.");
            }

            if (!ids.Add(item.Id))
            {
                errors.Add($"Duplicate item id '{item.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                errors.Add($"Item '{item.Id}' is missing a display name.");
            }

            if (string.IsNullOrWhiteSpace(item.Type))
            {
                errors.Add($"Item '{item.Id}' is missing a type.");
            }

            if (item.Value < 0)
            {
                errors.Add($"Item '{item.Id}' has a negative value.");
            }

            if (item.Weight < 0d)
            {
                errors.Add($"Item '{item.Id}' has a negative weight.");
            }

            if (item.Stackable && item.MaxStack.GetValueOrDefault(0) <= 0)
            {
                errors.Add($"Item '{item.Id}' is stackable but has no positive max_stack.");
            }
        }
    }

    private void ValidateEnemies(List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var enemy in _enemiesDocument.Enemies)
        {
            if (!StableIdPattern.IsMatch(enemy.Id))
            {
                errors.Add($"Enemy '{enemy.Id}' has an invalid id.");
            }

            if (!ids.Add(enemy.Id))
            {
                errors.Add($"Duplicate enemy id '{enemy.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(enemy.Name))
            {
                errors.Add($"Enemy '{enemy.Id}' is missing a display name.");
            }

            if (string.IsNullOrWhiteSpace(enemy.AiType))
            {
                errors.Add($"Enemy '{enemy.Id}' is missing an ai_type.");
            }

            if (enemy.MinDepth < 0 || enemy.MaxDepth < enemy.MinDepth)
            {
                errors.Add($"Enemy '{enemy.Id}' has an invalid depth range.");
            }

            if (enemy.SpawnWeight <= 0)
            {
                errors.Add($"Enemy '{enemy.Id}' must have a positive spawn_weight.");
            }

            if (enemy.Stats.HP <= 0 || enemy.Stats.Attack < 0 || enemy.Stats.Defense < 0 || enemy.Stats.Speed <= 0)
            {
                errors.Add($"Enemy '{enemy.Id}' has invalid core stats.");
            }
        }
    }

    private string ResolveDirectory(string? contentDirectory)
    {
        return ToolPaths.ResolveContentDirectory(contentDirectory ?? _loadedContentDirectory);
    }

    private static void ReplaceOrAppend<T>(List<T> entries, T updated, Func<T, string> idSelector)
    {
        var id = idSelector(updated);
        var index = entries.FindIndex(entry => string.Equals(idSelector(entry), id, StringComparison.Ordinal));
        if (index >= 0)
        {
            entries[index] = updated;
            return;
        }

        entries.Add(updated);
    }

    private static string ToDisplayName(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            return "Untitled";
        }

        var parts = stableId.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}