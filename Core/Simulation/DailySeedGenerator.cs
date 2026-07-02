using System;

namespace Roguelike.Core;

public static class DailySeedGenerator
{
    public static int GetTodaysSeed()
    {
        var today = DateTime.UtcNow;
        return GetSeedForDate(today);
    }

    public static string GetTodaysDateString() => DateTime.UtcNow.ToString("yyyy-MM-dd");

    public static int GetSeedForDate(DateTime date)
    {
        var utc = date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime();
        var dateSeed = utc.Year * 10000 + utc.Month * 100 + utc.Day;
        return dateSeed ^ 0x52_4F_47_55;
    }

    public static int CalculateScore(int floorReached, int enemiesKilled, int turnsTaken, int goldRemaining) =>
        Math.Max(0, floorReached) * 100
        + Math.Max(0, enemiesKilled) * 10
        - Math.Max(0, turnsTaken) / 2
        + Math.Max(0, goldRemaining);
}
