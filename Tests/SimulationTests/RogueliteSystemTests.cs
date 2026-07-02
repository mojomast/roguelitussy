using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class RogueliteSystemTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Daily seed is deterministic for same date", DailySeedIsDeterministic);
        registry.Add("Simulation.Daily score uses authored formula", DailyScoreUsesFormula);
        registry.Add("Simulation.Ascension modifiers include lower levels", AscensionModifiersIncludeLowerLevels);
        registry.Add("Simulation.RunNarrator is deterministic for same seed", RunNarratorIsDeterministic);
        registry.Add("Simulation.RunNarrator templates use allowed placeholders", RunNarratorPlaceholdersAreAllowed);
        registry.Add("Persistence.Meta progression ascension fields round-trip", MetaProgressionAscensionRoundTrips);
    }

    private static void DailySeedIsDeterministic()
    {
        var date = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        Expect.Equal(DailySeedGenerator.GetSeedForDate(date), DailySeedGenerator.GetSeedForDate(date), "Same date should produce same seed.");
        Expect.NotEqual(DailySeedGenerator.GetSeedForDate(date), DailySeedGenerator.GetSeedForDate(date.AddDays(1)), "Different dates should produce different seeds.");
    }

    private static void DailyScoreUsesFormula()
    {
        Expect.Equal(345, DailySeedGenerator.CalculateScore(3, 10, 110, 0), "Score should be floor*100 + kills*10 - turns/2 + gold.");
    }

    private static void AscensionModifiersIncludeLowerLevels()
    {
        var content = new StubContentDatabase();
        var authored = (Dictionary<string, AscensionModifier>)content.AscensionModifiers;
        authored["a01"] = new AscensionModifier { ModifierId = "a01", AscensionLevel = 1, DisplayName = "One", Description = "One", EffectType = "enemy_stat_boost", EffectValue = 0.1f };
        authored["a03"] = new AscensionModifier { ModifierId = "a03", AscensionLevel = 3, DisplayName = "Three", Description = "Three", EffectType = "shop_price_increase", EffectValue = 0.2f };

        var modifiers = AscensionModifiers.GetActiveModifiers(2, content);
        Expect.Equal(1, modifiers.Count, "Ascension level 2 should include level 1 but not level 3 modifiers.");
        Expect.Equal("a01", modifiers[0].ModifierId, "Active modifiers should be ordered by level.");
    }

    private static void RunNarratorIsDeterministic()
    {
        var content = new StubContentDatabase();
        var template = new NarrativeTemplate
        {
            TemplateId = "always",
            Condition = "always",
            SentenceTemplates = new[] { "{name} fell on floor {floor}." },
        };
        ((System.Collections.Generic.Dictionary<string, NarrativeTemplate>)content.NarrativeTemplates)["always"] = template;
        var run = new RunHistoryEntry("Ada", "trickster", 4, 7, 20, "rat", "dagger", 80, 1);

        var first = RunNarrator.GenerateEpitaph(run, content, 1234);
        var second = RunNarrator.GenerateEpitaph(run, content, 1234);

        Expect.Equal(first, second, "Same run and seed should produce identical epitaphs.");
    }

    private static void RunNarratorPlaceholdersAreAllowed()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        foreach (var template in content.NarrativeTemplates.Values)
        {
            Expect.True(RunNarrator.TemplateUsesOnlyAllowedPlaceholders(template), $"Narrative template '{template.TemplateId}' should only use allowed placeholders.");
        }
    }

    private static void MetaProgressionAscensionRoundTrips()
    {
        var data = new MetaProgressionData
        {
            EchoesTotal = 10,
            HasCompletedFirstClear = true,
            AscensionLevel = 3,
        };

        var loaded = MetaProgressionData.FromJson(data.ToJson());

        Expect.True(loaded.HasCompletedFirstClear, "First clear flag should survive JSON round-trip.");
        Expect.Equal(3, loaded.AscensionLevel, "Ascension level should survive JSON round-trip.");
    }
}
