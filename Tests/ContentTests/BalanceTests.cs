using System;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.ContentTests;

public sealed class BalanceTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Content.Budgets ramp predictably", BudgetsRampPredictably);
        registry.Add("Content.Early game remains survivable", EarlyGameRemainsSurvivable);
        registry.Add("Content.Floor loot gates high tier items by depth", FloorLootGatesHighTierItemsByDepth);
        registry.Add("Content.Loot resolution is deterministic", LootResolutionIsDeterministic);
    }

    private static void BudgetsRampPredictably()
    {
        Expect.Equal(5, DifficultyScaler.GetRecommendedEnemyCount(0), "Depth 0 should recommend five enemies");
        Expect.Equal(3, DifficultyScaler.GetRecommendedItemCount(0), "Depth 0 should recommend three items");
        Expect.Equal(13, DifficultyScaler.GetRecommendedEnemyCount(4), "Depth 4 should recommend thirteen enemies");
        Expect.Equal(7, DifficultyScaler.GetRecommendedItemCount(4), "Depth 4 should recommend seven items");
        Expect.Equal(20, DifficultyScaler.GetRecommendedEnemyCount(8), "Enemy recommendations should cap at twenty");
        Expect.Equal(10, DifficultyScaler.GetRecommendedItemCount(8), "Item recommendations should cap at ten");
    }

    private static void EarlyGameRemainsSurvivable()
    {
        var content = LoadContent();
        var rat = content.EnemyTemplates["rat"];
        var skeleton = content.EnemyTemplates["skeleton"];

        const int baselinePlayerHp = 100;
        const int baselinePlayerAttack = 8;
        const int baselinePlayerDefense = 4;

        var playerTurnsToKillRat = DifficultyScaler.EstimateTurnsToKill(baselinePlayerAttack, rat.BaseStats.HP, rat.BaseStats.Defense);
        var ratTurnsToKillPlayer = DifficultyScaler.EstimateTurnsToKill(rat.BaseStats.Attack, baselinePlayerHp, baselinePlayerDefense);
        Expect.True(playerTurnsToKillRat < ratTurnsToKillPlayer, "Baseline player should beat a Giant Rat in a straight exchange");

        var playerTurnsToKillSkeleton = DifficultyScaler.EstimateTurnsToKill(baselinePlayerAttack, skeleton.BaseStats.HP, skeleton.BaseStats.Defense);
        Expect.True(playerTurnsToKillSkeleton <= 4.0, "Baseline player should still dispatch a Skeleton in a few hits");

        var orc = content.EnemyTemplates["orc_brute"];
        var playerTurnsToKillOrc = DifficultyScaler.EstimateTurnsToKill(baselinePlayerAttack, orc.BaseStats.HP, orc.BaseStats.Defense);
        Expect.True(playerTurnsToKillOrc > playerTurnsToKillSkeleton, "Mid-game bruisers should outlast early-game skeletons");
    }

    private static void FloorLootGatesHighTierItemsByDepth()
    {
        var content = LoadContent();
        var earlyEntries = LootTableResolver.GetEligibleEntries(content, "floor_loot", depth: 0)
            .Where(entry => entry.ItemId is not null)
            .Select(entry => entry.ItemId!)
            .ToHashSet();
        var lateEntries = LootTableResolver.GetEligibleEntries(content, "floor_loot", depth: 4)
            .Where(entry => entry.ItemId is not null)
            .Select(entry => entry.ItemId!)
            .ToHashSet();

        Expect.True(earlyEntries.Contains("potion_health"), "Early floor loot should include basic sustain items");
        Expect.False(earlyEntries.Contains("sword_flame"), "Early floor loot should exclude level 5 weapons");
        Expect.False(earlyEntries.Contains("ring_regen"), "Early floor loot should exclude level 4 accessories");
        Expect.True(lateEntries.Contains("sword_flame"), "Deeper floor loot should unlock the flame sword");
        Expect.True(lateEntries.Contains("ring_regen"), "Deeper floor loot should unlock the regeneration ring");
    }

    private static void LootResolutionIsDeterministic()
    {
        var content = LoadContent();

        var seed = 123456;
        var firstRoll = LootTableResolver.RollTable(content, "floor_loot", new Random(seed), depth: 3)
            .Select(result => $"{result.ItemId}:{result.Count}")
            .ToArray();
        var secondRoll = LootTableResolver.RollTable(content, "floor_loot", new Random(seed), depth: 3)
            .Select(result => $"{result.ItemId}:{result.Count}")
            .ToArray();

        Expect.Equal(string.Join(",", firstRoll), string.Join(",", secondRoll), "Loot rolls must be deterministic for a fixed seed");
    }

    private static ContentLoader LoadContent()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        content.EnsureValid();
        return content;
    }
}