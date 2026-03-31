using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubContentDatabase : IContentDatabase
{
    public StubContentDatabase()
    {
        ItemTemplates = new Dictionary<string, ItemTemplate>
        {
            ["potion_health"] = new(
                "potion_health",
                "Health Potion",
                "Restores health.",
                ItemCategory.Consumable,
                EquipSlot.None,
                new Dictionary<string, int>(),
                "heal",
                -1,
                5),
            ["potion_haste"] = new(
                "potion_haste",
                "Haste Potion",
                "Boosts speed for a few turns.",
                ItemCategory.Consumable,
                EquipSlot.None,
                new Dictionary<string, int>
                {
                    ["duration"] = 5,
                    ["magnitude"] = 1,
                },
                "status:hasted",
                -1,
                3),
            ["sword_iron"] = new(
                "sword_iron",
                "Iron Sword",
                "Reliable melee weapon.",
                ItemCategory.Weapon,
                EquipSlot.MainHand,
                new Dictionary<string, int>
                {
                    ["attack"] = 2,
                },
                null,
                0,
                1),
            ["shield_wooden"] = new(
                "shield_wooden",
                "Wooden Shield",
                "Basic off-hand protection.",
                ItemCategory.Armor,
                EquipSlot.OffHand,
                new Dictionary<string, int>
                {
                    ["defense"] = 2,
                },
                null,
                0,
                1),
            ["dagger_venom"] = new(
                "dagger_venom",
                "Viper Fang",
                "Fast, light, and nasty.",
                ItemCategory.Weapon,
                EquipSlot.MainHand,
                new Dictionary<string, int>
                {
                    ["attack"] = 1,
                    ["speed"] = 10,
                },
                null,
                0,
                1),
            ["scroll_fireball"] = new(
                "scroll_fireball",
                "Scroll of Fireball",
                "Single-use offensive scroll.",
                ItemCategory.Scroll,
                EquipSlot.None,
                new Dictionary<string, int>(),
                "heal",
                -1,
                1),
            ["scroll_blink"] = new(
                "scroll_blink",
                "Scroll of Blink",
                "Single-use escape scroll.",
                ItemCategory.Scroll,
                EquipSlot.None,
                new Dictionary<string, int>(),
                "heal",
                -1,
                1),
        };

        EnemyTemplates = new Dictionary<string, EnemyTemplate>
        {
            ["goblin"] = new(
                "goblin",
                "Goblin",
                "A small hostile creature.",
                new Stats { HP = 8, MaxHP = 8, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 },
                "melee_rusher",
                Faction.Enemy,
                0,
                -1,
                100,
                null),
        };
    }

    public IReadOnlyDictionary<string, ItemTemplate> ItemTemplates { get; }

    public IReadOnlyDictionary<string, EnemyTemplate> EnemyTemplates { get; }

    public bool TryGetItemTemplate(string templateId, out ItemTemplate template)
    {
        var found = ItemTemplates.TryGetValue(templateId, out var item);
        template = item!;
        return found;
    }

    public bool TryGetEnemyTemplate(string templateId, out EnemyTemplate template)
    {
        var found = EnemyTemplates.TryGetValue(templateId, out var enemy);
        template = enemy!;
        return found;
    }

    public IReadOnlyList<ItemTemplate> GetAvailableItems(int depth) =>
        ItemTemplates.Values.ToArray();

    public IReadOnlyList<EnemyTemplate> GetAvailableEnemies(int depth) =>
        EnemyTemplates.Values.Where(enemy => depth >= enemy.MinDepth && (enemy.MaxDepth < 0 || depth <= enemy.MaxDepth)).ToArray();
}
