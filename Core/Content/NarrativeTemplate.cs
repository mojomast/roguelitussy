using System;
using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class NarrativeTemplate
{
    [JsonPropertyName("template_id")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("condition_value")]
    public string ConditionValue { get; set; } = string.Empty;

    [JsonPropertyName("sentence_templates")]
    public string[] SentenceTemplates { get; set; } = Array.Empty<string>();
}
