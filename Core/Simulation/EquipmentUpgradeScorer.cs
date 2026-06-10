using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class EquipmentUpgradeScorer
{
    private static readonly IReadOnlyDictionary<string, int> Weights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["attack"] = 100,
        ["defense"] = 100,
        ["accuracy"] = 80,
        ["evasion"] = 80,
        ["max_hp"] = 50,
        ["maxhp"] = 50,
        ["hp"] = 10,
        ["speed"] = 30,
        ["view_radius"] = 20,
        ["viewradius"] = 20,
    };

    public static int Score(IReadOnlyDictionary<string, int>? modifiers)
    {
        if (modifiers is null || modifiers.Count == 0)
        {
            return 0;
        }

        var score = 0;
        foreach (var modifier in modifiers.OrderBy(pair => NormalizeKey(pair.Key), StringComparer.Ordinal))
        {
            var key = NormalizeKey(modifier.Key);
            var weight = Weights.TryGetValue(key, out var knownWeight) ? knownWeight : 1;
            score += modifier.Value * weight;
        }

        return score;
    }

    public static bool IsStrictUpgrade(ItemTemplate candidate, EquippedItem? equipped)
    {
        if (candidate.Slot == EquipSlot.None || candidate.MaxStack > 1)
        {
            return false;
        }

        var currentScore = equipped is null ? 0 : Score(equipped.StatModifiers);
        return HasNoWorseModifiers(candidate.StatModifiers, equipped?.StatModifiers)
            && Score(candidate.StatModifiers) > currentScore;
    }

    private static bool HasNoWorseModifiers(
        IReadOnlyDictionary<string, int> candidate,
        IReadOnlyDictionary<string, int>? equipped)
    {
        if (equipped is null || equipped.Count == 0)
        {
            return true;
        }

        var candidateValues = NormalizeModifiers(candidate);
        var equippedValues = NormalizeModifiers(equipped);
        var keys = new HashSet<string>(candidateValues.Keys, StringComparer.Ordinal);
        foreach (var key in equippedValues.Keys)
        {
            keys.Add(key);
        }

        foreach (var key in keys)
        {
            candidateValues.TryGetValue(key, out var candidateValue);
            equippedValues.TryGetValue(key, out var equippedValue);
            if (candidateValue < equippedValue)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, int> NormalizeModifiers(IReadOnlyDictionary<string, int> modifiers)
    {
        var normalized = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var modifier in modifiers)
        {
            var key = NormalizeKey(modifier.Key);
            normalized[key] = normalized.TryGetValue(key, out var existing)
                ? existing + modifier.Value
                : modifier.Value;
        }

        return normalized;
    }

    private static string NormalizeKey(string key) => key.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
