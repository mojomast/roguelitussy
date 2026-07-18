using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class SynergyResolver
{
    public static List<SynergyDefinition> GetActiveSynergies(IEntity player, IContentDatabase content)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(content);

        return content.Synergies.Values
            .Where(synergy => IsActive(synergy, player, content))
            .OrderBy(synergy => synergy.SynergyId, StringComparer.Ordinal)
            .ToList();
    }

    public static List<SynergyDefinition> GetPotentialSynergies(IEntity player, IContentDatabase content)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(content);

        return content.Synergies.Values
            .Where(synergy => CountMissingRequirements(synergy, player, content) == 1)
            .OrderBy(synergy => synergy.SynergyId, StringComparer.Ordinal)
            .ToList();
    }

    public static void ApplyPassiveSynergies(IEntity player, IContentDatabase content, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);

        var component = player.GetComponent<SynergyComponent>();
        if (component is null)
        {
            component = new SynergyComponent();
            player.SetComponent(component);
        }

        var active = GetActiveSynergies(player, content);
        var activeIds = new HashSet<string>(active.Select(synergy => synergy.SynergyId), StringComparer.Ordinal);

        for (var index = component.AppliedPassiveSynergyIds.Count - 1; index >= 0; index--)
        {
            var appliedId = component.AppliedPassiveSynergyIds[index];
            if (activeIds.Contains(appliedId))
            {
                continue;
            }

            // Only reconcile synergies with a known definition: without one we can neither
            // verify the requirements nor know which bonus to remove.
            if (!content.TryGetSynergy(appliedId, out var definition))
            {
                continue;
            }

            RemovePassive(definition, player);
            component.AppliedPassiveSynergyIds.RemoveAt(index);
        }

        foreach (var synergy in active)
        {
            if (component.AppliedPassiveSynergyIds.Contains(synergy.SynergyId, StringComparer.Ordinal))
            {
                continue;
            }

            ApplyPassive(synergy, player);
            component.AppliedPassiveSynergyIds.Add(synergy.SynergyId);
        }
    }

    private static void ApplyPassive(SynergyDefinition synergy, IEntity player)
    {
        if (!string.Equals(synergy.BonusEffectType, "stat_mod", StringComparison.Ordinal))
        {
            return;
        }

        player.Stats.Attack += Math.Max(0, synergy.BonusValue);
    }

    private static void RemovePassive(SynergyDefinition synergy, IEntity player)
    {
        if (!string.Equals(synergy.BonusEffectType, "stat_mod", StringComparison.Ordinal))
        {
            return;
        }

        player.Stats.Attack -= Math.Max(0, synergy.BonusValue);
    }

    private static bool IsActive(SynergyDefinition synergy, IEntity player, IContentDatabase content) =>
        CountMissingRequirements(synergy, player, content) == 0;

    private static int CountMissingRequirements(SynergyDefinition synergy, IEntity player, IContentDatabase content)
    {
        var missing = 0;
        var relics = player.GetComponent<RelicComponent>()?.RelicIds ?? new List<string>();
        var perks = player.GetComponent<ProgressionComponent>()?.SelectedPerkIds ?? new List<string>();
        var tags = GetOwnedItemTags(player, content);
        var archetypeId = player.GetComponent<ArchetypeComponent>()?.ArchetypeId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(synergy.ArchetypeRestriction)
            && !string.Equals(synergy.ArchetypeRestriction, archetypeId, StringComparison.OrdinalIgnoreCase))
        {
            missing++;
        }

        missing += synergy.RequiredRelicIds.Count(relicId => !relics.Contains(relicId, StringComparer.Ordinal));
        missing += synergy.RequiredPerkIds.Count(perkId => !perks.Contains(perkId, StringComparer.Ordinal));
        missing += synergy.RequiredItemTags.Count(tag => !tags.Contains(tag));
        return missing;
    }

    private static HashSet<string> GetOwnedItemTags(IEntity player, IContentDatabase content)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);
        var inventory = player.GetComponent<InventoryComponent>();
        if (inventory is null)
        {
            return tags;
        }

        foreach (var item in inventory.Items)
        {
            AddItemTags(item.TemplateId, content, tags);
        }

        foreach (var equipped in inventory.EquippedItems.Values)
        {
            AddItemTags(equipped.Item.TemplateId, content, tags);
        }

        return tags;
    }

    private static void AddItemTags(string templateId, IContentDatabase content, ISet<string> tags)
    {
        if (!content.TryGetItemTemplate(templateId, out var template) || template.Tags is null)
        {
            return;
        }

        foreach (var tag in template.Tags)
        {
            tags.Add(tag);
        }
    }
}
