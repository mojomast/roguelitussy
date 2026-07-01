using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubContentDatabase : IContentDatabase
{
    public const int DefaultContentVersion = 1;
    public const string DefaultContentHash = "stub-content-hash";

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
                "cast_ability:fireball",
                -1,
                1,
                "rare",
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                0,
                0.0,
                true),
            ["scroll_blink"] = new(
                "scroll_blink",
                "Scroll of Blink",
                "Single-use escape scroll.",
                ItemCategory.Scroll,
                EquipSlot.None,
                new Dictionary<string, int>(),
                "cast_ability:blink",
                -1,
                1,
                "uncommon",
                0,
                0,
                0,
                0,
                0,
                null,
                null,
                0,
                0.0,
                true),
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
                0,
                0,
                10,
                AIParameters.Empty),
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
            ["spike_trap"] = new(
                "spike_trap",
                "Spike Trap",
                "Sharp iron spikes spring from the floor.",
                new AbilityTargeting("single", 0, 0, false, false, false, null),
                100,
                null,
                new AbilityEffect[]
                {
                    new("damage", DamageType.Physical, 8, null, 0.0, null, 0, 0, null, null, 0.0, null),
                }),
        };

        PerkTemplates = new Dictionary<string, PerkTemplate>
        {
            ["battle_instinct"] = new(
                "battle_instinct",
                "Battle Instinct",
                "Sharpen the aggressive habits that keep the fight tilted in your favor.",
                2,
                new PerkEffect[]
                {
                    new("stat_bonus", "Attack", 1),
                    new("stat_bonus", "Accuracy", 4),
                }),
            ["quartermasters_eye"] = new(
                "quartermasters_eye",
                "Quartermaster's Eye",
                "You know when a merchant is padding the margin and when to push back.",
                2,
                new PerkEffect[]
                {
                    new("shop_discount_percent", null, 20),
                }),
            ["iron_will"] = new(
                "iron_will",
                "Iron Will",
                "Steady breath and harder resolve buy time when a run starts going wrong.",
                3,
                new PerkEffect[]
                {
                    new("stat_bonus", "MaxHP", 6),
                    new("stat_bonus", "Defense", 1),
                }),
        };

        DialogueTemplates = new Dictionary<string, DialogueTemplate>
        {
            ["merchant_intro"] = new(
                "merchant_intro",
                "start",
                new Dictionary<string, DialogueNode>
                {
                    ["start"] = new(
                        "start",
                        "Supply lines are bad down here. Spend well and leave with something sharp.",
                        new DialogueOption[]
                        {
                            new("Show me your stock.", null, "shop"),
                            new("Any advice?", "advice", null),
                            new("Maybe later.", null, "close"),
                        }),
                    ["advice"] = new(
                        "advice",
                        "Buy mobility first, defense second, and only carry the consumables you will actually use this floor.",
                        new DialogueOption[]
                        {
                            new("Back to business.", "start", null),
                            new("I am done here.", null, "close"),
                        }),
                }),
            ["chronicler_intro"] = new(
                "chronicler_intro",
                "start",
                new Dictionary<string, DialogueNode>
                {
                    ["start"] = new(
                        "start",
                        "Every floor teaches the same lesson: carry fewer things, but know exactly why each one is in your pack.",
                        new DialogueOption[]
                        {
                            new("Explain.", "pack_logic", null),
                            new("I have heard enough.", null, "close"),
                        }),
                    ["pack_logic"] = new(
                        "pack_logic",
                        "A clean bag means faster decisions. Sell dead weight, keep one emergency heal, and compare gear by the stat it changes for your build.",
                        new DialogueOption[]
                        {
                            new("That helps.", null, "close"),
                        }),
                }),
        };

        NpcTemplates = new Dictionary<string, NpcTemplate>
        {
            ["quartermaster"] = new(
                "quartermaster",
                "Quartermaster Vale",
                "A cautious outfitter who trades in practical dungeon gear.",
                "shopkeeper",
                0,
                -1,
                "merchant_intro",
                "human",
                "neutral",
                "weathered",
                "vanguard",
                new MerchantOfferTemplate[]
                {
                    new("potion_health", 24, 4),
                    new("shield_wooden", 48, 1),
                    new("sword_iron", 70, 1),
                }),
            ["field_chronicler"] = new(
                "field_chronicler",
                "Field Chronicler Sen",
                "A quiet record-keeper who studies how adventurers fail and why some return.",
                "advisor",
                0,
                -1,
                "chronicler_intro",
                "elf",
                "feminine",
                "scarred",
                "mystic"),
        };

        TrapTemplates = new Dictionary<string, TrapTemplate>
        {
            ["spike_trap"] = new(
                "spike_trap",
                "Spike Trap",
                "Sharp iron spikes hidden in the floor impale whoever steps on the tile.",
                5,
                10,
                DamageType.Physical,
                null,
                0,
                0,
                null,
                100,
                "spike_trap",
                "res://Assets/Sprites/objects/trap_spikes.svg"),
        };

        StatusEffects = new Dictionary<string, StatusEffectDefinition>
        {
            ["poisoned"] = new()
            {
                Id = "poisoned",
                Name = "Poisoned",
                Stackable = false,
                Refreshable = true,
                TickEffects = new() { new() { Type = "damage", DamageType = "poison", Value = 2 } },
                IconPath = "res://Assets/Sprites/ui/status_poison.svg",
                ColorTint = "#44FF4488",
            },
            ["burning"] = new()
            {
                Id = "burning",
                Name = "Burning",
                Stackable = false,
                Refreshable = true,
                TickEffects = new() { new() { Type = "damage", DamageType = "fire", Value = 3 } },
                OnApplyEffects = new() { new() { Type = "remove_status", StatusId = "frozen" } },
                IconPath = "res://Assets/Sprites/ui/status_burn.svg",
                ColorTint = "#FF660088",
            },
            ["frozen"] = new()
            {
                Id = "frozen",
                Name = "Frozen",
                Stackable = false,
                Refreshable = false,
                Flags = new() { "skip_turn" },
                StatModifiers = new()
                {
                    new() { Stat = "defense", Operation = "add", Value = 5 },
                    new() { Stat = "speed", Operation = "set", Value = 0 },
                },
                OnApplyEffects = new() { new() { Type = "remove_status", StatusId = "burning" } },
                IconPath = "res://Assets/Sprites/ui/status_frozen.svg",
                ColorTint = "#88CCFF88",
            },
            ["stunned"] = new()
            {
                Id = "stunned",
                Name = "Stunned",
                Stackable = false,
                Refreshable = false,
                Flags = new() { "skip_turn" },
                IconPath = "res://Assets/Sprites/ui/status_stun.svg",
                ColorTint = "#FFFF0088",
            },
            ["haste"] = new()
            {
                Id = "haste",
                Name = "Haste",
                Stackable = false,
                Refreshable = true,
                StatModifiers = new() { new() { Stat = "speed", Operation = "multiply", Value = 1.5 } },
                IconPath = "res://Assets/Sprites/ui/status_haste.svg",
                ColorTint = "#00CCFF88",
            },
            ["regenerating"] = new()
            {
                Id = "regenerating",
                Name = "Regenerating",
                Stackable = true,
                MaxStacks = 3,
                Refreshable = true,
                TickEffects = new() { new() { Type = "heal", Value = 2 } },
                IconPath = "res://Assets/Sprites/ui/status_regen.svg",
                ColorTint = "#00FF0088",
            },
            ["phased"] = new()
            {
                Id = "phased",
                Name = "Phased",
                Stackable = false,
                Refreshable = false,
                Flags = new() { "phase_through_walls", "immune_physical" },
                IconPath = "res://Assets/Sprites/ui/status_phased.svg",
                ColorTint = "#AAAAFF44",
            },
            ["flying"] = new()
            {
                Id = "flying",
                Name = "Flying",
                Stackable = false,
                Refreshable = false,
                Flags = new() { "flying" },
                IconPath = "res://Assets/Sprites/ui/status_flying.svg",
                ColorTint = "#CCCCFF88",
            },
        };

        TrapTemplates = new Dictionary<string, TrapTemplate>
        {
            ["spike_trap"] = new(
                "spike_trap",
                "Spike Trap",
                "Sharp spikes impale the victim.",
                3,
                5,
                DamageType.Physical,
                null,
                0,
                0,
                new[] { "phase_through_walls", "flying" },
                100,
                "spike_trap"),
            ["poison_needle"] = new(
                "poison_needle",
                "Poison Needle",
                "A tiny needle coated with venom.",
                1,
                1,
                DamageType.Physical,
                "poisoned",
                3,
                1,
                null,
                100),
            ["deadly_pit"] = new(
                "deadly_pit",
                "Deadly Pit",
                "A bottomless pit.",
                100,
                100,
                DamageType.Physical,
                null,
                0,
                0,
                null,
                100),
            ["ground_only"] = new(
                "ground_only",
                "Ground Only",
                "Only harms grounded creatures.",
                5,
                5,
                DamageType.Physical,
                null,
                0,
                0,
                new[] { "flying", "phase_through_walls" },
                100),
        };

        RelicTemplates = new Dictionary<string, RelicTemplate>();
        FloorEvents = new Dictionary<string, FloorEventDefinition>();
    }


    public int ContentVersion { get; set; } = DefaultContentVersion;

    public string ContentHash { get; set; } = DefaultContentHash;

    public IReadOnlyDictionary<string, ItemTemplate> ItemTemplates { get; } = null!;

    public IReadOnlyDictionary<string, EnemyTemplate> EnemyTemplates { get; } = null!;

    public IReadOnlyDictionary<string, AbilityTemplate> AbilityTemplates { get; } = null!;

    public IReadOnlyDictionary<string, PerkTemplate> PerkTemplates { get; } = null!;

    public IReadOnlyDictionary<string, NpcTemplate> NpcTemplates { get; } = null!;

    public IReadOnlyDictionary<string, DialogueTemplate> DialogueTemplates { get; } = null!;

    public IReadOnlyDictionary<string, StatusEffectDefinition> StatusEffects { get; } = null!;

    public IReadOnlyDictionary<string, TrapTemplate> TrapTemplates { get; } = null!;

    public IReadOnlyDictionary<string, RelicTemplate> RelicTemplates { get; } = null!;

    public IReadOnlyDictionary<string, FloorEventDefinition> FloorEvents { get; } = null!;

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

    public bool TryGetPerkTemplate(string perkId, out PerkTemplate template)
    {
        var found = PerkTemplates.TryGetValue(perkId, out var perk);
        template = perk!;
        return found;
    }

    public bool TryGetNpcTemplate(string templateId, out NpcTemplate template)
    {
        var found = NpcTemplates.TryGetValue(templateId, out var npc);
        template = npc!;
        return found;
    }

    public bool TryGetDialogueTemplate(string dialogueId, out DialogueTemplate template)
    {
        var found = DialogueTemplates.TryGetValue(dialogueId, out var dialogue);
        template = dialogue!;
        return found;
    }

    public bool TryGetStatusEffect(string statusId, out StatusEffectDefinition definition)
    {
        var found = StatusEffects.TryGetValue(statusId, out var status);
        definition = status!;
        return found;
    }

    public bool TryGetTrapTemplate(string templateId, out TrapTemplate template)
    {
        var found = TrapTemplates.TryGetValue(templateId, out var trap);
        template = trap!;
        return found;
    }

    public bool TryGetRelicTemplate(string relicId, out RelicTemplate template)
    {
        var found = RelicTemplates.TryGetValue(relicId, out var relic);
        template = relic!;
        return found;
    }

    public bool TryGetFloorEvent(string eventId, out FloorEventDefinition definition)
    {
        var found = FloorEvents.TryGetValue(eventId, out var floorEvent);
        definition = floorEvent!;
        return found;
    }

    public IReadOnlyList<ItemTemplate> GetAvailableItems(int depth) =>
        ItemTemplates.Values.ToArray();

    public IReadOnlyList<EnemyTemplate> GetAvailableEnemies(int depth) =>
        EnemyTemplates.Values.Where(enemy => depth >= enemy.MinDepth && (enemy.MaxDepth < 0 || depth <= enemy.MaxDepth)).ToArray();

    public IReadOnlyList<NpcTemplate> GetAvailableNpcs(int depth) =>
        NpcTemplates.Values.Where(npc => depth >= npc.MinDepth && (npc.MaxDepth < 0 || depth <= npc.MaxDepth)).ToArray();
}
