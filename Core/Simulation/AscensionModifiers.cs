using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class AscensionModifiers
{
    public static int GetCurrentAscensionLevel() => 0;

    public static void ApplyEnemyStats(Stats stats, int level, IContentDatabase content)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var multiplier = 1f;
        foreach (var modifier in GetActiveModifiers(level, content))
        {
            if (string.Equals(modifier.EffectType, "enemy_stat_boost", StringComparison.Ordinal))
            {
                multiplier += Math.Max(0f, modifier.EffectValue);
            }
        }

        if (multiplier <= 1f)
        {
            return;
        }

        stats.MaxHP = Math.Max(1, (int)Math.Ceiling(stats.MaxHP * multiplier));
        stats.HP = stats.MaxHP;
    }

    public static int ResolveStartingHpPenalty(int level, IContentDatabase content)
    {
        return GetActiveModifiers(level, content)
            .Where(modifier => string.Equals(modifier.EffectType, "starting_hp_penalty", StringComparison.Ordinal))
            .Sum(modifier => Math.Max(0, (int)Math.Round(modifier.EffectValue)));
    }

    public static List<AscensionModifier> GetActiveModifiers(int level, IContentDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var clamped = Math.Clamp(level, 0, 10);
        return content.AscensionModifiers.Values
            .Where(modifier => modifier.AscensionLevel <= clamped)
            .OrderBy(modifier => modifier.AscensionLevel)
            .ThenBy(modifier => modifier.ModifierId, StringComparer.Ordinal)
            .ToList();
    }

    public static bool HasModifier(string modifierId, int level, IContentDatabase content) =>
        GetActiveModifiers(level, content).Any(modifier => string.Equals(modifier.ModifierId, modifierId, StringComparison.Ordinal));
}
