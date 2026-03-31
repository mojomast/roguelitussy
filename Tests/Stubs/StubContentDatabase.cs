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
                5,
                "common"),
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
                3,
                "uncommon"),
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
                1,
                "common",
                DamageMin: 3,
                DamageMax: 7,
                CritChance: 5,
                WeaponAccuracy: 85),
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
                1,
                "common"),
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
                1,
                "uncommon",
                DamageMin: 2,
                DamageMax: 5,
                CritChance: 15,
                WeaponAccuracy: 95,
                SpeedModifier: 20,
                OnHitEffects: new WeaponOnHitEffect[]
                {
                    new(StatusEffectType.Poisoned, 40, 5),
                }),
            ["sword_flame"] = new(
                "sword_flame",
                "Flamebrand",
                "Fire licks along the blade's edge.",
                ItemCategory.Weapon,
                EquipSlot.MainHand,
                new Dictionary<string, int>
                {
                    ["attack"] = 4,
                },
                null,
                0,
                1,
                "rare",
                DamageMin: 5,
                DamageMax: 12,
                CritChance: 8,
                WeaponAccuracy: 80,
                SpeedModifier: -10,
                OnHitEffects: new WeaponOnHitEffect[]
                {
                    new(StatusEffectType.Burning, 30, 3),
                }),
            ["scroll_fireball"] = new(
                "scroll_fireball",
                "Scroll of Fireball",
                "Single-use offensive scroll.",
                ItemCategory.Scroll,
                EquipSlot.None,
                new Dictionary<string, int>(),
                "heal",
                -1,
                1,
                "rare"),
            ["scroll_blink"] = new(
                "scroll_blink",
                "Scroll of Blink",
                "Single-use escape scroll.",
                ItemCategory.Scroll,
                EquipSlot.None,
                new Dictionary<string, int>(),
                "heal",
                -1,
                1,
                "uncommon"),
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
                null,
                10),
        };

        AbilityTemplates = new Dictionary<string, AbilityTemplate>
        {
            ["fireball"] = new(
                "fireball",
                "Fireball",
                "Hurl a ball of fire that explodes in a 3-tile radius.",
                new AbilityTargeting("aoe_circle", 8, 3, true, false, true, null),
                1200,
                8,
                new AbilityEffect[]
                {
                    new("damage", DamageType.Fire, 12, "attack", 0.5, null, 0, 0, null, null, 0.0, null),
                    new("apply_status", DamageType.Physical, 0, null, 0.0, "burning", 40, 3, null, null, 0.0, null),
                }),
            ["blink"] = new(
                "blink",
                "Blink",
                "Teleport to a visible tile within range.",
                new AbilityTargeting("tile", 8, 0, true, true, false, null),
                800,
                null,
                new AbilityEffect[]
                {
                    new("teleport", DamageType.Physical, 0, null, 0.0, null, 0, 0, null, null, 0.0, "target_tile"),
                }),
            ["arrow_shot"] = new(
                "arrow_shot",
                "Arrow Shot",
                "Fire an arrow at a target within range.",
                new AbilityTargeting("single", 8, 0, true, false, false, null),
                1000,
                null,
                new AbilityEffect[]
                {
                    new("damage", DamageType.Physical, 4, "attack", 0.8, null, 0, 0, null, null, 0.0, null),
                }),
            ["life_drain"] = new(
                "life_drain",
                "Life Drain",
                "Siphon life from a target, healing self.",
                new AbilityTargeting("single", 1, 0, true, false, false, null),
                1100,
                null,
                new AbilityEffect[]
                {
                    new("damage", DamageType.Dark, 6, "attack", 0.6, null, 0, 0, null, null, 0.0, null),
                    new("heal_self", DamageType.Physical, 0, null, 0.0, null, 0, 0, null, "damage_dealt", 0.5, null),
                }),
            ["war_cry"] = new(
                "war_cry",
                "War Cry",
                "Bolster nearby allies, weakening nearby enemies.",
                new AbilityTargeting("aoe_circle", 0, 4, false, false, false, "self"),
                800,
                null,
                new AbilityEffect[]
                {
                    new("apply_status", DamageType.Physical, 0, null, 0.0, "weakened", 100, 3, "enemies", null, 0.0, null),
                    new("apply_status", DamageType.Physical, 0, null, 0.0, "empowered", 100, 3, "allies", null, 0.0, null),
                }),
            ["phase_shift"] = new(
                "phase_shift",
                "Phase Shift",
                "Become incorporeal for 2 turns.",
                new AbilityTargeting("self", 0, 0, false, false, false, null),
                600,
                null,
                new AbilityEffect[]
                {
                    new("apply_status", DamageType.Physical, 0, null, 0.0, "phased", 100, 2, null, null, 0.0, null),
                }),
        };
    }

    public IReadOnlyDictionary<string, ItemTemplate> ItemTemplates { get; }

    public IReadOnlyDictionary<string, EnemyTemplate> EnemyTemplates { get; }

    public IReadOnlyDictionary<string, AbilityTemplate> AbilityTemplates { get; }

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

    public bool TryGetAbilityTemplate(string abilityId, out AbilityTemplate template)
    {
        var found = AbilityTemplates.TryGetValue(abilityId, out var ability);
        template = ability!;
        return found;
    }

    public IReadOnlyList<ItemTemplate> GetAvailableItems(int depth) =>
        ItemTemplates.Values.ToArray();

    public IReadOnlyList<EnemyTemplate> GetAvailableEnemies(int depth) =>
        EnemyTemplates.Values.Where(enemy => depth >= enemy.MinDepth && (enemy.MaxDepth < 0 || depth <= enemy.MaxDepth)).ToArray();
}
