using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class RelicTemplate
{
    [JsonPropertyName("relic_id")]
    public string RelicId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "common";

    [JsonPropertyName("trigger_hook")]
    public string TriggerHook { get; set; } = string.Empty;

    [JsonPropertyName("effect_type")]
    public string EffectType { get; set; } = string.Empty;

    [JsonPropertyName("effect_value")]
    public int EffectValue { get; set; }

    [JsonPropertyName("condition_tag")]
    public string? ConditionTag { get; set; }

    [JsonPropertyName("is_unique")]
    public bool IsUnique { get; set; } = true;
}
