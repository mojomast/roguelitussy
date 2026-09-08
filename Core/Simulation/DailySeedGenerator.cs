using System;

namespace Roguelike.Core;

public static class DailySeedGenerator
{
    public const string SupportedEffectType = "speed_score";

    private static readonly DailyModifierInfo[] ModifiersByDay =
    {
        new("sunday_bountiful", "Bountiful", "Double gold and loot drops.", "double_rewards", 2f),
        new("monday_merchants_week", "Merchant's Week", "All prices are 30% cheaper.", "shop_discount", 0.3f),
        new("tuesday_elite_surge", "Elite Surge", "Every floor has an elite enemy.", "elite_every_floor", 1f),
        new("wednesday_cursed_land", "Cursed Land", "All floors have curse room effects.", "curse_every_floor", 1f),
        new("thursday_speed_run", "Speed Run", "Score rewards faster clears.", SupportedEffectType, 1000f),
        new("friday_relic_rush", "Relic Rush", "Start with one random relic.", "starting_relic", 1f),
        new("saturday_death_march", "Death March", "No safe floors; bosses have +50% HP.", "boss_hp_boost", 0.5f),
    };

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

    public static DailyModifierInfo GetModifierForDate(DateTime date)
    {
        var utc = date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime();
        return ModifiersByDay[(int)utc.DayOfWeek];
    }

    public static string GetTodaysModifierId() => GetModifierForDate(DateTime.UtcNow).ModifierId;

    public static int CalculateScore(int floorReached, int enemiesKilled, int turnsTaken, int goldRemaining) =>
        CalculateScore(floorReached, enemiesKilled, turnsTaken, goldRemaining, null);

    public static int CalculateScore(
        int floorReached,
        int enemiesKilled,
        int turnsTaken,
        int goldRemaining,
        DailyModifierInfo? modifier) =>
        Math.Max(0, floorReached) * 100
        + Math.Max(0, enemiesKilled) * 10
        - Math.Max(0, turnsTaken) / 2
        + Math.Max(0, goldRemaining)
        + (modifier is not null && modifier.EffectType == SupportedEffectType
            ? Math.Max(0, (int)modifier.EffectValue - Math.Max(0, turnsTaken))
            : 0);
}

public sealed record DailyModifierInfo(
    string ModifierId,
    string DisplayName,
    string Description,
    string EffectType,
    float EffectValue)
{
    public bool IsSupported => string.Equals(EffectType, DailySeedGenerator.SupportedEffectType, StringComparison.Ordinal);
}
