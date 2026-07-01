using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class MetaProgressionUpgrade
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("max_level")]
    public int MaxLevel { get; set; }

    [JsonPropertyName("cost_per_level")]
    public List<int> CostPerLevel { get; set; } = new();

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("effect")]
    public string Effect { get; set; } = string.Empty;

    [JsonPropertyName("values")]
    [JsonConverter(typeof(MetaUpgradeValueListConverter))]
    public List<string> Values { get; set; } = new();

    public int GetCostForLevel(int currentLevel)
    {
        return currentLevel >= 0 && currentLevel < CostPerLevel.Count ? CostPerLevel[currentLevel] : 0;
    }
}

internal sealed class MetaUpgradeValueListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        var values = new List<string>();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            return values;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            values.Add(reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString() ?? string.Empty,
                JsonTokenType.Number => reader.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonTokenType.True => bool.TrueString,
                JsonTokenType.False => bool.FalseString,
                _ => string.Empty,
            });
        }

        return values;
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var entry in value)
        {
            writer.WriteStringValue(entry);
        }

        writer.WriteEndArray();
    }
}
