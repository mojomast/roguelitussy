using System;
using System.Linq;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.GenerationTests;

public sealed class LateEncounterVarietyTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Generation.Late encounters preserve ordinary roles across seeded floors", SeededLateFloorsPreserveOrdinaryRoles);
    }

    private static void SeededLateFloorsPreserveOrdinaryRoles()
    {
        var content = ContentLoader.LoadFromRepository();
        var observedRoles = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        for (var seed = 1; seed <= 32; seed++)
        {
            var manager = new GameManager();
            manager.AttachServices(new WorldState(), new TurnScheduler(), new DungeonGenerator(), new FOVCalculator(),
                content, new StubSaveManager(), new EventBus());
            manager.StartNewGame(seed);

            foreach (var depth in new[] { 11, 12 })
            {
                Expect.True(manager.TravelToFloor(depth), $"Seed {seed} should generate late floor {depth}.");
                var ordinaryMarkers = manager.World!.Entities
                    .Where(entity => entity.GetComponent<EnemyComponent>() is not null)
                    .Where(entity => entity.GetComponent<EnemyComponent>()!.TemplateId.StartsWith("boss_", StringComparison.Ordinal) == false)
                    .ToArray();

                Expect.True(ordinaryMarkers.Length > 0, $"Seed {seed}, depth {depth} should retain ordinary encounter spawns.");
                foreach (var enemy in ordinaryMarkers)
                {
                    var templateId = enemy.GetComponent<EnemyComponent>()!.TemplateId;
                    Expect.True(content.TryGetEnemyTemplate(templateId, out var template), "Spawned enemies must retain known templates.");
                    Expect.False(template.Tags?.Contains("boss", StringComparer.OrdinalIgnoreCase) == true,
                        $"Seed {seed}, depth {depth} ordinary markers must not resolve to bosses.");
                    observedRoles.Add(template.BrainType);
                }
            }
        }

        Expect.True(observedRoles.Count >= 3,
            "Thirty-two seeded late floors should produce at least three ordinary runtime AI roles.");
    }
}
