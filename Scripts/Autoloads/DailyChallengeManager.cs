using System;
using System.IO;
using System.Text.Json;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public sealed class DailyChallengeData
{
    public string LastAttemptDate { get; set; } = string.Empty;

    public bool TodayAttempted { get; set; }

    public bool TodayCompleted { get; set; }

    public int TodayBestFloor { get; set; }

    public int TodayBestScore { get; set; }
}

public partial class DailyChallengeManager : Node
{
    public const string DailyChallengeUserPath = "user://daily_challenge.json";

    private DailyChallengeData _data = new();

    public string LastAttemptDate => _data.LastAttemptDate;

    public bool TodayAttempted => IsToday() && _data.TodayAttempted;

    public bool TodayCompleted => IsToday() && _data.TodayCompleted;

    public int TodayBestFloor => IsToday() ? _data.TodayBestFloor : 0;

    public int TodayBestScore => IsToday() ? _data.TodayBestScore : 0;

    public override void _Ready()
    {
        Load();
        var bus = GetNodeOrNull<EventBus>("/root/EventBus");
        if (bus is not null)
        {
            bus.GameOverWithStats += RecordDailyAttempt;
        }
    }

    public void Load() => LoadFromFile(GlobalizePath(DailyChallengeUserPath));

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            _data = new DailyChallengeData();
            return;
        }

        try
        {
            _data = JsonSerializer.Deserialize<DailyChallengeData>(File.ReadAllText(path)) ?? new DailyChallengeData();
        }
        catch
        {
            // A corrupt or unreadable daily-challenge file must never take down the autoload;
            // fall back to a fresh day so the challenge stays playable.
            _data = new DailyChallengeData();
        }

        ResetIfNewDay();
    }

    public void Save() => SaveToFile(GlobalizePath(DailyChallengeUserPath));

    public void SaveToFile(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tempPath, path, true);
        }
        catch
        {
            // Daily-challenge persistence should never crash gameplay; a failed write is retried on the next save.
        }
    }

    public void RecordDailyAttempt(RunStats stats)
    {
        if (stats.Seed != DailySeedGenerator.GetTodaysSeed())
        {
            return;
        }

        ResetIfNewDay();
        _data.LastAttemptDate = DailySeedGenerator.GetTodaysDateString();
        _data.TodayAttempted = true;
        _data.TodayCompleted = true;
        _data.TodayBestFloor = Math.Max(_data.TodayBestFloor, stats.FloorReached);
        var score = DailySeedGenerator.CalculateScore(stats.FloorReached, stats.EnemiesKilled, stats.TotalTurns, stats.GoldCollected);
        _data.TodayBestScore = Math.Max(_data.TodayBestScore, score);
        Save();
    }

    private bool IsToday() => string.Equals(_data.LastAttemptDate, DailySeedGenerator.GetTodaysDateString(), StringComparison.Ordinal);

    private void ResetIfNewDay()
    {
        if (IsToday())
        {
            return;
        }

        _data.LastAttemptDate = DailySeedGenerator.GetTodaysDateString();
        _data.TodayAttempted = false;
        _data.TodayCompleted = false;
        _data.TodayBestFloor = 0;
        _data.TodayBestScore = 0;
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
