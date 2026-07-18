using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.ContentTests;

public sealed class ContentIntegrityTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Content.Loot tables never drop placeholder items", LootTablesNeverDropPlaceholderItems);
        registry.Add("Content.Boss phase statuses resolve to authored status effects", BossPhaseStatusesResolve);
        registry.Add("Content.Generation trap ids stay defined", GenerationTrapIdsStayDefined);
        registry.Add("Content.Magma boss is authored with phases", MagmaBossIsAuthoredWithPhases);
        registry.Add("Content.Descriptions carry no placeholder markers", DescriptionsCarryNoPlaceholderMarkers);
        registry.Add("Content.New status effects support hazard avoidance and regen", NewStatusEffectsSupportHazardAvoidanceAndRegen);
    }

    private static void LootTablesNeverDropPlaceholderItems()
    {
        var content = LoadContent();

        foreach (var table in content.LootTables.Values)
        {
            foreach (var entry in table.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.ItemId))
                {
                    continue;
                }

                Expect.False(
                    string.Equals(entry.ItemId, "relic_item_slot", System.StringComparison.Ordinal),
                    $"Loot table '{table.Id}' must not drop the reserved relic cache token.");

                Expect.True(
                    content.ItemDefinitions.TryGetValue(entry.ItemId!, out var item),
                    $"Loot table '{table.Id}' entry '{entry.ItemId}' must reference a defined item.");

                Expect.False(
                    item!.Description.Contains("placeholder", System.StringComparison.OrdinalIgnoreCase),
                    $"Loot table '{table.Id}' must not drop placeholder-described item '{entry.ItemId}'.");
            }
        }
    }

    private static void BossPhaseStatusesResolve()
    {
        var content = LoadContent();
        var bosses = content.EnemyDefinitions.Values
            .Where(enemy => enemy.BossPhaseData.Count > 0)
            .ToArray();

        Expect.True(bosses.Length >= 3, "Expected at least three phased bosses to be authored.");

        foreach (var boss in bosses)
        {
            foreach (var phase in boss.BossPhaseData)
            {
                if (string.IsNullOrWhiteSpace(phase.StatusEffect))
                {
                    continue;
                }

                Expect.True(
                    content.StatusEffects.ContainsKey(phase.StatusEffect),
                    $"Boss '{boss.Id}' phase {phase.Phase} status '{phase.StatusEffect}' must resolve against status_effects.json.");
            }
        }
    }

    private static void GenerationTrapIdsStayDefined()
    {
        var content = LoadContent();

        foreach (var trapId in new[] { "spike_trap", "trap_poison_gas", "trap_alarm", "trap_teleport", "trap_gold_drain" })
        {
            Expect.True(content.TrapDefinitions.ContainsKey(trapId), $"Generation-facing trap '{trapId}' must stay defined.");
            Expect.True(content.TryGetTrapTemplate(trapId, out _), $"Generation-facing trap '{trapId}' must project into the trap surface.");
        }

        Expect.False(content.TrapDefinitions.ContainsKey("trap_spike"), "The duplicate 'trap_spike' definition must stay deleted.");
    }

    private static void MagmaBossIsAuthoredWithPhases()
    {
        var content = LoadContent();

        Expect.True(content.EnemyDefinitions.TryGetValue("boss_magma_titan", out var titan), "The magma-tier boss must be authored.");
        Expect.Equal("Magma Titan", titan!.Name, "The magma boss name must match the death narrative condition value.");
        Expect.True(titan.MinDepth >= 7, "The Magma Titan belongs to the magma tier (depth 7+).");
        Expect.True(titan.BossPhaseData.Count >= 2, "The Magma Titan must have boss phases like the other bosses.");
        Expect.True(titan.Tags.Contains("boss"), "The Magma Titan must be tagged as a boss.");

        Expect.True(content.EnemyDefinitions.TryGetValue("boss_stone_guardian", out var guardian), "The stone guardian boss must remain authored.");
        Expect.True(titan.Stats.HP > guardian!.Stats.HP, "The Magma Titan must out-scale the Stone Guardian's HP.");
        Expect.True(titan.Stats.Attack > guardian.Stats.Attack, "The Magma Titan must out-scale the Stone Guardian's attack.");

        Expect.True(
            content.NarrativeTemplates.Values.Any(template =>
                string.Equals(template.Condition, "cause_of_death", System.StringComparison.Ordinal)
                && string.Equals(template.ConditionValue, "magma titan", System.StringComparison.Ordinal)),
            "The magma boss death narrative must stay wired to the Magma Titan.");
    }

    private static void DescriptionsCarryNoPlaceholderMarkers()
    {
        var content = LoadContent();

        foreach (var item in content.ItemDefinitions.Values)
        {
            ExpectHonestText(item.Description, $"Item '{item.Id}' description");
        }

        foreach (var enemy in content.EnemyDefinitions.Values)
        {
            ExpectHonestText(enemy.Description, $"Enemy '{enemy.Id}' description");
        }

        foreach (var perk in content.PerkDefinitions.Values)
        {
            ExpectHonestText(perk.Description, $"Perk '{perk.Id}' description");
        }

        foreach (var trap in content.TrapDefinitions.Values)
        {
            ExpectHonestText(trap.Description, $"Trap '{trap.Id}' description");
        }
    }

    private static void NewStatusEffectsSupportHazardAvoidanceAndRegen()
    {
        var content = LoadContent();

        Expect.True(content.StatusEffects.TryGetValue("flying", out var flying), "The 'flying' status must be authored for trap avoid_flags.");
        Expect.True(flying!.Flags.Contains("flying"), "The 'flying' status must carry the 'flying' flag for hazard avoidance.");

        Expect.True(content.StatusEffects.TryGetValue("regenerating", out var regenerating), "The 'regenerating' status must be authored.");
        Expect.True(
            regenerating!.TickEffects.Any(tick => string.Equals(tick.Type, "heal", System.StringComparison.Ordinal) && tick.Value > 0),
            "The 'regenerating' status must heal over time.");

        foreach (var trap in content.TrapDefinitions.Values)
        {
            foreach (var flag in trap.AvoidFlags)
            {
                var known = flag is "phased" or "flying";
                Expect.True(known, $"Trap '{trap.Id}' avoid flag '{flag}' must map onto an authored status.");
            }
        }
    }

    private static void ExpectHonestText(string text, string label)
    {
        Expect.False(string.IsNullOrWhiteSpace(text), $"{label} must not be empty.");
        foreach (var marker in new[] { "placeholder", "runtime support", "once support", "TODO", "TBD" })
        {
            Expect.False(
                text.Contains(marker, System.StringComparison.OrdinalIgnoreCase),
                $"{label} must not promise unimplemented mechanics (found '{marker}').");
        }
    }

    private static ContentLoader LoadContent()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();
        return content;
    }
}
