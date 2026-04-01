using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class ProgressionService
{
    public sealed record AwardResult(int ExperienceGained, int LevelsGained, IReadOnlyList<int> ReachedLevels);

    public static AwardResult AwardExperience(IEntity entity, int experience)
    {
        var progression = entity.GetComponent<ProgressionComponent>();
        if (progression is null || experience <= 0)
        {
            return new AwardResult(0, 0, Array.Empty<int>());
        }

        progression.Experience += experience;
        var reachedLevels = new List<int>();
        while (progression.CanLevelUp)
        {
            progression.Level++;
            progression.UnspentStatPoints += 2;
            progression.UnspentPerkChoices += 1;
            progression.ExperienceToNextLevel = ProgressionComponent.CalculateXpThreshold(progression.Level);

            entity.Stats.MaxHP += 3;
            entity.Stats.HP = Math.Min(entity.Stats.HP + 3, entity.Stats.MaxHP);
            entity.Stats.Attack += 1;
            reachedLevels.Add(progression.Level);
        }

        return new AwardResult(experience, reachedLevels.Count, reachedLevels);
    }

    public static bool TrySpendStatPoint(IEntity entity, string statName, out string message)
    {
        var progression = entity.GetComponent<ProgressionComponent>();
        if (progression is null)
        {
            message = "Progression data is unavailable.";
            return false;
        }

        if (progression.UnspentStatPoints <= 0)
        {
            message = "No unspent stat points remain.";
            return false;
        }

        if (!TryApplyStatBonus(entity, statName, statName == "MaxHP" ? 3 : 1))
        {
            message = $"Unknown stat '{statName}'.";
            return false;
        }

        progression.UnspentStatPoints--;
        message = $"Spent a stat point on {statName}.";
        return true;
    }

    public static IReadOnlyList<PerkTemplate> GetAvailablePerkChoices(IEntity entity, IContentDatabase? content)
    {
        var progression = entity.GetComponent<ProgressionComponent>();
        if (progression is null || content is null)
        {
            return Array.Empty<PerkTemplate>();
        }

        return content.PerkTemplates.Values
            .Where(perk => perk.UnlockLevel <= progression.Level && !progression.SelectedPerkIds.Contains(perk.TemplateId, StringComparer.Ordinal))
            .OrderBy(perk => perk.UnlockLevel)
            .ThenBy(perk => perk.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool TrySelectPerk(IEntity entity, IContentDatabase? content, string perkId, out string message)
    {
        var progression = entity.GetComponent<ProgressionComponent>();
        if (progression is null)
        {
            message = "Progression data is unavailable.";
            return false;
        }

        if (progression.UnspentPerkChoices <= 0)
        {
            message = "No perk choices are waiting.";
            return false;
        }

        if (content is null || !content.TryGetPerkTemplate(perkId, out var perk))
        {
            message = $"Unknown perk '{perkId}'.";
            return false;
        }

        if (progression.SelectedPerkIds.Contains(perkId, StringComparer.Ordinal))
        {
            message = $"Perk '{perk.DisplayName}' is already selected.";
            return false;
        }

        if (perk.UnlockLevel > progression.Level)
        {
            message = $"Perk '{perk.DisplayName}' is not unlocked yet.";
            return false;
        }

        foreach (var effect in perk.Effects)
        {
            switch (effect.Type)
            {
                case "stat_bonus":
                    if (!TryApplyStatBonus(entity, effect.Stat, effect.Value))
                    {
                        message = $"Perk '{perk.DisplayName}' references unsupported stat '{effect.Stat}'.";
                        return false;
                    }

                    break;
                case "shop_discount_percent":
                    break;
                default:
                    message = $"Perk '{perk.DisplayName}' uses unsupported effect '{effect.Type}'.";
                    return false;
            }
        }

        progression.SelectedPerkIds.Add(perk.TemplateId);
        progression.UnspentPerkChoices--;
        message = $"Learned perk {perk.DisplayName}.";
        return true;
    }

    public static int ResolveShopDiscountPercent(IEntity entity, IContentDatabase? content)
    {
        var progression = entity.GetComponent<ProgressionComponent>();
        if (progression is null || content is null)
        {
            return 0;
        }

        var totalDiscount = 0;
        foreach (var perkId in progression.SelectedPerkIds)
        {
            if (!content.TryGetPerkTemplate(perkId, out var perk))
            {
                continue;
            }

            foreach (var effect in perk.Effects)
            {
                if (string.Equals(effect.Type, "shop_discount_percent", StringComparison.Ordinal))
                {
                    totalDiscount += effect.Value;
                }
            }
        }

        return Math.Clamp(totalDiscount, 0, 90);
    }

    private static bool TryApplyStatBonus(IEntity entity, string? statName, int amount)
    {
        if (string.IsNullOrWhiteSpace(statName))
        {
            return false;
        }

        switch (statName)
        {
            case "MaxHP":
                entity.Stats.MaxHP += amount;
                entity.Stats.HP = Math.Min(entity.Stats.HP + amount, entity.Stats.MaxHP);
                return true;
            case "Attack":
                entity.Stats.Attack += amount;
                return true;
            case "Defense":
                entity.Stats.Defense += amount;
                return true;
            case "Accuracy":
                entity.Stats.Accuracy += amount;
                return true;
            case "Evasion":
                entity.Stats.Evasion += amount;
                return true;
            case "Speed":
                entity.Stats.Speed += amount;
                return true;
            case "ViewRadius":
                entity.Stats.ViewRadius += amount;
                return true;
            default:
                return false;
        }
    }
}