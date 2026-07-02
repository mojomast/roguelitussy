using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class AscensionModifier
{
    [JsonPropertyName("modifier_id")]
    public string ModifierId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("ascension_level")]
    public int AscensionLevel { get; set; }

    [JsonPropertyName("effect_type")]
    public string EffectType { get; set; } = string.Empty;

    [JsonPropertyName("effect_value")]
    public float EffectValue { get; set; }
}
