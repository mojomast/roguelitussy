using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubContentDatabase : IContentDatabase
{
    private readonly List<ItemTemplate> _items = new()
    {
        new ItemTemplate(
            TemplateId: "health_potion",
            DisplayName: "Health Potion",
            Description: "Restores 25 HP",
            Category: ItemCategory.Consumable,
            Slot: EquipSlot.None,
            StatModifiers: new Dictionary<string, int>(),
            UseEffect: "heal:25",
            MaxCharges: 1,
            MaxStack: 5
        ),
        new ItemTemplate(
            TemplateId: "sword_iron",
            DisplayName: "Iron Sword",
            Description: "A sturdy blade",
            Category: ItemCategory.Weapon,
            Slot: EquipSlot.MainHand,
            StatModifiers: new Dictionary<string, int> { ["Attack"] = 5 },
            UseEffect: null,
            MaxCharges: -1,
            MaxStack: 1
        ),
        new ItemTemplate(
            TemplateId: "shield_wood",
            DisplayName: "Wooden Shield",
            Description: "Basic protection",
            Category: ItemCategory.Armor,
            Slot: EquipSlot.OffHand,
            StatModifiers: new Dictionary<string, int> { ["Defense"] = 3 },
            UseEffect: null,
            MaxCharges: -1,
            MaxStack: 1
        ),
    };

    private readonly List<EnemyTemplate> _enemies = new()
    {
        new EnemyTemplate(
            TemplateId: "goblin",
            DisplayName: "Goblin",
            Description: "A sneaky green creature",
            BaseStats: new Stats
            {
                HP = 20, MaxHP = 20,
                Attack = 4, Defense = 1,
                Accuracy = 75, Evasion = 10,
                Speed = 90, ViewRadius = 6
            },
            BrainType: "melee_rusher",
            Faction: Faction.Enemy,
            MinDepth: 0, MaxDepth: 5,
            SpawnWeight: 10,
            LootTableId: null
        ),
        new EnemyTemplate(
            TemplateId: "skeleton",
            DisplayName: "Skeleton",
            Description: "Rattling bones",
            BaseStats: new Stats
            {
                HP = 35, MaxHP = 35,
                Attack = 6, Defense = 3,
                Accuracy = 70, Evasion = 5,
                Speed = 70, ViewRadius = 8
            },
            BrainType: "patrol_guard",
            Faction: Faction.Enemy,
            MinDepth: 1, MaxDepth: 8,
            SpawnWeight: 8,
            LootTableId: null
        ),
    };

    public ItemTemplate? GetItem(string templateId) =>
        _items.FirstOrDefault(i => i.TemplateId == templateId);

    public IReadOnlyList<ItemTemplate> AllItems => _items;

    public EnemyTemplate? GetEnemy(string templateId) =>
        _enemies.FirstOrDefault(e => e.TemplateId == templateId);

    public IReadOnlyList<EnemyTemplate> AllEnemies => _enemies;

    public IReadOnlyList<EnemyTemplate> GetEnemiesForDepth(int depth) =>
        _enemies.Where(e => depth >= e.MinDepth && (e.MaxDepth == -1 || depth <= e.MaxDepth)).ToList();

    public IReadOnlyList<ItemTemplate> GetItemsForDepth(int depth) => _items;
}
