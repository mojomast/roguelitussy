using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Roguelike.Core;

public static class MetaProgressionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyList<MetaProgressionUpgrade> LoadUpgradesFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Array.Empty<MetaProgressionUpgrade>();
        }

        var upgrades = JsonSerializer.Deserialize<List<MetaProgressionUpgrade>>(File.ReadAllText(path), JsonOptions)
            ?? new List<MetaProgressionUpgrade>();

        return upgrades
            .Where(upgrade => !string.IsNullOrWhiteSpace(upgrade.Id))
            .OrderBy(upgrade => upgrade.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static int CalculateEchoAward(
        int floorReached,
        int enemiesKilled,
        int goldCollected,
        bool firstTimeReachedDepth,
        int echoBonusPercent)
    {
        var baseEchoes = Math.Max(0, floorReached) * 2
            + Math.Max(0, enemiesKilled) / 5
            + Math.Max(0, goldCollected) / 50;

        if (firstTimeReachedDepth && floorReached > 0)
        {
            baseEchoes += 5;
        }

        return Math.Max(0, baseEchoes + (baseEchoes * Math.Max(0, echoBonusPercent) / 100));
    }

    public static int ResolveIntEffectValue(
        IReadOnlyDictionary<string, int> unlockLevels,
        IReadOnlyList<MetaProgressionUpgrade> upgrades,
        string effect)
    {
        var upgrade = upgrades.FirstOrDefault(candidate => string.Equals(candidate.Effect, effect, StringComparison.Ordinal));
        if (upgrade is null || !unlockLevels.TryGetValue(upgrade.Id, out var level) || level <= 0)
        {
            return 0;
        }

        var valueIndex = Math.Min(level, upgrade.Values.Count) - 1;
        return valueIndex >= 0 && int.TryParse(upgrade.Values[valueIndex], out var value) ? value : 0;
    }

    public static IReadOnlyList<string> ResolveRepeatedStringEffectValues(
        IReadOnlyDictionary<string, int> unlockLevels,
        IReadOnlyList<MetaProgressionUpgrade> upgrades,
        string effect)
    {
        var upgrade = upgrades.FirstOrDefault(candidate => string.Equals(candidate.Effect, effect, StringComparison.Ordinal));
        if (upgrade is null || !unlockLevels.TryGetValue(upgrade.Id, out var level) || level <= 0)
        {
            return Array.Empty<string>();
        }

        return upgrade.Values.Take(Math.Min(level, upgrade.Values.Count)).ToArray();
    }
}
