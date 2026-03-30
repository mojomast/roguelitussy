using System.IO;
using System.Text.Json;

namespace Godotussy;

internal static class ToolJson
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false,
        WriteIndented = true,
    };

    public static T Read<T>(string path)
    {
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<T>(json, ReadOptions);
        if (document is null)
        {
            throw new InvalidDataException($"Failed to deserialize '{path}'.");
        }

        return document;
    }

    public static void Write<T>(string path, T document)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(document, WriteOptions);
        File.WriteAllText(path, json);
    }
}