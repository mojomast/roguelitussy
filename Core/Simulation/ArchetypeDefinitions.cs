using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record ArchetypeDefinition(
    string Id,
    Stats BaseStats,
    string[] StartingItemIds,
    string[] StartingAbilityIds,
    string SignatureMechanicId,
    string[] ExclusivePerkIds)
{
    public Stats CreateStats() => BaseStats.Clone();
}

public sealed class ArchetypeComponent
{
    public string ArchetypeId { get; set; } = ArchetypeDefinitions.DefaultId;

    public string SignatureMechanicId { get; set; } = string.Empty;
}

public static class ArchetypeDefinitions
{
    public const string DefaultId = "vanguard";

    public static readonly IReadOnlyDictionary<string, ArchetypeDefinition> All = new Dictionary<string, ArchetypeDefinition>(StringComparer.Ordinal)
    {
        ["vanguard"] = new(
            "vanguard",
            new Stats { HP = 50, MaxHP = 50, Attack = 8, Accuracy = 75, Defense = 5, Evasion = 8, Speed = 90, ViewRadius = 7 },
            new[] { "potion_health", "potion_health", "item_shield_basic" },
            new[] { "shield_bash" },
            "shield_bash_on_floor_enter",
            new[] { "perk_fortress", "perk_undying" }),
        ["ranger"] = new(
            "ranger",
            new Stats { HP = 35, MaxHP = 35, Attack = 10, Accuracy = 95, Defense = 2, Evasion = 18, Speed = 110, ViewRadius = 10 },
            new[] { "potion_health", "item_arrows_bundle" },
            new[] { "ranged_shot" },
            "ranged_attack_enabled",
            new[] { "perk_eagle_eye", "perk_volley" }),
        ["trickster"] = new(
            "trickster",
            new Stats { HP = 30, MaxHP = 30, Attack = 9, Accuracy = 85, Defense = 1, Evasion = 22, Speed = 120, ViewRadius = 9 },
            new[] { "potion_health", "item_smoke_bomb" },
            new[] { "backstab" },
            "kill_streak_double_turn",
            new[] { "perk_phantom_step", "perk_death_dance" }),
        ["arcanist"] = new(
            "arcanist",
            new Stats { HP = 28, MaxHP = 28, Attack = 6, Accuracy = 90, Defense = 1, Evasion = 12, Speed = 100, ViewRadius = 9 },
            new[] { "scroll_fireball", "scroll_frost_nova", "potion_mana" },
            new[] { "arcane_bolt", "mana_shield" },
            "ability_charges_not_potions",
            new[] { "perk_arcane_surge", "perk_spell_echo" }),
    };

    public static ArchetypeDefinition Get(string? archetypeIdOrName)
    {
        var id = NormalizeId(archetypeIdOrName);
        return All.TryGetValue(id, out var definition) ? definition : All[DefaultId];
    }

    public static bool IsExclusivePerkForOtherArchetype(string perkId, string? archetypeIdOrName)
    {
        var requesterId = Get(archetypeIdOrName).Id;
        foreach (var definition in All.Values)
        {
            if (definition.Id == requesterId)
            {
                continue;
            }

            if (Array.Exists(definition.ExclusivePerkIds, id => string.Equals(id, perkId, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    public static string NormalizeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultId;
        }

        var normalized = value.Trim().ToLowerInvariant().Replace(' ', '_');
        return normalized switch
        {
            "skirmisher" => "ranger",
            "mystic" => "arcanist",
            _ => normalized,
        };
    }
}
