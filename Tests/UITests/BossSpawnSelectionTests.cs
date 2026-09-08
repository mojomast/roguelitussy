using System;
using System.Collections.Generic;
using System.Linq;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class BossSpawnSelectionTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.BossSpawn depth three selects Stone Guardian", DepthThreeGuardian);
        registry.Add("UI.BossSpawn depth six never selects Magma Titan", DepthSixExcludesTitan);
        registry.Add("UI.BossSpawn depth nine selects authored bosses across seeds", DepthNineBosses);
        registry.Add("UI.BossSpawn ordinary markers exclude bosses", OrdinaryExcludesBosses);
        registry.Add("UI.BossSpawn valid fixed templates override depth and marker", FixedOverrides);
        registry.Add("UI.BossSpawn invalid fixed templates fall through to matching pool", InvalidFixedFallsThrough);
        registry.Add("UI.BossSpawn unavailable matching pools skip spawns", UnavailablePoolSkips);
        registry.Add("UI.BossSpawn same seed and depth reproduce selection", DeterministicSelection);
    }

    private static void DepthThreeGuardian()
    {
        var content = ContentLoader.LoadFromRepository();
        for (var seed = 1; seed <= 32; seed++)
        {
            var enemies = Spawn(content, seed, 3, Markers(true));
            Expect.Equal(4, enemies.Length, "Every boss marker should spawn an eligible enemy.");
            Expect.True(enemies.All(enemy => enemy.GetComponent<EnemyComponent>()!.TemplateId == "boss_stone_guardian"),
                "Depth three boss markers must select Stone Guardian.");
        }
    }

    private static void DepthSixExcludesTitan()
    {
        var content = ContentLoader.LoadFromRepository();
        for (var seed = 1; seed <= 32; seed++)
        {
            var enemies = Spawn(content, seed, 6, Markers(true));
            Expect.Equal(4, enemies.Length, "Depth six boss markers should be populated.");
            Expect.True(enemies.All(enemy => enemy.GetComponent<EnemyComponent>()!.TemplateId is "boss_stone_guardian" or "boss_shadow_wraith"),
                "Depth six must not borrow Magma Titan from depth seven or select ordinary enemies.");
        }
    }

    private static void DepthNineBosses()
    {
        var content = ContentLoader.LoadFromRepository();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        for (var seed = 1; seed <= 32; seed++)
        {
            var enemies = Spawn(content, seed, 9, Markers(true));
            Expect.Equal(4, enemies.Length, "Every depth nine boss marker should spawn.");
            foreach (var enemy in enemies)
            {
                var id = enemy.GetComponent<EnemyComponent>()!.TemplateId;
                Expect.True(content.TryGetEnemyTemplate(id, out var template) && template.Tags!.Contains("boss"),
                    "Boss markers must select boss-tagged templates.");
                selected.Add(id);
            }
        }

        Expect.True(selected.SetEquals(new[] { "boss_stone_guardian", "boss_shadow_wraith", "boss_magma_titan" }),
            "Seeded weighted selection should reach all three eligible authored bosses.");
    }

    private static void OrdinaryExcludesBosses()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var depth in new[] { 3, 6, 9 })
        {
            for (var seed = 1; seed <= 32; seed++)
            {
                var enemies = Spawn(content, seed, depth, Markers(false));
                Expect.Equal(4, enemies.Length, "Ordinary markers should still spawn eligible enemies.");
                foreach (var enemy in enemies)
                {
                    var id = enemy.GetComponent<EnemyComponent>()!.TemplateId;
                    Expect.True(content.TryGetEnemyTemplate(id, out var template)
                        && template.Tags?.Contains("boss") != true
                        && depth >= template.MinDepth && depth <= template.MaxDepth,
                        "Ordinary markers must select only nonboss templates at the actual depth.");
                }
            }
        }
    }

    private static void FixedOverrides()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var depth in new[] { 3, 100 })
        {
            var spawns = new[]
            {
                new EnemySpawnData(new Position(3, 3), "boss_magma_titan", IsBoss: true),
                new EnemySpawnData(new Position(4, 3), "rat", IsBoss: true),
                new EnemySpawnData(new Position(5, 3), "boss_magma_titan"),
                new EnemySpawnData(new Position(6, 3), "rat"),
            };
            var enemies = Spawn(content, 42, depth, spawns);
            Expect.Equal(spawns.Length, enemies.Length, "Valid fixed IDs should spawn even with an unavailable random pool.");
            foreach (var spawn in spawns)
            {
                var enemy = enemies.Single(entity => entity.Position == spawn.Position);
                Expect.Equal(spawn.TemplateId!, enemy.GetComponent<EnemyComponent>()!.TemplateId,
                    "A valid fixed ID must override both depth eligibility and the boss marker.");
            }
        }
    }

    private static void InvalidFixedFallsThrough()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var isBoss in new[] { true, false })
        {
            var markers = Markers(isBoss);
            var invalid = markers.Select(spawn => spawn with { TemplateId = "missing_enemy_template" }).ToArray();
            var expected = Spawn(content, 42, 3, markers);
            var actual = Spawn(content, 42, 3, invalid);
            Expect.Equal(4, actual.Length, "Invalid fixed IDs should fall through rather than skip eligible markers.");
            Expect.True(expected.Select(enemy => enemy.GetComponent<EnemyComponent>()!.TemplateId)
                .SequenceEqual(actual.Select(enemy => enemy.GetComponent<EnemyComponent>()!.TemplateId)),
                "Invalid fixed IDs should use the same matching pool and RNG as unspecified IDs.");
        }
    }

    private static void UnavailablePoolSkips()
    {
        var content = ContentLoader.LoadFromRepository();
        Expect.Equal(0, Spawn(content, 42, 2, Markers(true)).Length,
            "A boss marker before boss eligibility must not fall back to ordinary or deeper templates.");
        foreach (var isBoss in new[] { true, false })
        {
            Expect.Equal(0, Spawn(content, 42, 100, Markers(isBoss)).Length,
                "An exhausted authored depth pool must skip markers without fallback.");
        }
    }

    private static void DeterministicSelection()
    {
        var content = ContentLoader.LoadFromRepository();
        var markers = Markers(true).Select((spawn, index) => spawn with { IsBoss = index % 2 == 0 }).ToArray();
        foreach (var depth in new[] { 3, 6, 9 })
        {
            var first = Spawn(content, 12345, depth, markers);
            var second = Spawn(content, 12345, depth, markers);
            Expect.Equal(4, first.Length, "Mixed markers should all spawn.");
            Expect.True(first.Select(enemy => (enemy.Id, enemy.Position, enemy.GetComponent<EnemyComponent>()!.TemplateId))
                .SequenceEqual(second.Select(enemy => (enemy.Id, enemy.Position, enemy.GetComponent<EnemyComponent>()!.TemplateId))),
                "Same seed and depth must reproduce ordered enemy identities, positions, and templates.");
        }
    }

    private static EnemySpawnData[] Markers(bool isBoss) => Enumerable.Range(3, 4)
        .Select(x => new EnemySpawnData(new Position(x, 3), IsBoss: isBoss)).ToArray();

    private static IEntity[] Spawn(IContentDatabase content, int seed, int depth, EnemySpawnData[] spawns)
    {
        var manager = new GameManager();
        manager.AttachServices(new WorldState(), new TurnScheduler(), new SpawnGenerator(spawns),
            new FOVCalculator(), content, new StubSaveManager(), new EventBus());
        manager.StartNewGame(seed);
        Expect.Equal(GameManager.GameState.Playing, manager.CurrentState, "Test run should start successfully.");
        Expect.True(manager.TravelToFloor(depth), "Public floor travel should populate the requested depth.");
        Expect.Equal(depth, manager.World!.Depth, "Generated world should retain the requested authored depth.");
        return manager.World.Entities.Where(entity => entity.GetComponent<EnemyComponent>() is not null)
            .OrderBy(entity => entity.Position.X).ToArray();
    }

    private sealed class SpawnGenerator(EnemySpawnData[] spawns) : IGenerator
    {
        public LevelData GenerateLevel(WorldState world, int seed, int depth)
        {
            world.InitGrid(10, 10);
            world.Seed = seed;
            world.Depth = depth;
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    world.SetTile(new Position(x, y), TileType.Floor);
                }
            }

            var start = new Position(1, 1);
            var exit = new Position(8, 8);
            world.SetTile(start, TileType.StairsUp);
            world.SetTile(exit, TileType.StairsDown);
            return new LevelData(start, exit, spawns.Select(spawn => spawn.Position).ToArray(),
                Array.Empty<Position>(), Array.Empty<RoomData>(), EnemySpawnDetails: spawns);
        }

        public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data) => Array.Empty<string>();
    }
}
