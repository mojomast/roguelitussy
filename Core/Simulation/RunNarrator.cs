using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Roguelike.Core;

public static class RunNarrator
{
    private static readonly Regex PlaceholderPattern = new("\\{([a-z_]+)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string GenerateEpitaph(RunHistoryEntry run, IContentDatabase content, int seed)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(content);

        var random = new Random(seed);
        var matches = content.NarrativeTemplates.Values
            .Where(template => Matches(template, run))
            .OrderBy(_ => random.Next())
            .Take(5)
            .ToList();

        if (matches.Count == 0)
        {
            return $"{run.CharacterName} reached floor {run.FloorReached}, slew {run.EnemiesKilled}, and fell to {run.CauseOfDeath}.";
        }

        return string.Join(" ", matches.Select(template =>
        {
            var sentence = template.SentenceTemplates[random.Next(template.SentenceTemplates.Length)];
            return InterpolateTemplate(sentence, run);
        }));
    }

    public static bool TemplateUsesOnlyAllowedPlaceholders(NarrativeTemplate template)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "name", "archetype", "floor", "kills", "best_item", "cause", "echo_count",
        };

        return template.SentenceTemplates
            .SelectMany(sentence => PlaceholderPattern.Matches(sentence).Select(match => match.Groups[1].Value))
            .All(allowed.Contains);
    }

    private static bool Matches(NarrativeTemplate template, RunHistoryEntry run)
    {
        return template.Condition switch
        {
            "always" => true,
            "archetype" => string.IsNullOrWhiteSpace(template.ConditionValue)
                || string.Equals(run.Archetype, template.ConditionValue, StringComparison.OrdinalIgnoreCase),
            "floor_reached" => MatchesRange(template.ConditionValue, run.FloorReached),
            "kill_count_range" => MatchesRange(template.ConditionValue, run.EnemiesKilled),
            "cause_of_death" => run.CauseOfDeath.Contains(template.ConditionValue, StringComparison.OrdinalIgnoreCase),
            "relic_held" => run.RelicsHeld?.Contains(template.ConditionValue, StringComparer.Ordinal) == true,
            "synergy_active" => run.SynergyIds?.Contains(template.ConditionValue, StringComparer.Ordinal) == true,
            "best_item" => string.IsNullOrWhiteSpace(template.ConditionValue)
                || run.BestItemName.Contains(template.ConditionValue, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool MatchesRange(string value, int actual)
    {
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = part.Split('-', StringSplitOptions.TrimEntries);
            if (range.Length == 1 && int.TryParse(range[0], out var exact) && actual == exact)
            {
                return true;
            }

            if (range.Length == 2
                && int.TryParse(range[0], out var min)
                && int.TryParse(range[1], out var max)
                && actual >= min
                && actual <= max)
            {
                return true;
            }
        }

        return false;
    }

    private static string InterpolateTemplate(string template, RunHistoryEntry run)
    {
        return template
            .Replace("{name}", run.CharacterName, StringComparison.Ordinal)
            .Replace("{archetype}", run.Archetype, StringComparison.Ordinal)
            .Replace("{floor}", run.FloorReached.ToString(), StringComparison.Ordinal)
            .Replace("{kills}", run.EnemiesKilled.ToString(), StringComparison.Ordinal)
            .Replace("{best_item}", string.IsNullOrWhiteSpace(run.BestItemName) ? "their best find" : run.BestItemName, StringComparison.Ordinal)
            .Replace("{cause}", string.IsNullOrWhiteSpace(run.CauseOfDeath) ? "the dungeon" : run.CauseOfDeath, StringComparison.Ordinal)
            .Replace("{echo_count}", Math.Max(0, run.GoldCollected / 50).ToString(), StringComparison.Ordinal);
    }
}
