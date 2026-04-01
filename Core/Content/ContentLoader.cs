using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Roguelike.Core;

public sealed class ContentLoader : IContentDatabase
{
    private const int SupportedVersion = 1;
    private static readonly Regex StableIdPattern = new("^[a-z0-9_]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
    };

    public string ContentDirectory { get; }

    public IReadOnlyDictionary<string, ItemTemplate> ItemTemplates { get; }

    public IReadOnlyDictionary<string, EnemyTemplate> EnemyTemplates { get; }

    public IReadOnlyDictionary<string, AbilityTemplate> AbilityTemplates { get; }

    public IReadOnlyDictionary<string, PerkTemplate> PerkTemplates { get; }

    public IReadOnlyDictionary<string, NpcTemplate> NpcTemplates { get; }

    public IReadOnlyDictionary<string, DialogueTemplate> DialogueTemplates { get; }

    public IReadOnlyDictionary<string, ItemDefinition> ItemDefinitions { get; }

    public IReadOnlyDictionary<string, EnemyDefinition> EnemyDefinitions { get; }

    public IReadOnlyDictionary<string, AbilityDefinition> AbilityDefinitions { get; }

    public IReadOnlyDictionary<string, PerkDefinition> PerkDefinitions { get; }

    public IReadOnlyDictionary<string, NpcDefinition> NpcDefinitions { get; }

    public IReadOnlyDictionary<string, DialogueDefinition> DialogueDefinitions { get; }

    public IReadOnlyDictionary<string, StatusEffectDefinition> StatusEffects { get; }

    public IReadOnlyDictionary<string, RoomPrefabDefinition> RoomPrefabs { get; }

    public IReadOnlyDictionary<string, LootTableDefinition> LootTables { get; }

    public IReadOnlyDictionary<string, string> TileLegend { get; }

    public IReadOnlyList<string> ValidationErrors { get; }

    public bool IsValid => ValidationErrors.Count == 0;

    private ContentLoader(
        string contentDirectory,
        SortedDictionary<string, ItemDefinition> itemDefinitions,
        SortedDictionary<string, EnemyDefinition> enemyDefinitions,
        SortedDictionary<string, AbilityDefinition> abilityDefinitions,
        SortedDictionary<string, PerkDefinition> perkDefinitions,
        SortedDictionary<string, NpcDefinition> npcDefinitions,
        SortedDictionary<string, DialogueDefinition> dialogueDefinitions,
        SortedDictionary<string, StatusEffectDefinition> statusEffects,
        SortedDictionary<string, RoomPrefabDefinition> roomPrefabs,
        SortedDictionary<string, LootTableDefinition> lootTables,
        SortedDictionary<string, string> tileLegend,
        SortedDictionary<string, ItemTemplate> itemTemplates,
        SortedDictionary<string, EnemyTemplate> enemyTemplates,
        SortedDictionary<string, AbilityTemplate> abilityTemplates,
        SortedDictionary<string, PerkTemplate> perkTemplates,
        SortedDictionary<string, NpcTemplate> npcTemplates,
        SortedDictionary<string, DialogueTemplate> dialogueTemplates,
        IReadOnlyList<string> validationErrors)
    {
        ContentDirectory = contentDirectory;
        ItemDefinitions = new ReadOnlyDictionary<string, ItemDefinition>(itemDefinitions);
        EnemyDefinitions = new ReadOnlyDictionary<string, EnemyDefinition>(enemyDefinitions);
        AbilityDefinitions = new ReadOnlyDictionary<string, AbilityDefinition>(abilityDefinitions);
        PerkDefinitions = new ReadOnlyDictionary<string, PerkDefinition>(perkDefinitions);
        NpcDefinitions = new ReadOnlyDictionary<string, NpcDefinition>(npcDefinitions);
        DialogueDefinitions = new ReadOnlyDictionary<string, DialogueDefinition>(dialogueDefinitions);
        StatusEffects = new ReadOnlyDictionary<string, StatusEffectDefinition>(statusEffects);
        RoomPrefabs = new ReadOnlyDictionary<string, RoomPrefabDefinition>(roomPrefabs);
        LootTables = new ReadOnlyDictionary<string, LootTableDefinition>(lootTables);
        TileLegend = new ReadOnlyDictionary<string, string>(tileLegend);
        ItemTemplates = new ReadOnlyDictionary<string, ItemTemplate>(itemTemplates);
        EnemyTemplates = new ReadOnlyDictionary<string, EnemyTemplate>(enemyTemplates);
        AbilityTemplates = new ReadOnlyDictionary<string, AbilityTemplate>(abilityTemplates);
        PerkTemplates = new ReadOnlyDictionary<string, PerkTemplate>(perkTemplates);
        NpcTemplates = new ReadOnlyDictionary<string, NpcTemplate>(npcTemplates);
        DialogueTemplates = new ReadOnlyDictionary<string, DialogueTemplate>(dialogueTemplates);
        ValidationErrors = validationErrors;
    }

    public static ContentLoader LoadFromDirectory(string contentDirectory, bool throwOnValidationErrors = true)
    {
        var fullDirectory = Path.GetFullPath(contentDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException($"Content directory '{fullDirectory}' does not exist.");
        }

        var items = ReadDocument<ItemsDocument>(Path.Combine(fullDirectory, "items.json"));
        var enemies = ReadDocument<EnemiesDocument>(Path.Combine(fullDirectory, "enemies.json"));
        var abilities = ReadDocument<AbilitiesDocument>(Path.Combine(fullDirectory, "abilities.json"));
        var perks = ReadDocument<PerksDocument>(Path.Combine(fullDirectory, "perks.json"));
        var dialogs = ReadDocument<DialogsDocument>(Path.Combine(fullDirectory, "dialogs.json"));
        var npcs = ReadDocument<NpcsDocument>(Path.Combine(fullDirectory, "npcs.json"));
        var statusEffects = ReadDocument<StatusEffectsDocument>(Path.Combine(fullDirectory, "status_effects.json"));
        var rooms = ReadDocument<RoomPrefabsDocument>(Path.Combine(fullDirectory, "room_prefabs.json"));
        var lootTables = ReadDocument<LootTablesDocument>(Path.Combine(fullDirectory, "loot_tables.json"));

        var itemDefinitions = BuildLookup(items.Items, item => item.Id, "item");
        var enemyDefinitions = BuildLookup(enemies.Enemies, enemy => enemy.Id, "enemy");
        var abilityDefinitions = BuildLookup(abilities.Abilities, ability => ability.Id, "ability");
        var perkDefinitions = BuildLookup(perks.Perks, perk => perk.Id, "perk");
        var dialogueDefinitions = BuildLookup(dialogs.Dialogs, dialogue => dialogue.Id, "dialog");
        var npcDefinitions = BuildLookup(npcs.Npcs, npc => npc.Id, "npc");
        var statusDefinitions = BuildLookup(statusEffects.StatusEffects, effect => effect.Id, "status effect");
        var roomDefinitions = BuildLookup(rooms.Rooms, room => room.Id, "room prefab");
        var lootDefinitions = BuildLookup(lootTables.LootTables, table => table.Id, "loot table");
        var tileLegend = new SortedDictionary<string, string>(rooms.TileLegend, StringComparer.Ordinal);

        var itemTemplates = new SortedDictionary<string, ItemTemplate>(StringComparer.Ordinal);
        foreach (var definition in itemDefinitions.Values)
        {
            itemTemplates[definition.Id] = BuildItemTemplate(definition);
        }

        var enemyTemplates = new SortedDictionary<string, EnemyTemplate>(StringComparer.Ordinal);
        foreach (var definition in enemyDefinitions.Values)
        {
            enemyTemplates[definition.Id] = BuildEnemyTemplate(definition);
        }

        var abilityTemplates = new SortedDictionary<string, AbilityTemplate>(StringComparer.Ordinal);
        foreach (var definition in abilityDefinitions.Values)
        {
            abilityTemplates[definition.Id] = BuildAbilityTemplate(definition);
        }

        var perkTemplates = new SortedDictionary<string, PerkTemplate>(StringComparer.Ordinal);
        foreach (var definition in perkDefinitions.Values)
        {
            perkTemplates[definition.Id] = BuildPerkTemplate(definition);
        }

        var dialogueTemplates = new SortedDictionary<string, DialogueTemplate>(StringComparer.Ordinal);
        foreach (var definition in dialogueDefinitions.Values)
        {
            dialogueTemplates[definition.Id] = BuildDialogueTemplate(definition);
        }

        var npcTemplates = new SortedDictionary<string, NpcTemplate>(StringComparer.Ordinal);
        foreach (var definition in npcDefinitions.Values)
        {
            npcTemplates[definition.Id] = BuildNpcTemplate(definition);
        }

        var validationErrors = Validate(
            items,
            enemies,
            abilities,
            perks,
            dialogs,
            npcs,
            statusEffects,
            rooms,
            lootTables,
            itemDefinitions,
            enemyDefinitions,
            abilityDefinitions,
            perkDefinitions,
            dialogueDefinitions,
            npcDefinitions,
            statusDefinitions,
            roomDefinitions,
            lootDefinitions,
            tileLegend);

        validationErrors.AddRange(DifficultyScaler.ValidateBalance(itemDefinitions, enemyDefinitions, lootDefinitions));

        var loader = new ContentLoader(
            fullDirectory,
            itemDefinitions,
            enemyDefinitions,
            abilityDefinitions,
            perkDefinitions,
            npcDefinitions,
            dialogueDefinitions,
            statusDefinitions,
            roomDefinitions,
            lootDefinitions,
            tileLegend,
            itemTemplates,
            enemyTemplates,
            abilityTemplates,
            perkTemplates,
            npcTemplates,
            dialogueTemplates,
            validationErrors.AsReadOnly());

        if (throwOnValidationErrors)
        {
            loader.EnsureValid();
        }

        return loader;
    }

    public static ContentLoader LoadFromRepository(string? startDirectory = null, bool throwOnValidationErrors = true)
    {
        return LoadFromDirectory(FindContentDirectory(startDirectory), throwOnValidationErrors);
    }

    public static string FindContentDirectory(string? startDirectory = null)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory ?? AppContext.BaseDirectory));
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Content");
            if (File.Exists(Path.Combine(candidate, "items.json"))
                && File.Exists(Path.Combine(candidate, "enemies.json"))
                && File.Exists(Path.Combine(candidate, "abilities.json"))
                && File.Exists(Path.Combine(candidate, "perks.json"))
                && File.Exists(Path.Combine(candidate, "dialogs.json"))
                && File.Exists(Path.Combine(candidate, "npcs.json"))
                && File.Exists(Path.Combine(candidate, "status_effects.json"))
                && File.Exists(Path.Combine(candidate, "room_prefabs.json"))
                && File.Exists(Path.Combine(candidate, "loot_tables.json")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository Content directory.");
    }

    public bool TryGetItemTemplate(string templateId, out ItemTemplate template)
    {
        var found = ItemTemplates.TryGetValue(templateId, out var itemTemplate);
        template = itemTemplate!;
        return found;
    }

    public bool TryGetEnemyTemplate(string templateId, out EnemyTemplate template)
    {
        var found = EnemyTemplates.TryGetValue(templateId, out var enemyTemplate);
        template = enemyTemplate!;
        return found;
    }

    public bool TryGetAbilityTemplate(string abilityId, out AbilityTemplate template)
    {
        var found = AbilityTemplates.TryGetValue(abilityId, out var abilityTemplate);
        template = abilityTemplate!;
        return found;
    }

    public bool TryGetPerkTemplate(string perkId, out PerkTemplate template)
    {
        var found = PerkTemplates.TryGetValue(perkId, out var perkTemplate);
        template = perkTemplate!;
        return found;
    }

    public bool TryGetNpcTemplate(string templateId, out NpcTemplate template)
    {
        var found = NpcTemplates.TryGetValue(templateId, out var npcTemplate);
        template = npcTemplate!;
        return found;
    }

    public bool TryGetDialogueTemplate(string dialogueId, out DialogueTemplate template)
    {
        var found = DialogueTemplates.TryGetValue(dialogueId, out var dialogueTemplate);
        template = dialogueTemplate!;
        return found;
    }

    public IReadOnlyList<ItemTemplate> GetAvailableItems(int depth)
    {
        return ItemDefinitions.Values
            .Where(item => IsItemAvailableAtDepth(item, depth))
            .Select(item => ItemTemplates[item.Id])
            .OrderBy(item => item.TemplateId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<EnemyTemplate> GetAvailableEnemies(int depth)
    {
        return EnemyDefinitions.Values
            .Where(enemy => depth >= enemy.MinDepth && depth <= enemy.MaxDepth)
            .Select(enemy => EnemyTemplates[enemy.Id])
            .OrderBy(enemy => enemy.TemplateId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<NpcTemplate> GetAvailableNpcs(int depth)
    {
        return NpcDefinitions.Values
            .Where(npc => depth >= npc.MinDepth && (npc.MaxDepth < 0 || depth <= npc.MaxDepth))
            .Select(npc => NpcTemplates[npc.Id])
            .OrderBy(npc => npc.TemplateId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool IsItemAvailableAtDepth(string itemId, int depth)
    {
        return ItemDefinitions.TryGetValue(itemId, out var item) && IsItemAvailableAtDepth(item, depth);
    }

    public void EnsureValid()
    {
        if (ValidationErrors.Count == 0)
        {
            return;
        }

        throw new InvalidDataException(string.Join(Environment.NewLine, ValidationErrors));
    }

    private static T ReadDocument<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required content file '{path}' was not found.", path);
        }

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<T>(json, SerializerOptions);
        if (document is null)
        {
            throw new InvalidDataException($"Failed to deserialize content file '{path}'.");
        }

        return document;
    }

    private static SortedDictionary<string, T> BuildLookup<T>(IEnumerable<T> source, Func<T, string> idSelector, string kind)
    {
        var lookup = new SortedDictionary<string, T>(StringComparer.Ordinal);

        foreach (var entry in source)
        {
            var id = idSelector(entry);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidDataException($"Encountered a {kind} with an empty id.");
            }

            if (!lookup.TryAdd(id, entry))
            {
                throw new InvalidDataException($"Duplicate {kind} id '{id}' detected.");
            }
        }

        return lookup;
    }

    private static ItemTemplate BuildItemTemplate(ItemDefinition item)
    {
        var slot = ParseEquipSlot(item.Slot);
        var category = MapItemCategory(item.Type, slot);
        var statModifiers = BuildItemStatModifiers(item);
        var useEffect = BuildUseEffect(item.Effects);
        var maxStack = item.Stackable ? Math.Max(item.MaxStack ?? 1, 1) : 1;
        var maxCharges = string.Equals(item.Type, "consumable", StringComparison.Ordinal) ? -1 : 0;

        item.Stats.TryGetValue("damage_min", out var damageMin);
        item.Stats.TryGetValue("damage_max", out var damageMax);
        item.Stats.TryGetValue("crit_chance", out var critChance);
        item.Stats.TryGetValue("accuracy", out var weaponAccuracy);
        item.Stats.TryGetValue("speed_modifier", out var speedModifier);

        var onHitEffects = BuildOnHitEffects(item.Effects);

        var requirements = item.Requirements.Count > 0
            ? (IReadOnlyDictionary<string, int>)new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(item.Requirements, StringComparer.OrdinalIgnoreCase))
            : null;

        return new ItemTemplate(
            item.Id,
            item.Name,
            item.Description,
            category,
            slot,
            statModifiers,
            useEffect,
            maxCharges,
            maxStack,
            item.Rarity,
            damageMin,
            damageMax,
            critChance,
            weaponAccuracy,
            speedModifier,
            onHitEffects,
            requirements,
            item.Value,
            item.Weight);
    }

    private static DialogueTemplate BuildDialogueTemplate(DialogueDefinition dialogue)
    {
        var nodes = new SortedDictionary<string, DialogueNode>(StringComparer.Ordinal);
        foreach (var node in dialogue.Nodes)
        {
            nodes[node.Id] = new DialogueNode(
                node.Id,
                node.Text,
                node.Options.Select(option => new DialogueOption(option.Text, option.Next, option.Action)).ToArray());
        }

        return new DialogueTemplate(dialogue.Id, dialogue.StartNode, new ReadOnlyDictionary<string, DialogueNode>(nodes));
    }

    private static NpcTemplate BuildNpcTemplate(NpcDefinition npc)
    {
        IReadOnlyList<MerchantOfferTemplate>? merchantOffers = null;
        if (npc.Stock.Count > 0)
        {
            merchantOffers = npc.Stock
                .Select(stock => new MerchantOfferTemplate(stock.ItemId, stock.Price, stock.Quantity))
                .ToArray();
        }

        return new NpcTemplate(
            npc.Id,
            npc.Name,
            npc.Description,
            npc.Role,
            npc.MinDepth,
            npc.MaxDepth,
            npc.DialogueId,
            string.IsNullOrWhiteSpace(npc.RaceId) ? "human" : npc.RaceId,
            string.IsNullOrWhiteSpace(npc.GenderId) ? "neutral" : npc.GenderId,
            string.IsNullOrWhiteSpace(npc.AppearanceId) ? "default" : npc.AppearanceId,
            string.IsNullOrWhiteSpace(npc.ArchetypeId) ? "adventurer" : npc.ArchetypeId,
            merchantOffers);
    }

    private static IReadOnlyList<WeaponOnHitEffect>? BuildOnHitEffects(List<ItemEffectDefinition> effects)
    {
        List<WeaponOnHitEffect>? onHit = null;
        foreach (var effect in effects)
        {
            if (!string.Equals(effect.Type, "on_hit", StringComparison.Ordinal) || effect.StatusEffect is null)
            {
                continue;
            }

            if (!StatusEffectProcessor.TryParseStatusEffect(effect.StatusEffect, out var statusType))
            {
                continue;
            }

            onHit ??= new List<WeaponOnHitEffect>();
            onHit.Add(new WeaponOnHitEffect(statusType, effect.Chance ?? 0, effect.Duration ?? 0));
        }

        return onHit?.AsReadOnly();
    }

    private static EnemyTemplate BuildEnemyTemplate(EnemyDefinition enemy)
    {
        var baseStats = new Stats
        {
            HP = enemy.Stats.HP,
            MaxHP = enemy.Stats.HP,
            Attack = enemy.Stats.Attack,
            Accuracy = enemy.Stats.Accuracy,
            Defense = enemy.Stats.Defense,
            Evasion = enemy.Stats.Evasion,
            Speed = NormalizeSpeed(enemy.Stats.Speed),
            ViewRadius = enemy.Stats.FovRange,
        };

        return new EnemyTemplate(
            enemy.Id,
            enemy.Name,
            enemy.Description,
            baseStats,
            MapBrainType(enemy.AiType),
            ParseFaction(enemy.Faction),
            enemy.MinDepth,
            enemy.MaxDepth,
            enemy.SpawnWeight,
            enemy.LootTableId,
            enemy.Stats.XpValue);
    }

    private static AbilityTemplate BuildAbilityTemplate(AbilityDefinition ability)
    {
        var targeting = new AbilityTargeting(
            ability.Targeting.Type,
            ability.Targeting.Range,
            ability.Targeting.Radius ?? 0,
            ability.Targeting.RequiresLos,
            ability.Targeting.RequiresWalkable,
            ability.Targeting.HitsAllies ?? false,
            ability.Targeting.Center);

        var effects = new List<AbilityEffect>();
        foreach (var effect in ability.Effects)
        {
            var damageType = DamageType.Physical;
            if (!string.IsNullOrEmpty(effect.DamageType) && Enum.TryParse<DamageType>(effect.DamageType, ignoreCase: true, out var parsed))
            {
                damageType = parsed;
            }

            effects.Add(new AbilityEffect(
                effect.Type,
                damageType,
                effect.BaseValue ?? 0,
                effect.StatScaling?.Stat,
                effect.StatScaling?.Factor ?? 0.0,
                effect.StatusEffect,
                effect.Chance ?? 0,
                effect.Duration ?? 0,
                effect.Filter,
                effect.ValueSource,
                effect.Factor ?? 0.0,
                effect.Destination));
        }

        return new AbilityTemplate(
            ability.Id,
            ability.Name,
            ability.Description,
            targeting,
            ability.Costs.Energy,
            ability.Costs.MP,
            effects.AsReadOnly());
    }

    private static PerkTemplate BuildPerkTemplate(PerkDefinition perk)
    {
        var effects = perk.Effects
            .Select(effect => new PerkEffect(effect.Type, effect.Stat, effect.Value))
            .ToArray();

        return new PerkTemplate(
            perk.Id,
            perk.Name,
            perk.Description,
            perk.UnlockLevel,
            effects);
    }

    private static ReadOnlyDictionary<string, int> BuildItemStatModifiers(ItemDefinition item)
    {
        var modifiers = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var hasDamageMin = item.Stats.TryGetValue("damage_min", out var damageMin);
        var hasDamageMax = item.Stats.TryGetValue("damage_max", out var damageMax);

        if (hasDamageMin || hasDamageMax)
        {
            if (!hasDamageMin)
            {
                damageMin = damageMax;
            }

            if (!hasDamageMax)
            {
                damageMax = damageMin;
            }

            var average = (damageMin + damageMax) / 2;
            modifiers["Attack"] = average;
        }

        foreach (var (key, value) in item.Stats)
        {
            switch (key)
            {
                case "damage_min":
                case "damage_max":
                    break;
                case "defense":
                    modifiers["Defense"] = value;
                    break;
                case "accuracy":
                    modifiers["Accuracy"] = value;
                    break;
                case "speed_modifier":
                    modifiers["Speed"] = value;
                    break;
                case "fov_bonus":
                    modifiers["ViewRadius"] = value;
                    break;
                default:
                    modifiers[ToPascalCase(key)] = value;
                    break;
            }
        }

        return new ReadOnlyDictionary<string, int>(modifiers);
    }

    private static string? BuildUseEffect(IEnumerable<ItemEffectDefinition> effects)
    {
        foreach (var effect in effects)
        {
            switch (effect.Type)
            {
                case "on_use" when string.Equals(effect.Action, "heal", StringComparison.Ordinal):
                    return "heal";
                case "on_use" when string.Equals(effect.Action, "apply_status", StringComparison.Ordinal):
                    return effect.StatusEffect is null ? "apply_status" : $"apply_status:{effect.StatusEffect}";
                case "on_use" when string.Equals(effect.Action, "cast_ability", StringComparison.Ordinal):
                    return effect.AbilityId is null ? "cast_ability" : $"cast_ability:{effect.AbilityId}";
                case "on_hit":
                    return effect.StatusEffect is null ? "on_hit" : $"on_hit:{effect.StatusEffect}";
                case "passive" when !string.IsNullOrWhiteSpace(effect.Action):
                    return $"passive:{effect.Action}";
            }
        }

        return null;
    }

    private static int NormalizeSpeed(int rawSpeed)
    {
        return rawSpeed > 300 ? Math.Max(1, rawSpeed / 10) : rawSpeed;
    }

    private static EquipSlot ParseEquipSlot(string slot)
    {
        return slot switch
        {
            "none" => EquipSlot.None,
            "main_hand" => EquipSlot.MainHand,
            "off_hand" => EquipSlot.OffHand,
            "head" => EquipSlot.Head,
            "body" => EquipSlot.Body,
            "feet" => EquipSlot.Feet,
            "ring" => EquipSlot.Ring,
            "amulet" => EquipSlot.Amulet,
            _ => throw new InvalidDataException($"Unknown equip slot '{slot}'."),
        };
    }

    private static ItemCategory MapItemCategory(string type, EquipSlot slot)
    {
        return type switch
        {
            "weapon" => ItemCategory.Weapon,
            "armor" => ItemCategory.Armor,
            "consumable" => slot == EquipSlot.None ? ItemCategory.Consumable : ItemCategory.Scroll,
            "accessory" => ItemCategory.Misc,
            _ => ItemCategory.Misc,
        };
    }

    private static Faction ParseFaction(string faction)
    {
        if (Enum.TryParse<Faction>(faction, ignoreCase: true, out var parsedFaction))
        {
            return parsedFaction;
        }

        throw new InvalidDataException($"Unknown faction '{faction}'.");
    }

    private static string MapBrainType(string aiType)
    {
        return aiType switch
        {
            "melee_rush" => "melee_rusher",
            "ranged_kite" => "ranged_kiter",
            "ambush" => "ambush",
            "patrol" => "patrol_guard",
            "support" => "support",
            _ => aiType,
        };
    }

    private static bool IsItemAvailableAtDepth(ItemDefinition item, int depth)
    {
        var requiredLevel = item.Requirements.TryGetValue("level", out var level) ? level : 1;
        return requiredLevel <= Math.Max(1, depth + 1);
    }

    private static string ToPascalCase(string value)
    {
        var parts = value.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static List<string> Validate(
        ItemsDocument items,
        EnemiesDocument enemies,
        AbilitiesDocument abilities,
        PerksDocument perks,
        DialogsDocument dialogs,
        NpcsDocument npcs,
        StatusEffectsDocument statusEffects,
        RoomPrefabsDocument rooms,
        LootTablesDocument lootTables,
        IReadOnlyDictionary<string, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<string, EnemyDefinition> enemyDefinitions,
        IReadOnlyDictionary<string, AbilityDefinition> abilityDefinitions,
        IReadOnlyDictionary<string, PerkDefinition> perkDefinitions,
        IReadOnlyDictionary<string, DialogueDefinition> dialogueDefinitions,
        IReadOnlyDictionary<string, NpcDefinition> npcDefinitions,
        IReadOnlyDictionary<string, StatusEffectDefinition> statusDefinitions,
        IReadOnlyDictionary<string, RoomPrefabDefinition> roomDefinitions,
        IReadOnlyDictionary<string, LootTableDefinition> lootDefinitions,
        IReadOnlyDictionary<string, string> tileLegend)
    {
        var errors = new List<string>();

        ValidateHeader(items.Schema, items.Version, "roguelike-items-v1", "items.json", errors);
        ValidateHeader(enemies.Schema, enemies.Version, "roguelike-enemies-v1", "enemies.json", errors);
        ValidateHeader(abilities.Schema, abilities.Version, "roguelike-abilities-v1", "abilities.json", errors);
        ValidateHeader(perks.Schema, perks.Version, "roguelike-perks-v1", "perks.json", errors);
        ValidateHeader(dialogs.Schema, dialogs.Version, "roguelike-dialogs-v1", "dialogs.json", errors);
        ValidateHeader(npcs.Schema, npcs.Version, "roguelike-npcs-v1", "npcs.json", errors);
        ValidateHeader(statusEffects.Schema, statusEffects.Version, "roguelike-status-effects-v1", "status_effects.json", errors);
        ValidateHeader(rooms.Schema, rooms.Version, "roguelike-room-prefabs-v1", "room_prefabs.json", errors);
        ValidateHeader(lootTables.Schema, lootTables.Version, "roguelike-loot-tables-v1", "loot_tables.json", errors);

        ValidateItems(itemDefinitions, abilityDefinitions, statusDefinitions, errors);
        ValidateEnemies(enemyDefinitions, abilityDefinitions, lootDefinitions, errors);
        ValidateAbilities(abilityDefinitions, statusDefinitions, errors);
        ValidatePerks(perkDefinitions, errors);
        ValidateDialogs(dialogueDefinitions, errors);
        ValidateNpcs(npcDefinitions, dialogueDefinitions, itemDefinitions, errors);
        ValidateStatusEffects(statusDefinitions, errors);
        ValidateRooms(roomDefinitions, enemyDefinitions, itemDefinitions, npcDefinitions, lootDefinitions, tileLegend, errors);
        ValidateLootTables(lootDefinitions, itemDefinitions, errors);

        return errors;
    }

    private static void ValidateHeader(string actualSchema, int actualVersion, string expectedSchema, string fileName, ICollection<string> errors)
    {
        if (!string.Equals(actualSchema, expectedSchema, StringComparison.Ordinal))
        {
            errors.Add($"{fileName} schema must be '{expectedSchema}' but was '{actualSchema}'.");
        }

        if (actualVersion != SupportedVersion)
        {
            errors.Add($"{fileName} version must be {SupportedVersion} but was {actualVersion}.");
        }
    }

    private static void ValidateItems(
        IReadOnlyDictionary<string, ItemDefinition> items,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        IReadOnlyDictionary<string, StatusEffectDefinition> statusEffects,
        ICollection<string> errors)
    {
        foreach (var (id, item) in items)
        {
            ValidateStableId(id, "Item", errors);
            ValidateRequiredText(item.Name, $"Item '{id}' name", errors);
            ValidateRequiredText(item.Description, $"Item '{id}' description", errors);
            ValidateRequiredText(item.Type, $"Item '{id}' type", errors);
            ValidateRequiredText(item.Slot, $"Item '{id}' slot", errors);
            ValidateRequiredText(item.Rarity, $"Item '{id}' rarity", errors);
            ValidateRequiredText(item.SpritePath, $"Item '{id}' sprite_path", errors);

            if (item.Value < 0)
            {
                errors.Add($"Item '{id}' has negative value.");
            }

            if (item.Weight <= 0)
            {
                errors.Add($"Item '{id}' must have positive weight.");
            }

            if (item.SpriteAtlasCoords.Count != 2)
            {
                errors.Add($"Item '{id}' must define exactly two sprite atlas coordinates.");
            }

            if (!IsAllowedValue(item.Type, "weapon", "armor", "consumable", "accessory"))
            {
                errors.Add($"Item '{id}' has unknown type '{item.Type}'.");
            }

            if (string.Equals(item.Type, "consumable", StringComparison.Ordinal) && item.Slot != "none")
            {
                errors.Add($"Consumable item '{id}' must use slot 'none'.");
            }

            if (!IsAllowedValue(item.Slot, "none", "main_hand", "off_hand", "head", "body", "feet", "ring", "amulet"))
            {
                errors.Add($"Item '{id}' has unknown slot '{item.Slot}'.");
            }

            if (item.Stackable && item.MaxStack is null or <= 1)
            {
                errors.Add($"Stackable item '{id}' must declare max_stack greater than 1.");
            }

            if (!item.Stackable && item.MaxStack is > 1)
            {
                errors.Add($"Non-stackable item '{id}' cannot declare max_stack greater than 1.");
            }

            foreach (var (requirement, value) in item.Requirements)
            {
                if (value < 0)
                {
                    errors.Add($"Item '{id}' requirement '{requirement}' cannot be negative.");
                }
            }

            foreach (var effect in item.Effects)
            {
                switch (effect.Type)
                {
                    case "on_hit":
                        if (string.IsNullOrWhiteSpace(effect.StatusEffect) || !statusEffects.ContainsKey(effect.StatusEffect))
                        {
                            errors.Add($"Item '{id}' on_hit effect references unknown status '{effect.StatusEffect}'.");
                        }

                        if (effect.Chance is null or < 1 or > 100)
                        {
                            errors.Add($"Item '{id}' on_hit effect must declare chance between 1 and 100.");
                        }

                        if (effect.Duration is null or <= 0)
                        {
                            errors.Add($"Item '{id}' on_hit effect must declare a positive duration.");
                        }
                        break;

                    case "on_use":
                        if (!IsAllowedValue(effect.Action, "heal", "apply_status", "cast_ability"))
                        {
                            errors.Add($"Item '{id}' on_use effect has invalid action '{effect.Action}'.");
                        }

                        if (!IsAllowedValue(effect.Target, "self", "aimed"))
                        {
                            errors.Add($"Item '{id}' on_use effect has invalid target '{effect.Target}'.");
                        }

                        if (string.Equals(effect.Action, "heal", StringComparison.Ordinal) && effect.Value is null or <= 0)
                        {
                            errors.Add($"Item '{id}' heal effect must declare a positive value.");
                        }

                        if (string.Equals(effect.Action, "apply_status", StringComparison.Ordinal)
                            && (string.IsNullOrWhiteSpace(effect.StatusEffect) || !statusEffects.ContainsKey(effect.StatusEffect)))
                        {
                            errors.Add($"Item '{id}' apply_status effect references unknown status '{effect.StatusEffect}'.");
                        }

                        if (string.Equals(effect.Action, "cast_ability", StringComparison.Ordinal)
                            && (string.IsNullOrWhiteSpace(effect.AbilityId) || !abilities.ContainsKey(effect.AbilityId)))
                        {
                            errors.Add($"Item '{id}' cast_ability effect references unknown ability '{effect.AbilityId}'.");
                        }
                        break;

                    case "passive":
                        if (!IsAllowedValue(effect.Action, "regen_hp", "modify_stat"))
                        {
                            errors.Add($"Item '{id}' passive effect has invalid action '{effect.Action}'.");
                        }

                        if (effect.Value is null)
                        {
                            errors.Add($"Item '{id}' passive effect must declare a value.");
                        }

                        if (string.Equals(effect.Action, "modify_stat", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(effect.Stat))
                        {
                            errors.Add($"Item '{id}' modify_stat passive must declare a stat.");
                        }
                        break;

                    default:
                        errors.Add($"Item '{id}' has unknown effect type '{effect.Type}'.");
                        break;
                }
            }
        }
    }

    private static void ValidatePerks(
        IReadOnlyDictionary<string, PerkDefinition> perks,
        ICollection<string> errors)
    {
        foreach (var (id, perk) in perks)
        {
            ValidateStableId(id, "Perk", errors);
            ValidateRequiredText(perk.Name, $"Perk '{id}' name", errors);
            ValidateRequiredText(perk.Description, $"Perk '{id}' description", errors);

            if (perk.UnlockLevel < 2)
            {
                errors.Add($"Perk '{id}' must unlock at level 2 or later.");
            }

            if (perk.Effects.Count == 0)
            {
                errors.Add($"Perk '{id}' must define at least one effect.");
            }

            foreach (var effect in perk.Effects)
            {
                ValidateRequiredText(effect.Type, $"Perk '{id}' effect type", errors);
                if (!IsAllowedValue(effect.Type, "stat_bonus", "shop_discount_percent"))
                {
                    errors.Add($"Perk '{id}' has unsupported effect type '{effect.Type}'.");
                    continue;
                }

                if (effect.Value == 0)
                {
                    errors.Add($"Perk '{id}' has an effect with zero value.");
                }

                if (string.Equals(effect.Type, "stat_bonus", StringComparison.Ordinal))
                {
                    ValidateRequiredText(effect.Stat ?? string.Empty, $"Perk '{id}' stat bonus target", errors);
                }
            }
        }
    }

    private static void ValidateEnemies(
        IReadOnlyDictionary<string, EnemyDefinition> enemies,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        IReadOnlyDictionary<string, LootTableDefinition> lootTables,
        ICollection<string> errors)
    {
        foreach (var (id, enemy) in enemies)
        {
            ValidateStableId(id, "Enemy", errors);
            ValidateRequiredText(enemy.Name, $"Enemy '{id}' name", errors);
            ValidateRequiredText(enemy.Description, $"Enemy '{id}' description", errors);
            ValidateRequiredText(enemy.AiType, $"Enemy '{id}' ai_type", errors);
            ValidateRequiredText(enemy.Faction, $"Enemy '{id}' faction", errors);
            ValidateRequiredText(enemy.SpritePath, $"Enemy '{id}' sprite_path", errors);

            if (enemy.Stats.HP <= 0)
            {
                errors.Add($"Enemy '{id}' must have positive hp.");
            }

            if (enemy.Stats.Attack <= 0)
            {
                errors.Add($"Enemy '{id}' must have positive attack.");
            }

            if (enemy.Stats.Defense < 0)
            {
                errors.Add($"Enemy '{id}' cannot have negative defense.");
            }

            if (enemy.Stats.Accuracy is < 0 or > 100)
            {
                errors.Add($"Enemy '{id}' accuracy must be between 0 and 100.");
            }

            if (enemy.Stats.Evasion is < 0 or > 100)
            {
                errors.Add($"Enemy '{id}' evasion must be between 0 and 100.");
            }

            if (enemy.Stats.Speed <= 0)
            {
                errors.Add($"Enemy '{id}' speed must be positive.");
            }

            if (enemy.Stats.FovRange <= 0)
            {
                errors.Add($"Enemy '{id}' fov_range must be positive.");
            }

            if (enemy.MinDepth > enemy.MaxDepth)
            {
                errors.Add($"Enemy '{id}' min_depth cannot exceed max_depth.");
            }

            if (enemy.SpawnWeight <= 0)
            {
                errors.Add($"Enemy '{id}' must have positive spawn_weight.");
            }

            if (enemy.SpriteAtlasCoords.Count != 2)
            {
                errors.Add($"Enemy '{id}' must define exactly two sprite atlas coordinates.");
            }

            if (!Enum.TryParse<Faction>(enemy.Faction, ignoreCase: true, out _))
            {
                errors.Add($"Enemy '{id}' has unknown faction '{enemy.Faction}'.");
            }

            if (!IsAllowedValue(enemy.AiType, "melee_rush", "ranged_kite", "ambush", "support", "patrol"))
            {
                errors.Add($"Enemy '{id}' has unknown ai_type '{enemy.AiType}'.");
            }

            foreach (var ability in enemy.Abilities)
            {
                if (string.IsNullOrWhiteSpace(ability.AbilityId) || !abilities.ContainsKey(ability.AbilityId))
                {
                    errors.Add($"Enemy '{id}' references unknown ability '{ability.AbilityId}'.");
                }

                if (ability.Cooldown < 0)
                {
                    errors.Add($"Enemy '{id}' ability '{ability.AbilityId}' cannot have negative cooldown.");
                }

                if (ability.Priority <= 0)
                {
                    errors.Add($"Enemy '{id}' ability '{ability.AbilityId}' must have positive priority.");
                }
            }

            if (!string.IsNullOrWhiteSpace(enemy.LootTableId) && !lootTables.ContainsKey(enemy.LootTableId))
            {
                errors.Add($"Enemy '{id}' references unknown loot table '{enemy.LootTableId}'.");
            }
        }
    }

    private static void ValidateAbilities(
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        IReadOnlyDictionary<string, StatusEffectDefinition> statusEffects,
        ICollection<string> errors)
    {
        foreach (var (id, ability) in abilities)
        {
            ValidateStableId(id, "Ability", errors);
            ValidateRequiredText(ability.Name, $"Ability '{id}' name", errors);
            ValidateRequiredText(ability.Description, $"Ability '{id}' description", errors);
            ValidateRequiredText(ability.Targeting.Type, $"Ability '{id}' targeting.type", errors);
            ValidateRequiredText(ability.Animation, $"Ability '{id}' animation", errors);
            ValidateRequiredText(ability.Sfx, $"Ability '{id}' sfx", errors);

            if (!IsAllowedValue(ability.Targeting.Type, "self", "single", "tile", "aoe_circle", "aoe_line", "aoe_cone"))
            {
                errors.Add($"Ability '{id}' has unknown targeting type '{ability.Targeting.Type}'.");
            }

            if (ability.Costs.Energy <= 0)
            {
                errors.Add($"Ability '{id}' must have positive energy cost.");
            }

            if (ability.Effects.Count == 0)
            {
                errors.Add($"Ability '{id}' must declare at least one effect.");
            }

            foreach (var effect in ability.Effects)
            {
                switch (effect.Type)
                {
                    case "damage":
                        if (!TryParseDamageType(effect.DamageType, out _))
                        {
                            errors.Add($"Ability '{id}' damage effect has invalid damage_type '{effect.DamageType}'.");
                        }

                        if (effect.BaseValue is null or < 0)
                        {
                            errors.Add($"Ability '{id}' damage effect must declare a non-negative base_value.");
                        }

                        if (effect.StatScaling is not null && string.IsNullOrWhiteSpace(effect.StatScaling.Stat))
                        {
                            errors.Add($"Ability '{id}' damage effect stat_scaling must declare a stat.");
                        }
                        break;

                    case "apply_status":
                        if (string.IsNullOrWhiteSpace(effect.StatusEffect) || !statusEffects.ContainsKey(effect.StatusEffect))
                        {
                            errors.Add($"Ability '{id}' apply_status effect references unknown status '{effect.StatusEffect}'.");
                        }

                        if (effect.Chance is null or < 1 or > 100)
                        {
                            errors.Add($"Ability '{id}' apply_status effect must declare chance between 1 and 100.");
                        }

                        if (effect.Duration is null or <= 0)
                        {
                            errors.Add($"Ability '{id}' apply_status effect must declare a positive duration.");
                        }
                        break;

                    case "heal_self":
                        if (!IsAllowedValue(effect.ValueSource, "flat", "damage_dealt"))
                        {
                            errors.Add($"Ability '{id}' heal_self effect has invalid value_source '{effect.ValueSource}'.");
                        }

                        if (effect.Factor is null or < 0)
                        {
                            errors.Add($"Ability '{id}' heal_self effect must declare a non-negative factor.");
                        }
                        break;

                    case "teleport":
                        if (!IsAllowedValue(effect.Destination, "target_tile"))
                        {
                            errors.Add($"Ability '{id}' teleport effect has invalid destination '{effect.Destination}'.");
                        }
                        break;

                    default:
                        errors.Add($"Ability '{id}' has unknown effect type '{effect.Type}'.");
                        break;
                }
            }
        }
    }

    private static void ValidateDialogs(
        IReadOnlyDictionary<string, DialogueDefinition> dialogs,
        ICollection<string> errors)
    {
        foreach (var (id, dialog) in dialogs)
        {
            ValidateStableId(id, "Dialog", errors);
            ValidateRequiredText(dialog.StartNode, $"Dialog '{id}' start_node", errors);

            if (dialog.Nodes.Count == 0)
            {
                errors.Add($"Dialog '{id}' must define at least one node.");
                continue;
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in dialog.Nodes)
            {
                if (!nodeIds.Add(node.Id))
                {
                    errors.Add($"Dialog '{id}' contains duplicate node id '{node.Id}'.");
                }

                ValidateRequiredText(node.Id, $"Dialog '{id}' node id", errors);
                ValidateRequiredText(node.Text, $"Dialog '{id}' node '{node.Id}' text", errors);

                if (node.Options.Count == 0)
                {
                    errors.Add($"Dialog '{id}' node '{node.Id}' must define at least one option.");
                }

                foreach (var option in node.Options)
                {
                    ValidateRequiredText(option.Text, $"Dialog '{id}' node '{node.Id}' option text", errors);

                    if (!string.IsNullOrWhiteSpace(option.Action) && !IsAllowedValue(option.Action, "close", "shop"))
                    {
                        errors.Add($"Dialog '{id}' node '{node.Id}' option has unknown action '{option.Action}'.");
                    }
                }
            }

            if (!nodeIds.Contains(dialog.StartNode))
            {
                errors.Add($"Dialog '{id}' start_node '{dialog.StartNode}' does not exist.");
            }

            foreach (var node in dialog.Nodes)
            {
                foreach (var option in node.Options)
                {
                    if (!string.IsNullOrWhiteSpace(option.Next) && !nodeIds.Contains(option.Next))
                    {
                        errors.Add($"Dialog '{id}' node '{node.Id}' option points to missing node '{option.Next}'.");
                    }
                }
            }
        }
    }

    private static void ValidateNpcs(
        IReadOnlyDictionary<string, NpcDefinition> npcs,
        IReadOnlyDictionary<string, DialogueDefinition> dialogs,
        IReadOnlyDictionary<string, ItemDefinition> items,
        ICollection<string> errors)
    {
        foreach (var (id, npc) in npcs)
        {
            ValidateStableId(id, "Npc", errors);
            ValidateRequiredText(npc.Name, $"Npc '{id}' name", errors);
            ValidateRequiredText(npc.Description, $"Npc '{id}' description", errors);
            ValidateRequiredText(npc.Role, $"Npc '{id}' role", errors);
            ValidateRequiredText(npc.DialogueId, $"Npc '{id}' dialogue_id", errors);

            if (npc.MaxDepth >= 0 && npc.MinDepth > npc.MaxDepth)
            {
                errors.Add($"Npc '{id}' min_depth cannot exceed max_depth.");
            }

            if (!dialogs.ContainsKey(npc.DialogueId))
            {
                errors.Add($"Npc '{id}' references unknown dialog '{npc.DialogueId}'.");
            }

            foreach (var stock in npc.Stock)
            {
                if (string.IsNullOrWhiteSpace(stock.ItemId) || !items.ContainsKey(stock.ItemId))
                {
                    errors.Add($"Npc '{id}' stock references unknown item '{stock.ItemId}'.");
                }

                if (stock.Price <= 0)
                {
                    errors.Add($"Npc '{id}' stock item '{stock.ItemId}' must have a positive price.");
                }

                if (stock.Quantity <= 0)
                {
                    errors.Add($"Npc '{id}' stock item '{stock.ItemId}' must have a positive quantity.");
                }
            }
        }
    }

    private static void ValidateStatusEffects(
        IReadOnlyDictionary<string, StatusEffectDefinition> statusEffects,
        ICollection<string> errors)
    {
        foreach (var (id, effect) in statusEffects)
        {
            ValidateStableId(id, "Status effect", errors);
            ValidateRequiredText(effect.Name, $"Status effect '{id}' name", errors);
            ValidateRequiredText(effect.Description, $"Status effect '{id}' description", errors);
            ValidateRequiredText(effect.IconPath, $"Status effect '{id}' icon_path", errors);
            ValidateRequiredText(effect.DurationType, $"Status effect '{id}' duration_type", errors);
            ValidateRequiredText(effect.TickTiming, $"Status effect '{id}' tick_timing", errors);

            if (effect.DefaultDuration < 0)
            {
                errors.Add($"Status effect '{id}' default_duration cannot be negative.");
            }

            if (effect.Stackable && effect.MaxStacks is null or <= 1)
            {
                errors.Add($"Stackable status effect '{id}' must declare max_stacks greater than 1.");
            }

            if (!IsAllowedValue(effect.DurationType, "turns"))
            {
                errors.Add($"Status effect '{id}' has invalid duration_type '{effect.DurationType}'.");
            }

            if (!IsAllowedValue(effect.TickTiming, "none", "start_of_turn", "end_of_turn"))
            {
                errors.Add($"Status effect '{id}' has invalid tick_timing '{effect.TickTiming}'.");
            }

            if (!Regex.IsMatch(effect.ColorTint, "^#[0-9A-Fa-f]{8}$"))
            {
                errors.Add($"Status effect '{id}' color_tint must be an 8-digit RGBA hex string.");
            }

            foreach (var tickEffect in effect.TickEffects)
            {
                if (!string.Equals(tickEffect.Type, "damage", StringComparison.Ordinal))
                {
                    errors.Add($"Status effect '{id}' tick effect type '{tickEffect.Type}' is unsupported.");
                }

                if (!TryParseDamageType(tickEffect.DamageType, out _))
                {
                    errors.Add($"Status effect '{id}' tick effect has invalid damage_type '{tickEffect.DamageType}'.");
                }
            }

            foreach (var modifier in effect.StatModifiers)
            {
                if (string.IsNullOrWhiteSpace(modifier.Stat))
                {
                    errors.Add($"Status effect '{id}' has a stat modifier without a stat.");
                }

                if (!IsAllowedValue(modifier.Operation, "add", "multiply", "set"))
                {
                    errors.Add($"Status effect '{id}' has invalid stat modifier operation '{modifier.Operation}'.");
                }
            }

            foreach (var flag in effect.Flags)
            {
                if (!IsAllowedValue(flag, "skip_turn", "phase_through_walls", "immune_physical"))
                {
                    errors.Add($"Status effect '{id}' has invalid flag '{flag}'.");
                }
            }

            ValidateLifecycleEffects(id, effect.OnApplyEffects, statusEffects, errors);
            ValidateLifecycleEffects(id, effect.OnExpireEffects, statusEffects, errors);
        }
    }

    private static void ValidateLifecycleEffects(
        string parentId,
        IEnumerable<StatusLifecycleEffectDefinition> lifecycleEffects,
        IReadOnlyDictionary<string, StatusEffectDefinition> statusEffects,
        ICollection<string> errors)
    {
        foreach (var effect in lifecycleEffects)
        {
            if (!string.Equals(effect.Type, "remove_status", StringComparison.Ordinal))
            {
                errors.Add($"Status effect '{parentId}' lifecycle effect '{effect.Type}' is unsupported.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(effect.StatusId) || !statusEffects.ContainsKey(effect.StatusId))
            {
                errors.Add($"Status effect '{parentId}' lifecycle effect references unknown status '{effect.StatusId}'.");
            }
        }
    }

    private static void ValidateRooms(
        IReadOnlyDictionary<string, RoomPrefabDefinition> rooms,
        IReadOnlyDictionary<string, EnemyDefinition> enemies,
        IReadOnlyDictionary<string, ItemDefinition> items,
        IReadOnlyDictionary<string, NpcDefinition> npcs,
        IReadOnlyDictionary<string, LootTableDefinition> lootTables,
        IReadOnlyDictionary<string, string> tileLegend,
        ICollection<string> errors)
    {
        if (!tileLegend.ContainsKey("#") || !tileLegend.ContainsKey("."))
        {
            errors.Add("room_prefabs.json tile_legend must define at least '#' and '.'.");
        }

        foreach (var (id, room) in rooms)
        {
            ValidateStableId(id, "Room prefab", errors);
            ValidateRequiredText(room.Name, $"Room '{id}' name", errors);

            if (room.Width <= 0 || room.Height <= 0)
            {
                errors.Add($"Room '{id}' must have positive width and height.");
            }

            if (room.MinDepth > room.MaxDepth)
            {
                errors.Add($"Room '{id}' min_depth cannot exceed max_depth.");
            }

            if (room.Layout.Count != room.Height)
            {
                errors.Add($"Room '{id}' layout height does not match declared height.");
            }

            for (var y = 0; y < room.Layout.Count; y++)
            {
                var row = room.Layout[y];
                if (row.Length != room.Width)
                {
                    errors.Add($"Room '{id}' row {y} length does not match declared width.");
                }

                foreach (var symbol in row)
                {
                    if (!tileLegend.ContainsKey(symbol.ToString(CultureInfo.InvariantCulture)))
                    {
                        errors.Add($"Room '{id}' uses undefined tile legend symbol '{symbol}'.");
                    }
                }
            }

            foreach (var door in room.Doors)
            {
                if (!IsWithin(room, door.X, door.Y))
                {
                    errors.Add($"Room '{id}' door at ({door.X},{door.Y}) is out of bounds.");
                    continue;
                }

                if (room.Layout[door.Y][door.X] != '+')
                {
                    errors.Add($"Room '{id}' door at ({door.X},{door.Y}) must sit on '+'.");
                }
            }

            foreach (var spawnPoint in room.SpawnPoints)
            {
                if (!IsWithin(room, spawnPoint.X, spawnPoint.Y))
                {
                    errors.Add($"Room '{id}' spawn point at ({spawnPoint.X},{spawnPoint.Y}) is out of bounds.");
                }
            }

            foreach (var fixedEntity in room.FixedEntities)
            {
                if (!IsWithin(room, fixedEntity.X, fixedEntity.Y))
                {
                    errors.Add($"Room '{id}' fixed entity at ({fixedEntity.X},{fixedEntity.Y}) is out of bounds.");
                }

                var entityType = fixedEntity.EntityType?.Trim().ToLowerInvariant() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(entityType) && !string.IsNullOrWhiteSpace(fixedEntity.TemplateId))
                {
                    if (items.ContainsKey(fixedEntity.TemplateId))
                    {
                        entityType = "item";
                    }
                    else if (enemies.ContainsKey(fixedEntity.TemplateId))
                    {
                        entityType = "enemy";
                    }
                    else if (npcs.ContainsKey(fixedEntity.TemplateId))
                    {
                        entityType = "npc";
                    }
                }

                switch (entityType)
                {
                    case "":
                        if (!string.IsNullOrWhiteSpace(fixedEntity.TemplateId))
                        {
                            errors.Add($"Room '{id}' fixed entity references unknown template '{fixedEntity.TemplateId}'.");
                        }
                        break;
                    case "item" when string.IsNullOrWhiteSpace(fixedEntity.TemplateId) || !items.ContainsKey(fixedEntity.TemplateId):
                        errors.Add($"Room '{id}' fixed item references unknown template '{fixedEntity.TemplateId}'.");
                        break;
                    case "enemy" when string.IsNullOrWhiteSpace(fixedEntity.TemplateId) || !enemies.ContainsKey(fixedEntity.TemplateId):
                        errors.Add($"Room '{id}' fixed enemy references unknown template '{fixedEntity.TemplateId}'.");
                        break;
                    case "npc" when string.IsNullOrWhiteSpace(fixedEntity.TemplateId) || !npcs.ContainsKey(fixedEntity.TemplateId):
                        errors.Add($"Room '{id}' fixed npc references unknown template '{fixedEntity.TemplateId}'.");
                        break;
                    case "chest" when !string.IsNullOrWhiteSpace(fixedEntity.TemplateId) && !lootTables.ContainsKey(fixedEntity.TemplateId):
                        errors.Add($"Room '{id}' fixed chest references unknown loot table '{fixedEntity.TemplateId}'.");
                        break;
                    case "chest":
                        break;
                    default:
                        errors.Add($"Room '{id}' fixed entity type '{fixedEntity.EntityType}' is unsupported.");
                        break;
                }
            }
        }
    }

    private static void ValidateLootTables(
        IReadOnlyDictionary<string, LootTableDefinition> lootTables,
        IReadOnlyDictionary<string, ItemDefinition> items,
        ICollection<string> errors)
    {
        foreach (var (id, table) in lootTables)
        {
            ValidateStableId(id, "Loot table", errors);

            if (table.Rolls <= 0)
            {
                errors.Add($"Loot table '{id}' must have positive rolls.");
            }

            if (table.Entries.Count == 0)
            {
                errors.Add($"Loot table '{id}' must have at least one entry.");
            }

            foreach (var entry in table.Entries)
            {
                if (entry.Weight <= 0)
                {
                    errors.Add($"Loot table '{id}' has a non-positive entry weight.");
                }

                if (entry.CountMin < 0 || entry.CountMax < 0 || entry.CountMin > entry.CountMax)
                {
                    errors.Add($"Loot table '{id}' has invalid count range {entry.CountMin}-{entry.CountMax}.");
                }

                if (entry.ItemId is not null && !items.ContainsKey(entry.ItemId))
                {
                    errors.Add($"Loot table '{id}' references unknown item '{entry.ItemId}'.");
                }

                if (entry.ItemId is null && (entry.CountMin != 0 || entry.CountMax != 0))
                {
                    errors.Add($"Loot table '{id}' null-drop entry must use a zero count range.");
                }
            }
        }
    }

    private static void ValidateStableId(string id, string kind, ICollection<string> errors)
    {
        if (!StableIdPattern.IsMatch(id))
        {
            errors.Add($"{kind} id '{id}' must be stable snake_case.");
        }
    }

    private static void ValidateRequiredText(string value, string label, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
        }
    }

    private static bool IsAllowedValue(string? candidate, params string[] allowedValues)
    {
        return candidate is not null && allowedValues.Contains(candidate, StringComparer.Ordinal);
    }

    private static bool TryParseDamageType(string? damageType, out DamageType parsed)
    {
        parsed = DamageType.Physical;
        if (string.IsNullOrWhiteSpace(damageType))
        {
            return false;
        }

        return damageType switch
        {
            "physical" => (parsed = DamageType.Physical) == DamageType.Physical,
            "fire" => (parsed = DamageType.Fire) == DamageType.Fire,
            "cold" => (parsed = DamageType.Cold) == DamageType.Cold,
            "ice" => (parsed = DamageType.Cold) == DamageType.Cold,
            "poison" => (parsed = DamageType.Poison) == DamageType.Poison,
            "lightning" => (parsed = DamageType.Lightning) == DamageType.Lightning,
            "holy" => (parsed = DamageType.Holy) == DamageType.Holy,
            "dark" => (parsed = DamageType.Dark) == DamageType.Dark,
            _ => false,
        };
    }

    private static bool IsWithin(RoomPrefabDefinition room, int x, int y)
    {
        return x >= 0 && y >= 0 && x < room.Width && y < room.Height;
    }
}