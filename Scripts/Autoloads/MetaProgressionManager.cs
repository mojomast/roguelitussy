using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class MetaProgressionManager : Node
{
    public const string MetaProgressionUserPath = "user://meta_progress.json";
    private const string MetaUpgradeResourcePath = "res://Content/meta_upgrades.json";

    private MetaProgressionData _data = new();
    private IReadOnlyList<MetaProgressionUpgrade> _upgrades = Array.Empty<MetaProgressionUpgrade>();
    private bool _suppressNextSimpleGameOver;

    public IReadOnlyList<MetaProgressionUpgrade> Upgrades => _upgrades;

    public override void _Ready()
    {
        Load();
        BindEventBus(GetNodeOrNull<EventBus>("/root/EventBus"));
    }

    public void Load()
    {
        _upgrades = MetaProgressionService.LoadUpgradesFromFile(GlobalizePath(MetaUpgradeResourcePath));
        try
        {
            _data = MetaProgressionData.LoadFromFile(GlobalizePath(MetaProgressionUserPath));
        }
        catch
        {
            _data = new MetaProgressionData();
        }
    }

    public void Save()
    {
        try
        {
            _data.SaveToFile(GlobalizePath(MetaProgressionUserPath));
        }
        catch
        {
            // Meta progression should never crash gameplay; failed writes can be retried next save.
        }
    }

    public int GetEchoes() => _data.EchoesAvailable;

    public void AddEchoes(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _data.EchoesTotal += amount;
        Save();
    }

    public bool SpendEchoes(int cost)
    {
        if (cost < 0 || _data.EchoesAvailable < cost)
        {
            return false;
        }

        _data.EchoesSpent += cost;
        Save();
        return true;
    }

    public bool IsUnlocked(string upgradeId) => GetUnlockLevel(upgradeId) > 0;

    public int GetUnlockLevel(string upgradeId)
    {
        return !string.IsNullOrWhiteSpace(upgradeId) && _data.UnlockLevels.TryGetValue(upgradeId, out var level)
            ? Math.Max(0, level)
            : 0;
    }

    public bool TryUpgrade(string upgradeId)
    {
        var upgrade = _upgrades.FirstOrDefault(candidate => string.Equals(candidate.Id, upgradeId, StringComparison.Ordinal));
        if (upgrade is null)
        {
            return false;
        }

        var currentLevel = GetUnlockLevel(upgrade.Id);
        if (currentLevel >= upgrade.MaxLevel)
        {
            return false;
        }

        var cost = upgrade.GetCostForLevel(currentLevel);
        if (cost <= 0 || _data.EchoesAvailable < cost)
        {
            return false;
        }

        _data.EchoesSpent += cost;
        _data.UnlockLevels[upgrade.Id] = currentLevel + 1;
        Save();
        return true;
    }

    public void RecordRun(RunHistoryEntry entry)
    {
        _data.AddRunHistory(entry);
        Save();
    }

    public IReadOnlyList<RunHistoryEntry> GetRunHistory() => _data.RunHistory.ToArray();

    public bool HasCompletedFirstClear => _data.HasCompletedFirstClear;

    public int AscensionLevel => _data.AscensionLevel;

    public void CompleteRun()
    {
        _data.HasCompletedFirstClear = true;
        Save();
    }

    public void SetAscensionLevel(int level)
    {
        var nextLevel = _data.HasCompletedFirstClear ? Math.Clamp(level, 0, 10) : 0;
        if (_data.AscensionLevel == nextLevel)
        {
            return;
        }

        _data.AscensionLevel = nextLevel;
        Save();
        GetNodeOrNull<EventBus>("/root/EventBus")?.EmitAscensionLevelChanged(nextLevel);
    }

    public int GetIntBonus(string effect)
    {
        return MetaProgressionService.ResolveIntEffectValue(_data.UnlockLevels, _upgrades, effect);
    }

    public IReadOnlyList<string> GetRepeatedStringBonuses(string effect)
    {
        return MetaProgressionService.ResolveRepeatedStringEffectValues(_data.UnlockLevels, _upgrades, effect);
    }

    private void BindEventBus(EventBus? bus)
    {
        if (bus is null)
        {
            return;
        }

        bus.GameOverWithStats += OnGameOverWithStats;
        bus.GameOver += OnGameOver;
    }

    private void OnGameOverWithStats(RunStats stats)
    {
        _suppressNextSimpleGameOver = true;
        var firstDepth = IsFirstTimeReachedDepth(stats.FloorReached);
        var echoBonus = GetIntBonus("echo_bonus_pct");
        var echoes = MetaProgressionService.CalculateEchoAward(
            stats.FloorReached,
            stats.EnemiesKilled,
            stats.GoldCollected,
            firstDepth,
            echoBonus);

        AddEchoes(echoes);
        var entry = ToHistoryEntry(stats);
        if (GetNodeOrNull<ContentDatabase>("/root/ContentDatabase")?.Database is { } content)
        {
            entry = entry with { Epitaph = RunNarrator.GenerateEpitaph(entry, content, stats.Seed) };
        }

        RecordRun(entry);
    }

    private void OnGameOver(int finalDepth, int turnsSurvived)
    {
        if (_suppressNextSimpleGameOver)
        {
            _suppressNextSimpleGameOver = false;
            return;
        }

        var entry = new RunHistoryEntry(
            "Unknown",
            "unknown",
            Math.Max(0, finalDepth),
            0,
            0,
            "Unknown",
            string.Empty,
            Math.Max(0, turnsSurvived),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var echoes = MetaProgressionService.CalculateEchoAward(entry.FloorReached, 0, 0, IsFirstTimeReachedDepth(entry.FloorReached), GetIntBonus("echo_bonus_pct"));
        AddEchoes(echoes);
        RecordRun(entry);
    }

    private bool IsFirstTimeReachedDepth(int floorReached)
    {
        return floorReached > 0 && !_data.RunHistory.Any(entry => entry.FloorReached >= floorReached);
    }

    private static RunHistoryEntry ToHistoryEntry(RunStats stats)
    {
        return new RunHistoryEntry(
            stats.CharacterName,
            "unknown",
            Math.Max(0, stats.FloorReached),
            Math.Max(0, stats.EnemiesKilled),
            Math.Max(0, stats.GoldCollected),
            string.IsNullOrWhiteSpace(stats.CauseOfDeath) ? "Unknown" : stats.CauseOfDeath,
            stats.BestItemName,
            Math.Max(0, stats.TotalTurns),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private static string GlobalizePath(string path)
    {
        if (path.StartsWith("user://", StringComparison.Ordinal))
        {
            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "godotussy",
                path[7..].Replace('/', Path.DirectorySeparatorChar));
        }

        return ProjectSettings.GlobalizePath(path);
    }
}
