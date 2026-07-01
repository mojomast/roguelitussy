using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class MetaProgressionData
{
    public const int MaxRunHistoryEntries = 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int EchoesTotal { get; set; }

    public int EchoesSpent { get; set; }

    public Dictionary<string, int> UnlockLevels { get; set; } = new(StringComparer.Ordinal);

    public List<RunHistoryEntry> RunHistory { get; set; } = new();

    [JsonIgnore]
    public int EchoesAvailable => Math.Max(0, EchoesTotal - EchoesSpent);

    public static MetaProgressionData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new MetaProgressionData();
        }

        var data = JsonSerializer.Deserialize<MetaProgressionData>(json, JsonOptions) ?? new MetaProgressionData();
        data.Normalize();
        return data;
    }

    public string ToJson()
    {
        Normalize();
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public static MetaProgressionData LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new MetaProgressionData();
        }

        return FromJson(File.ReadAllText(path));
    }

    public void SaveToFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Normalize();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, ToJson());
        File.Move(tempPath, path, true);
    }

    public void AddRunHistory(RunHistoryEntry entry)
    {
        RunHistory.Insert(0, entry);
        TrimRunHistory();
    }

    public void Normalize()
    {
        EchoesTotal = Math.Max(0, EchoesTotal);
        EchoesSpent = Math.Clamp(EchoesSpent, 0, EchoesTotal);
        UnlockLevels ??= new Dictionary<string, int>(StringComparer.Ordinal);
        RunHistory ??= new List<RunHistoryEntry>();
        TrimRunHistory();
    }

    private void TrimRunHistory()
    {
        if (RunHistory.Count > MaxRunHistoryEntries)
        {
            RunHistory.RemoveRange(MaxRunHistoryEntries, RunHistory.Count - MaxRunHistoryEntries);
        }
    }
}

public sealed record RunHistoryEntry(
    string CharacterName,
    string Archetype,
    int FloorReached,
    int EnemiesKilled,
    int GoldCollected,
    string CauseOfDeath,
    string BestItemName,
    int TotalTurns,
    long Timestamp);
