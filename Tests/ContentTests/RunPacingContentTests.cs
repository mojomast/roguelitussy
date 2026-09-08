using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.ContentTests;

public sealed class RunPacingContentTests : ITestSuite
{
    private static readonly string[] ExpectedBossesAtDepth3 = { "boss_stone_guardian" };
    private static readonly string[] ExpectedBossesAtDepth6 = { "boss_shadow_wraith", "boss_stone_guardian" };
    private static readonly string[] ExpectedBossesAtDepth9 = { "boss_magma_titan", "boss_shadow_wraith", "boss_stone_guardian" };

    public void Register(TestRegistry registry)
    {
        registry.Add("Content.RunPacing late ordinary pools retain role variety", LateOrdinaryPoolsRetainRoleVariety);
        registry.Add("Content.RunPacing boss pools retain authored depth gates", BossPoolsRetainDepthGates);
        registry.Add("Content.RunPacing recovery outlets cover floors zero through twelve", RecoveryOutletsCoverEarlyAndLateFloors);
        registry.Add("Content.RunPacing Orin field dressing is continuous and reachable", OrinDressingIsContinuousAndReachable);
    }

    private static void LateOrdinaryPoolsRetainRoleVariety()
    {
        var content = LoadContent();
        var extendedLateRoles = new HashSet<string>(StringComparer.Ordinal)
        {
            "orc_brute",
            "cultist_healer",
            "shadow_stalker",
            "flame_elemental",
            "magma_wisp",
        };
        var starterTemplates = new HashSet<string>(StringComparer.Ordinal)
        {
            "rat",
            "goblin_archer",
            "cave_spider",
            "web_spinner",
        };

        foreach (var templateId in extendedLateRoles)
        {
            Expect.Equal(99, content.EnemyDefinitions[templateId].MaxDepth,
                $"{templateId} should remain available as an established late-run role.");
        }

        foreach (var depth in Enumerable.Range(9, 4))
        {
            var ordinary = content.GetAvailableEnemies(depth)
                .Where(enemy => enemy.Tags?.Contains("boss", StringComparer.OrdinalIgnoreCase) != true)
                .ToArray();
            var roles = ordinary.Select(enemy => enemy.BrainType).ToHashSet(StringComparer.Ordinal);

            Expect.True(ordinary.Length >= 8, $"Depth {depth} should retain at least eight ordinary encounter templates.");
            Expect.True(roles.Count >= 4, $"Depth {depth} should retain at least four implemented AI roles.");
            Expect.False(ordinary.Any(enemy => starterTemplates.Contains(enemy.TemplateId)),
                $"Depth {depth} should not restore starter creatures as late-run filler.");
            Expect.True(ordinary.All(enemy => enemy.BrainType is "melee_rusher" or "ranged_kiter" or "support" or "ambush" or "patrol_guard"),
                $"Depth {depth} ordinary templates must use known runtime AI roles.");
            Expect.True(roles.IsSupersetOf(new[] { "melee_rusher", "ranged_kiter", "support", "ambush" }),
                $"Depth {depth} should preserve melee, ranged, support, and ambush roles.");
        }
    }

    private static void BossPoolsRetainDepthGates()
    {
        var content = LoadContent();
        AssertBossPool(content, 3, ExpectedBossesAtDepth3);
        AssertBossPool(content, 6, ExpectedBossesAtDepth6);
        AssertBossPool(content, 9, ExpectedBossesAtDepth9);
    }

    private static void RecoveryOutletsCoverEarlyAndLateFloors()
    {
        var content = LoadContent();
        for (var depth = 0; depth <= 12; depth++)
        {
            var hasRecovery = content.GetAvailableNpcs(depth).Any(npc =>
                npc.MerchantOffers?.Any(offer => offer.ItemTemplateId == "potion_health") == true
                || npc.Services?.Any(service => service.Id == "field_dressing" && service.HealAmount > 0 && service.Cost > 0) == true);
            Expect.True(hasRecovery, $"Depth {depth} should have an authored merchant or field-dressing recovery outlet.");
        }
    }

    private static void OrinDressingIsContinuousAndReachable()
    {
        var content = LoadContent();
        var ilex = content.NpcTemplates["sister_ilex"].Services!.Single(service => service.Id == "field_dressing");
        var orin = content.NpcTemplates["cinder_broker_orin"].Services!.Single(service => service.Id == "field_dressing");
        Expect.Equal(22, orin.Cost, "Orin's emergency dressing should use its authored deep-route cost.");
        Expect.Equal(30, orin.HealAmount, "Orin's emergency dressing should use its authored deep-route healing.");
        Expect.True((double)orin.Cost / orin.HealAmount <= ((double)ilex.Cost / ilex.HealAmount) * 1.1,
            "Orin's recovery cost per HP should remain within 10% of Ilex's continuity rate.");

        var player = new Entity("Injured", new Position(0, 0), new Stats { HP = 10, MaxHP = 30 }, Faction.Player);
        var dialogue = content.DialogueTemplates["orin_intro"];
        foreach (var startNodeId in dialogue.StartNodeIds.Prepend(dialogue.StartNodeId).Distinct(StringComparer.Ordinal))
        {
            var options = DialogueResolver.GetOptions(dialogue.Nodes[startNodeId], player);
            Expect.True(options.Any(option => option.ActionId == "service:field_dressing"),
                $"Orin greeting '{startNodeId}' should offer injured players field dressing directly.");
        }
    }

    private static void AssertBossPool(ContentLoader content, int depth, IReadOnlyList<string> expected)
    {
        var actual = content.GetAvailableEnemies(depth)
            .Where(enemy => enemy.Tags?.Contains("boss", StringComparer.OrdinalIgnoreCase) == true)
            .Select(enemy => enemy.TemplateId)
            .ToArray();
        Expect.True(expected.SequenceEqual(actual), $"Depth {depth} boss eligibility must remain unchanged.");
    }

    private static ContentLoader LoadContent()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        Expect.True(content.IsValid, string.Join(Environment.NewLine, content.ValidationErrors));
        return content;
    }
}
