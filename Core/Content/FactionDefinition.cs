using System;
using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class FactionDefinition
{
    [JsonPropertyName("faction_id")]
    public string FactionId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("hostile_threshold")]
    public int HostileThreshold { get; set; }

    [JsonPropertyName("friendly_threshold")]
    public int FriendlyThreshold { get; set; }

    [JsonPropertyName("neutral_range")]
    public int[] NeutralRange { get; set; } = Array.Empty<int>();
}
