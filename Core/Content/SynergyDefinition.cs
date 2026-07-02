using System;
using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class SynergyDefinition
{
    [JsonPropertyName("synergy_id")]
    public string SynergyId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("required_relic_ids")]
    public string[] RequiredRelicIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("required_perk_ids")]
    public string[] RequiredPerkIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("required_item_tags")]
    public string[] RequiredItemTags { get; set; } = Array.Empty<string>();

    [JsonPropertyName("bonus_effect_type")]
    public string BonusEffectType { get; set; } = string.Empty;

    [JsonPropertyName("bonus_value")]
    public int BonusValue { get; set; }

    [JsonPropertyName("archetype_restriction")]
    public string ArchetypeRestriction { get; set; } = string.Empty;
}
