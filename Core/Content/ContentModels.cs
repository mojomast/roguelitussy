using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class ItemsDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("items")]
    public List<ItemDefinition> Items { get; set; } = new();
}

public sealed class EnemiesDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("enemies")]
    public List<EnemyDefinition> Enemies { get; set; } = new();
}

public sealed class AbilitiesDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("abilities")]
    public List<AbilityDefinition> Abilities { get; set; } = new();
}

public sealed class PerksDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("perks")]
    public List<PerkDefinition> Perks { get; set; } = new();
}

public sealed class StatusEffectsDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("status_effects")]
    public List<StatusEffectDefinition> StatusEffects { get; set; } = new();
}

public sealed class RoomPrefabsDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("tile_legend")]
    public Dictionary<string, string> TileLegend { get; set; } = new();

    [JsonPropertyName("rooms")]
    public List<RoomPrefabDefinition> Rooms { get; set; } = new();
}

public sealed class LootTablesDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("loot_tables")]
    public List<LootTableDefinition> LootTables { get; set; } = new();
}

public sealed class DialogsDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("dialogs")]
    public List<DialogueDefinition> Dialogs { get; set; } = new();
}

public sealed class NpcsDocument
{
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("npcs")]
    public List<NpcDefinition> Npcs { get; set; } = new();
}

public sealed class ItemDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("slot")]
    public string Slot { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public Dictionary<string, int> Stats { get; set; } = new();

    [JsonPropertyName("effects")]
    public List<ItemEffectDefinition> Effects { get; set; } = new();

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("requirements")]
    public Dictionary<string, int> Requirements { get; set; } = new();

    [JsonPropertyName("stackable")]
    public bool Stackable { get; set; }

    [JsonPropertyName("max_stack")]
    public int? MaxStack { get; set; }

    [JsonPropertyName("sprite_path")]
    public string SpritePath { get; set; } = string.Empty;

    [JsonPropertyName("sprite_atlas_coords")]
    public List<int> SpriteAtlasCoords { get; set; } = new();
}

public sealed class ItemEffectDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("status_effect")]
    public string? StatusEffect { get; set; }

    [JsonPropertyName("chance")]
    public int? Chance { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("value")]
    public int? Value { get; set; }

    [JsonPropertyName("ability_id")]
    public string? AbilityId { get; set; }

    [JsonPropertyName("target")]
    public string? Target { get; set; }

    [JsonPropertyName("stat")]
    public string? Stat { get; set; }

    [JsonPropertyName("per")]
    public string? Per { get; set; }
}

public sealed class EnemyDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public EnemyStatsDefinition Stats { get; set; } = new();

    [JsonPropertyName("ai_type")]
    public string AiType { get; set; } = string.Empty;

    [JsonPropertyName("ai_params")]
    public Dictionary<string, JsonElement> AiParams { get; set; } = new();

    [JsonPropertyName("faction")]
    public string Faction { get; set; } = string.Empty;

    [JsonPropertyName("min_depth")]
    public int MinDepth { get; set; }

    [JsonPropertyName("max_depth")]
    public int MaxDepth { get; set; }

    [JsonPropertyName("spawn_weight")]
    public int SpawnWeight { get; set; }

    [JsonPropertyName("abilities")]
    public List<EnemyAbilityReference> Abilities { get; set; } = new();

    [JsonPropertyName("loot_table_id")]
    public string? LootTableId { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("sprite_path")]
    public string SpritePath { get; set; } = string.Empty;

    [JsonPropertyName("sprite_atlas_coords")]
    public List<int> SpriteAtlasCoords { get; set; } = new();
}

public sealed class EnemyStatsDefinition
{
    [JsonPropertyName("hp")]
    public int HP { get; set; }

    [JsonPropertyName("attack")]
    public int Attack { get; set; }

    [JsonPropertyName("defense")]
    public int Defense { get; set; }

    [JsonPropertyName("accuracy")]
    public int Accuracy { get; set; }

    [JsonPropertyName("evasion")]
    public int Evasion { get; set; }

    [JsonPropertyName("speed")]
    public int Speed { get; set; }

    [JsonPropertyName("fov_range")]
    public int FovRange { get; set; }

    [JsonPropertyName("xp_value")]
    public int XpValue { get; set; }
}

public sealed class EnemyAbilityReference
{
    [JsonPropertyName("ability_id")]
    public string AbilityId { get; set; } = string.Empty;

    [JsonPropertyName("cooldown")]
    public int Cooldown { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}

public sealed class DialogueDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("start_node")]
    public string StartNode { get; set; } = string.Empty;

    [JsonPropertyName("nodes")]
    public List<DialogueNodeDefinition> Nodes { get; set; } = new();
}

public sealed class DialogueNodeDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("options")]
    public List<DialogueOptionDefinition> Options { get; set; } = new();
}

public sealed class DialogueOptionDefinition
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }
}

public sealed class NpcDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("min_depth")]
    public int MinDepth { get; set; }

    [JsonPropertyName("max_depth")]
    public int MaxDepth { get; set; }

    [JsonPropertyName("dialogue_id")]
    public string DialogueId { get; set; } = string.Empty;

    [JsonPropertyName("race_id")]
    public string RaceId { get; set; } = string.Empty;

    [JsonPropertyName("gender_id")]
    public string GenderId { get; set; } = string.Empty;

    [JsonPropertyName("appearance_id")]
    public string AppearanceId { get; set; } = string.Empty;

    [JsonPropertyName("archetype_id")]
    public string ArchetypeId { get; set; } = string.Empty;

    [JsonPropertyName("stock")]
    public List<MerchantStockDefinition> Stock { get; set; } = new();
}

public sealed class MerchantStockDefinition
{
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public int Price { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public sealed class PerkDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("unlock_level")]
    public int UnlockLevel { get; set; }

    [JsonPropertyName("effects")]
    public List<PerkEffectDefinition> Effects { get; set; } = new();
}

public sealed class PerkEffectDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("stat")]
    public string? Stat { get; set; }

    [JsonPropertyName("value")]
    public int Value { get; set; }
}

public sealed class AbilityDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("targeting")]
    public AbilityTargetingDefinition Targeting { get; set; } = new();

    [JsonPropertyName("costs")]
    public AbilityCostDefinition Costs { get; set; } = new();

    [JsonPropertyName("effects")]
    public List<AbilityEffectDefinition> Effects { get; set; } = new();

    [JsonPropertyName("animation")]
    public string Animation { get; set; } = string.Empty;

    [JsonPropertyName("sfx")]
    public string Sfx { get; set; } = string.Empty;
}

public sealed class AbilityTargetingDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("range")]
    public int Range { get; set; }

    [JsonPropertyName("radius")]
    public int? Radius { get; set; }

    [JsonPropertyName("requires_los")]
    public bool RequiresLos { get; set; }

    [JsonPropertyName("requires_walkable")]
    public bool RequiresWalkable { get; set; }

    [JsonPropertyName("hits_allies")]
    public bool? HitsAllies { get; set; }

    [JsonPropertyName("center")]
    public string? Center { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("arc")]
    public int? Arc { get; set; }
}

public sealed class AbilityCostDefinition
{
    [JsonPropertyName("energy")]
    public int Energy { get; set; }

    [JsonPropertyName("mp")]
    public int? MP { get; set; }
}

public sealed class AbilityEffectDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("damage_type")]
    public string? DamageType { get; set; }

    [JsonPropertyName("base_value")]
    public int? BaseValue { get; set; }

    [JsonPropertyName("stat_scaling")]
    public AbilityStatScalingDefinition? StatScaling { get; set; }

    [JsonPropertyName("status_effect")]
    public string? StatusEffect { get; set; }

    [JsonPropertyName("chance")]
    public int? Chance { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }

    [JsonPropertyName("filter")]
    public string? Filter { get; set; }

    [JsonPropertyName("value_source")]
    public string? ValueSource { get; set; }

    [JsonPropertyName("factor")]
    public double? Factor { get; set; }

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }
}

public sealed class AbilityStatScalingDefinition
{
    [JsonPropertyName("stat")]
    public string Stat { get; set; } = string.Empty;

    [JsonPropertyName("factor")]
    public double Factor { get; set; }
}

public sealed class StatusEffectDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon_path")]
    public string IconPath { get; set; } = string.Empty;

    [JsonPropertyName("duration_type")]
    public string DurationType { get; set; } = string.Empty;

    [JsonPropertyName("default_duration")]
    public int DefaultDuration { get; set; }

    [JsonPropertyName("stackable")]
    public bool Stackable { get; set; }

    [JsonPropertyName("max_stacks")]
    public int? MaxStacks { get; set; }

    [JsonPropertyName("refreshable")]
    public bool Refreshable { get; set; }

    [JsonPropertyName("tick_timing")]
    public string TickTiming { get; set; } = string.Empty;

    [JsonPropertyName("tick_effects")]
    public List<StatusEffectTickEffectDefinition> TickEffects { get; set; } = new();

    [JsonPropertyName("stat_modifiers")]
    public List<StatusStatModifierDefinition> StatModifiers { get; set; } = new();

    [JsonPropertyName("flags")]
    public List<string> Flags { get; set; } = new();

    [JsonPropertyName("on_apply_effects")]
    public List<StatusLifecycleEffectDefinition> OnApplyEffects { get; set; } = new();

    [JsonPropertyName("on_expire_effects")]
    public List<StatusLifecycleEffectDefinition> OnExpireEffects { get; set; } = new();

    [JsonPropertyName("color_tint")]
    public string ColorTint { get; set; } = string.Empty;
}

public sealed class StatusEffectTickEffectDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("damage_type")]
    public string? DamageType { get; set; }

    [JsonPropertyName("value")]
    public int Value { get; set; }
}

public sealed class StatusStatModifierDefinition
{
    [JsonPropertyName("stat")]
    public string Stat { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public double Value { get; set; }
}

public sealed class StatusLifecycleEffectDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status_id")]
    public string? StatusId { get; set; }
}

public sealed class RoomPrefabDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("min_depth")]
    public int MinDepth { get; set; }

    [JsonPropertyName("max_depth")]
    public int MaxDepth { get; set; }

    [JsonPropertyName("layout")]
    public List<string> Layout { get; set; } = new();

    [JsonPropertyName("doors")]
    public List<RoomDoorDefinition> Doors { get; set; } = new();

    [JsonPropertyName("spawn_points")]
    public List<RoomSpawnPointDefinition> SpawnPoints { get; set; } = new();

    [JsonPropertyName("fixed_entities")]
    public List<FixedEntityDefinition> FixedEntities { get; set; } = new();

    [JsonPropertyName("item_quality_bonus")]
    public int? ItemQualityBonus { get; set; }

    [JsonPropertyName("enemy_count_bonus")]
    public int? EnemyCountBonus { get; set; }

    [JsonPropertyName("lock_doors_on_enter")]
    public bool LockDoorsOnEnter { get; set; }
}

public sealed class RoomDoorDefinition
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;
}

public sealed class RoomSpawnPointDefinition
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("trap_id")]
    public string? TrapId { get; set; }
}

public sealed class FixedEntityDefinition
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("entity_type")]
    public string? EntityType { get; set; }

    [JsonPropertyName("template_id")]
    public string? TemplateId { get; set; }
}

public sealed class LootTableDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("rolls")]
    public int Rolls { get; set; }

    [JsonPropertyName("entries")]
    public List<LootEntryDefinition> Entries { get; set; } = new();
}

public sealed class LootEntryDefinition
{
    [JsonPropertyName("item_id")]
    public string? ItemId { get; set; }

    [JsonPropertyName("weight")]
    public int Weight { get; set; }

    [JsonPropertyName("count_min")]
    public int CountMin { get; set; }

    [JsonPropertyName("count_max")]
    public int CountMax { get; set; }
}