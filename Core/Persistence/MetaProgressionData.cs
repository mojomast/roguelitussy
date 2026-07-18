using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roguelike.Core;

public sealed class MetaProgressionData
{
    public const int MaxRunHistoryEntries = 20;

    /// <summary>
    /// Schema version written to disk. Files without a version property (legacy,
    /// pre-versioning) deserialize as 0 and are upgraded in <see cref="Normalize"/>.
    /// </summary>
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int Version { get; set; }

    public int EchoesTotal { get; set; }

    public int EchoesSpent { get; set; }

    public Dictionary<string, int> UnlockLevels { get; set; } = new(StringComparer.Ordinal);

    public bool HasCompletedFirstClear { get; set; }

    public int AscensionLevel { get; set; }

    public List<RunHistoryEntry> RunHistory { get; set; } = new();

    [JsonIgnore]
    public int EchoesAvailable => Math.Max(0, EchoesTotal - EchoesSpent);

    /// <summary>
    /// Parses persisted meta progression. Corrupt, truncated, or structurally wrong
    /// JSON never throws; a fresh default instance is returned instead so callers
    /// can always rely on getting usable data back.
    /// </summary>
    public static MetaProgressionData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateFresh();
        }

        MetaProgressionData? data;
        try
        {
            data = JsonSerializer.Deserialize<MetaProgressionData>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return CreateFresh();
        }

        data ??= CreateFresh();
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
            return CreateFresh();
        }

        try
        {
            return FromJson(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CreateFresh();
        }
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
        MigrateToCurrentVersion();
        EchoesTotal = Math.Max(0, EchoesTotal);
        EchoesSpent = Math.Clamp(EchoesSpent, 0, EchoesTotal);
        AscensionLevel = HasCompletedFirstClear ? Math.Clamp(AscensionLevel, 0, 10) : 0;
        UnlockLevels ??= new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in UnlockLevels.Keys.ToArray())
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                UnlockLevels.Remove(key);
            }
            else if (UnlockLevels[key] < 0)
            {
                UnlockLevels[key] = 0;
            }
        }

        RunHistory ??= new List<RunHistoryEntry>();
        RunHistory.RemoveAll(entry => entry is null);
        TrimRunHistory();
    }

    private static MetaProgressionData CreateFresh() => new() { Version = CurrentVersion };

    /// <summary>
    /// Version 0 (unversioned legacy files) shares the current field layout, so the
    /// defaulting performed by <see cref="Normalize"/> is the whole migration. Future
    /// schema changes add explicit steps here keyed off the loaded version.
    /// </summary>
    private void MigrateToCurrentVersion() => Version = CurrentVersion;

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
    long Timestamp,
    List<string>? RelicsHeld = null,
    List<string>? SynergyIds = null,
    List<string>? ActivePerkIds = null,
    string[]? FloorsClearedThemes = null,
    string Epitaph = "");
