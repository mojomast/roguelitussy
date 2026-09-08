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
            progression.PerkDraftsGenerated = true;
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

        var available = content.PerkTemplates.Values
            .Where(perk => perk.UnlockLevel <= progression.Level
                && !progression.SelectedPerkIds.Contains(perk.TemplateId, StringComparer.Ordinal)
                && IsPerkAvailableToArchetype(entity, perk))
            .OrderBy(perk => perk.UnlockLevel)
            .ThenBy(perk => perk.DisplayName, StringComparer.Ordinal)
            .ToArray();

        if (!progression.PerkDraftsGenerated)
        {
            return available;
        }

        EnsurePerkDrafts(entity, progression, available);
        if (progression.PendingPerkDrafts.Count == 0)
        {
            return Array.Empty<PerkTemplate>();
        }

        var draftIds = progression.PendingPerkDrafts[0];
        return draftIds
            .Select(id => content.PerkTemplates.TryGetValue(id, out var perk) ? perk : null)
            .Where(perk => perk is not null)
            .Cast<PerkTemplate>()
            .ToArray();
    }

    public static void GeneratePerkDrafts(IEntity entity, IContentDatabase? content)
    {
        var progression = entity.GetComponent<ProgressionComponent>();
        if (progression is null || content is null)
        {
            return;
        }

        progression.PerkDraftsGenerated = true;
        var available = content.PerkTemplates.Values
            .Where(perk => perk.UnlockLevel <= progression.Level
                && !progression.SelectedPerkIds.Contains(perk.TemplateId, StringComparer.Ordinal)
                && IsPerkAvailableToArchetype(entity, perk))
            .OrderBy(perk => perk.UnlockLevel)
            .ThenBy(perk => perk.DisplayName, StringComparer.Ordinal)
            .ToArray();
        EnsurePerkDrafts(entity, progression, available);
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

        if (progression.PerkDraftsGenerated)
        {
            EnsurePerkDrafts(entity, progression, content.PerkTemplates.Values
                .Where(candidate => candidate.UnlockLevel <= progression.Level
                    && !progression.SelectedPerkIds.Contains(candidate.TemplateId, StringComparer.Ordinal)
                    && IsPerkAvailableToArchetype(entity, candidate))
                .OrderBy(candidate => candidate.UnlockLevel)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
                .ToArray());

            if (progression.PendingPerkDrafts.Count == 0
                || !progression.PendingPerkDrafts[0].Contains(perkId, StringComparer.Ordinal))
            {
                message = $"Perk '{perk.DisplayName}' is not in the current draft.";
                return false;
            }
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

        if (!IsPerkAvailableToArchetype(entity, perk))
        {
            message = $"Perk '{perk.DisplayName}' is not available to this archetype.";
            return false;
        }

        foreach (var effect in perk.Effects)
        {
            switch (effect.Type)
            {
                case "perk_gate":
                    break;
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
        if (progression.PerkDraftsGenerated && progression.PendingPerkDrafts.Count > 0)
        {
            progression.PendingPerkDrafts.RemoveAt(0);
        }
        message = $"Learned perk {perk.DisplayName}.";
        return true;
    }

    private static void EnsurePerkDrafts(IEntity entity, ProgressionComponent progression, IReadOnlyList<PerkTemplate> available)
    {
        var targetCount = Math.Max(0, progression.UnspentPerkChoices);
        var reserved = new HashSet<string>(progression.SelectedPerkIds, StringComparer.Ordinal);
        foreach (var draft in progression.PendingPerkDrafts)
        {
            foreach (var id in draft)
            {
                reserved.Add(id);
            }
        }

        while (progression.PendingPerkDrafts.Count < targetCount)
        {
            var candidates = available.Where(perk => !reserved.Contains(perk.TemplateId)).ToArray();
            var draft = new List<string>();
            if (candidates.Length > 0)
            {
                var start = StableIndex(entity, progression.PendingPerkDrafts.Count, candidates.Length);
                for (var offset = 0; offset < candidates.Length && draft.Count < 3; offset++)
                {
                    var perk = candidates[(start + offset) % candidates.Length];
                    draft.Add(perk.TemplateId);
                    reserved.Add(perk.TemplateId);
                }
            }

            progression.PendingPerkDrafts.Add(draft);
            if (draft.Count == 0)
            {
                break;
            }
        }
    }

    private static int StableIndex(IEntity entity, int draftIndex, int length)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in entity.Id.Value.ToString("N"))
            {
                hash = (hash ^ character) * 16777619u;
            }

            hash = (hash ^ (uint)draftIndex) * 16777619u;
            return (int)(hash % (uint)length);
        }
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

    private static bool IsPerkAvailableToArchetype(IEntity entity, PerkTemplate perk)
    {
        var archetypeId = entity.GetComponent<ArchetypeComponent>()?.ArchetypeId;
        foreach (var effect in perk.Effects)
        {
            if (!string.Equals(effect.Type, "perk_gate", StringComparison.Ordinal))
            {
                continue;
            }

            return string.Equals(effect.Stat, ArchetypeDefinitions.Get(archetypeId).Id, StringComparison.Ordinal);
        }

        return !ArchetypeDefinitions.IsExclusivePerkForOtherArchetype(perk.TemplateId, archetypeId);
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
