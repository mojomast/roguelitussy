using System.Collections.Generic;

namespace Roguelike.Core;

public static class RequirementValidator
{
    public static bool MeetsRequirements(IEntity entity, ItemTemplate template)
    {
        if (template.Requirements is null || template.Requirements.Count == 0)
        {
            return true;
        }

        foreach (var requirement in template.Requirements)
        {
            if (!MeetsSingleRequirement(entity, requirement.Key, requirement.Value))
            {
                return false;
            }
        }

        return true;
    }

    public static List<string> GetFailedRequirements(IEntity entity, ItemTemplate template)
    {
        var failures = new List<string>();

        if (template.Requirements is null || template.Requirements.Count == 0)
        {
            return failures;
        }

        foreach (var requirement in template.Requirements)
        {
            if (!MeetsSingleRequirement(entity, requirement.Key, requirement.Value))
            {
                var currentValue = GetCurrentValue(entity, requirement.Key);
                failures.Add($"Requires {requirement.Key} {requirement.Value} (current: {currentValue})");
            }
        }

        return failures;
    }

    private static bool MeetsSingleRequirement(IEntity entity, string key, int requiredValue)
    {
        return GetCurrentValue(entity, key) >= requiredValue;
    }

    private static int GetCurrentValue(IEntity entity, string key)
    {
        switch (key.ToLowerInvariant())
        {
            case "level":
                var progression = entity.GetComponent<ProgressionComponent>();
                return progression?.Level ?? 1;
            case "attack":
            case "strength":
                return entity.Stats.Attack;
            case "defense":
                return entity.Stats.Defense;
            case "accuracy":
            case "dexterity":
                return entity.Stats.Accuracy;
            case "speed":
                return entity.Stats.Speed;
            default:
                return 0;
        }
    }
}
