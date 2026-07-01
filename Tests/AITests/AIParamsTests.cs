using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.AITests;

public sealed class AIParamsTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("AI.Params content loader parses all authored ai_params", ContentLoaderParsesAllAiParams);
        registry.Add("AI.Params content loader rejects unknown ai_params keys", ContentLoaderRejectsUnknownAiParamsKeys);
        registry.Add("AI.Params goblin archer with preferred_range 4 stops at range 4", GoblinArcherStopsAtPreferredRange4);
        registry.Add("AI.Params aggro_range overrides view radius for target acquisition", AggroRangeOverridesViewRadius);
        registry.Add("AI.Params flee_hp_pct triggers retreat", FleeHpPctTriggersRetreat);
        registry.Add("AI.Params wander_when_idle false prevents patrolling", WanderWhenIdleFalsePreventsPatrol);
        registry.Add("AI.Params patrol_radius is honored by patrol target selection", PatrolRadiusIsHonored);
        registry.Add("AI.Params support_range gates ally buffs", SupportRangeGatesAllyBuffs);
        registry.Add("AI.Params min_range keeps enemy at distance", MinRangeKeepsEnemyAtDistance);
        registry.Add("AI.Params group_aggro_range pulls nearby allies into combat", GroupAggroRangePullsNearbyAllies);
        registry.Add("AI.Params phase_through_walls applies phased status", PhaseThroughWallsAppliesPhasedStatus);
        registry.Add("AI.Params phase_through_walls pathfinder finds routes through walls", PhaseThroughWallsPathfinderRoutesThroughWalls);
    }

    private static void ContentLoaderParsesAllAiParams()
    {
        var content = LoadContent();

        Expect.True(content.TryGetEnemyTemplate("goblin_archer", out var archer), "goblin_archer template should load");
        Expect.Equal(4, archer.AIParameters.PreferredRange!.Value, "preferred_range should project from ai_params");
        Expect.Equal(20f, archer.AIParameters.FleeHpPct!.Value, "flee_hp_pct should project from ai_params");
        Expect.Equal(3, archer.AIParameters.MinRange!.Value, "min_range should project from ai_params");
        Expect.Equal(10, archer.AIParameters.AggroRange!.Value, "aggro_range should project from ai_params");
        Expect.True(archer.AIParameters.WanderWhenIdle!.Value, "wander_when_idle should project from ai_params");

        Expect.True(content.TryGetEnemyTemplate("wraith", out var wraith), "wraith template should load");
        Expect.True(wraith.AIParameters.PhaseThroughWalls!.Value, "phase_through_walls should project from ai_params");

        Expect.True(content.TryGetEnemyTemplate("skeleton_knight", out var knight), "skeleton_knight template should load");
        Expect.Equal(6, knight.AIParameters.PatrolRadius!.Value, "patrol_radius should project from ai_params");

        Expect.True(content.TryGetEnemyTemplate("goblin_shaman", out var shaman), "goblin_shaman template should load");
        Expect.Equal(6, shaman.AIParameters.SupportRange!.Value, "support_range should project from ai_params");
        Expect.Equal(6, shaman.AIParameters.GroupAggroRange!.Value, "group_aggro_range should project from ai_params");
    }

    private static void ContentLoaderRejectsUnknownAiParamsKeys()
    {
        using var sandbox = ContentSandbox.Create();
        var json = """
        {
          "$schema": "roguelike-enemies-v1",
          "version": 1,
          "enemies": [
            {
              "id": "bad_dummy",
              "name": "Bad Dummy",
              "description": "Has an unknown ai_param.",
              "stats": { "hp": 10, "attack": 1, "defense": 0, "accuracy": 50, "evasion": 0, "speed": 100, "fov_range": 6, "xp_value": 1 },
              "ai_type": "melee_rush",
              "ai_params": { "unknown_key": 1 },
              "faction": "Enemy",
              "min_depth": 0,
              "max_depth": 1,
              "spawn_weight": 1,
              "abilities": [],
              "gold_min": 0,
              "gold_max": 0,
              "tags": [],
              "sprite_path": "res://Assets/Sprites/0x72/Imp_Idle_1.png",
              "sprite_atlas_coords": [0, 0]
            }
          ]
        }
        """;

        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "enemies.json"), json);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "abilities.json"), EmptyAbilitiesJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "items.json"), EmptyItemsJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "perks.json"), EmptyPerksJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "dialogs.json"), EmptyDialogsJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "npcs.json"), EmptyNpcsJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "status_effects.json"), EmptyStatusEffectsJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "room_prefabs.json"), EmptyRoomPrefabsJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "loot_tables.json"), EmptyLootTablesJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "traps.json"), EmptyTrapsJson());
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "relics.json"), "[]");
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, "floor_events.json"), "[]");

        var content = ContentLoader.LoadFromDirectory(sandbox.DirectoryPath, throwOnValidationErrors: false);
        Expect.False(content.IsValid, "Unknown ai_params key should fail validation");
        Expect.True(content.ValidationErrors.Any(error => error.Contains("unknown_key")), "Validation error should name the unknown key");
    }

    private static void GoblinArcherStopsAtPreferredRange4()
    {
        var content = LoadContent();
        var world = CreateWorld();
        var pathfinder = new Pathfinder();

        Expect.True(content.TryGetEnemyTemplate("goblin_archer", out var template), "goblin_archer template should exist");
        var archer = CreateEnemyFromTemplate(template, new Position(2, 2));
        var player = CreatePlayer(new Position(10, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(archer);

        var finalDistance = SimulateUntilStationary(archer, world, pathfinder, maxTurns: 30);

        Expect.Equal(4, finalDistance, "Goblin archer with preferred_range 4 should stop 4 tiles away from the target");
    }

    private static void AggroRangeOverridesViewRadius()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var profile = AIProfiles.MeleeRusher with { AggroRange = 3 };
        var brain = new MeleeRusherBrain(profile);

        var enemy = CreateEnemy(new Position(5, 5), new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var farPlayer = CreatePlayer(new Position(9, 5));

        world.Player = farPlayer;
        world.AddEntity(farPlayer);
        world.AddEntity(enemy);

        var farAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(farAction is WaitAction, "Enemy should ignore target beyond aggro_range");

        world.RemoveEntity(farPlayer.Id);
        var nearPlayer = CreatePlayer(new Position(7, 5));
        world.Player = nearPlayer;
        world.AddEntity(nearPlayer);

        var nearAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(nearAction is MoveAction, "Enemy should chase target within aggro_range");
    }

    private static void FleeHpPctTriggersRetreat()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var profile = AIProfiles.MeleeRusher with { FleeThreshold = 0.50f, CanFlee = true };
        var brain = new MeleeRusherBrain(profile);

        var enemy = CreateEnemy(new Position(3, 3), new Stats { HP = 5, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var player = CreatePlayer(new Position(4, 3));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(action is MoveAction, "Enemy at or below flee_hp_pct should flee instead of attacking");
        var move = (MoveAction)action;
        var next = enemy.Position + move.Delta;
        Expect.True(next.DistanceTo(player.Position) > enemy.Position.DistanceTo(player.Position), "Fleeing should increase distance");
    }

    private static void WanderWhenIdleFalsePreventsPatrol()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var profile = AIProfiles.PatrolGuard with { PatrolsWhenIdle = false };
        var brain = new PatrolGuardBrain(profile);

        var enemy = CreateEnemy(new Position(4, 4), new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 3 });
        var player = CreatePlayer(new Position(0, 0), viewRadius: 2);

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        for (var i = 0; i < 10; i++)
        {
            var action = brain.DecideAction(enemy, world, pathfinder);
            Expect.True(action is WaitAction, "Enemy with wander_when_idle false should never patrol");
        }
    }

    private static void PatrolRadiusIsHonored()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var profile = AIProfiles.PatrolGuard with { PatrolsWhenIdle = true, PatrolRadius = 2, IdleTurnsBeforePatrol = 2 };
        var brain = new PatrolGuardBrain(profile);

        var enemy = CreateEnemy(new Position(5, 5), new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 3 });
        var player = CreatePlayer(new Position(0, 0), viewRadius: 2);

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        for (var i = 0; i < 2; i++)
        {
            brain.DecideAction(enemy, world, pathfinder);
        }

        var action = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(action is MoveAction, "Patrol should begin after idle threshold");

        var memory = enemy.GetComponent<AIStateComponent>()!;
        var patrolTarget = memory.PatrolTarget;
        Expect.True(patrolTarget != Position.Invalid, "Patrol target should be set");
        var distance = Math.Max(Math.Abs(patrolTarget.X - enemy.Position.X), Math.Abs(patrolTarget.Y - enemy.Position.Y));
        Expect.True(distance <= 2, "Patrol target should respect patrol_radius");
    }

    private static void SupportRangeGatesAllyBuffs()
    {
        var world = CreateWorldWithContent();
        var pathfinder = new Pathfinder();
        var profile = AIProfiles.Support with { SupportRange = 2 };
        var brain = new SupportBrain(profile);

        var enemy = CreateEnemy(new Position(3, 3));
        var abilitiesComp = new AbilitiesComponent();
        abilitiesComp.Slots.Add(new EnemyAbilitySlot { AbilityId = "war_cry", Cooldown = 3, Priority = 90 });
        enemy.SetComponent(abilitiesComp);
        enemy.SetComponent(new CooldownComponent());

        var farAlly = new StubEntity("Ally", new Position(6, 3), Faction.Enemy, stats: new Stats { HP = 5, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var player = CreatePlayer(new Position(9, 3));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);
        world.AddEntity(farAlly);

        var farAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(farAction is not CastAbilityAction, "Support brain should not buff when no allies are within support_range");

        world.RemoveEntity(farAlly.Id);
        var nearAlly = new StubEntity("Ally", new Position(4, 3), Faction.Enemy, stats: new Stats { HP = 5, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        world.AddEntity(nearAlly);

        var nearAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(nearAction is CastAbilityAction, "Support brain should buff when allies are within support_range");
    }

    private static void MinRangeKeepsEnemyAtDistance()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var profile = AIProfiles.RangedKiter with { PreferredRange = 4, MinRange = 3 };
        var brain = new RangedKiterBrain(profile);

        var enemy = CreateEnemy(new Position(5, 5), new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var player = CreatePlayer(new Position(6, 5));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(action is MoveAction, "Enemy closer than min_range should move away");
        var move = (MoveAction)action;
        var next = enemy.Position + move.Delta;
        Expect.True(next.DistanceTo(player.Position) >= enemy.Position.DistanceTo(player.Position), "Moving away should not reduce distance");
    }

    private static void GroupAggroRangePullsNearbyAllies()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();

        var profileLeader = AIProfiles.MeleeRusher with { AggroRange = 10 };
        var leader = new MeleeRusherBrain(profileLeader);

        var profileFollower = AIProfiles.MeleeRusher with { AggroRange = 2, GroupAggroRange = 6 };
        var follower = new MeleeRusherBrain(profileFollower);

        var leaderEntity = CreateEnemy(new Position(8, 5), new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        leaderEntity.SetComponent<IBrain>(leader);
        var followerEntity = CreateEnemy(new Position(12, 5), new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        followerEntity.SetComponent<IBrain>(follower);
        var player = CreatePlayer(new Position(5, 5));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(leaderEntity);
        world.AddEntity(followerEntity);

        var leaderAction = leader.DecideAction(leaderEntity, world, pathfinder);
        Expect.True(leaderAction is MoveAction, "Leader should aggro the player");

        var followerAction = follower.DecideAction(followerEntity, world, pathfinder);
        Expect.True(followerAction is MoveAction, "Follower outside its own aggro range should still chase via group aggro");
    }

    private static void PhaseThroughWallsAppliesPhasedStatus()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var profile = AIProfiles.Ambush with { PhaseThroughWalls = true, AggroRange = 6 };
        var brain = new AmbushBrain(profile);

        var enemy = CreateEnemy(new Position(2, 2), new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var player = CreatePlayer(new Position(4, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        Expect.False(StatusEffectProcessor.HasEffect(enemy, StatusEffectType.Phased), "Enemy should not start phased");

        brain.DecideAction(enemy, world, pathfinder);

        Expect.True(StatusEffectProcessor.HasEffect(enemy, StatusEffectType.Phased), "phase_through_walls should apply a permanent phased status");
    }

    private static void PhaseThroughWallsPathfinderRoutesThroughWalls()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 3; x <= 5; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }

        var start = new Position(1, 5);
        var goal = new Position(7, 5);

        var normalPath = pathfinder.FindPath(start, goal, world, 20, phaseThroughWalls: false);
        Expect.True(normalPath.Count == 0, "Normal pathfinder should not route through a wall");

        var phasedPath = pathfinder.FindPath(start, goal, world, 20, phaseThroughWalls: true);
        Expect.True(phasedPath.Count > 0, "Phased pathfinder should route through a wall");
    }

    private static int SimulateUntilStationary(IEntity enemy, WorldState world, IPathfinder pathfinder, int maxTurns)
    {
        var brain = enemy.GetComponent<IBrain>()!;
        var previousPosition = enemy.Position;
        var stationaryTurns = 0;

        for (var i = 0; i < maxTurns; i++)
        {
            var action = brain.DecideAction(enemy, world, pathfinder);
            if (action is MoveAction move)
            {
                var from = enemy.Position;
                var target = from + move.Delta;
                enemy.Position = target;
                world.UpdateEntityPosition(enemy.Id, from, target);
            }

            if (enemy.Position == previousPosition)
            {
                stationaryTurns++;
                if (stationaryTurns >= 2)
                {
                    break;
                }
            }
            else
            {
                stationaryTurns = 0;
                previousPosition = enemy.Position;
            }
        }

        var player = world.Player;
        return enemy.Position.DistanceTo(player.Position);
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(20, 20);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static WorldState CreateWorldWithContent()
    {
        var world = CreateWorld();
        world.ContentDatabase = new StubContentDatabase();
        return world;
    }

    private static StubEntity CreateEnemyFromTemplate(EnemyTemplate template, Position position)
    {
        var entity = new StubEntity(
            template.DisplayName,
            position,
            template.Faction,
            stats: template.BaseStats.Clone());
        entity.SetComponent<IBrain>(BrainFactory.Create(template));
        entity.SetComponent(new XpValueComponent { Value = template.XpValue });
        entity.SetComponent(new EnemyComponent { TemplateId = template.TemplateId });
        return entity;
    }

    private static StubEntity CreateEnemy(Position position, Stats? stats = null)
    {
        return new StubEntity(
            "Enemy",
            position,
            Faction.Enemy,
            stats: stats ?? new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
    }

    private static StubEntity CreatePlayer(Position position, int viewRadius = 8)
    {
        return new StubEntity(
            "Player",
            position,
            Faction.Player,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 5, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = viewRadius });
    }

    private static ContentLoader LoadContent()
    {
        return ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
    }

    private sealed class ContentSandbox : IDisposable
    {
        private ContentSandbox(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public string DirectoryPath { get; }

        public static ContentSandbox Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-ai-params-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new ContentSandbox(directoryPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }

    private static string EmptyAbilitiesJson() => """
    {
      "$schema": "roguelike-abilities-v1",
      "version": 1,
      "abilities": []
    }
    """;

    private static string EmptyItemsJson() => """
    {
      "$schema": "roguelike-items-v1",
      "version": 1,
      "items": []
    }
    """;

    private static string EmptyPerksJson() => """
    {
      "$schema": "roguelike-perks-v1",
      "version": 1,
      "perks": []
    }
    """;

    private static string EmptyDialogsJson() => """
    {
      "$schema": "roguelike-dialogs-v1",
      "version": 1,
      "dialogs": []
    }
    """;

    private static string EmptyNpcsJson() => """
    {
      "$schema": "roguelike-npcs-v1",
      "version": 1,
      "npcs": []
    }
    """;

    private static string EmptyStatusEffectsJson() => """
    {
      "$schema": "roguelike-status-effects-v1",
      "version": 1,
      "status_effects": []
    }
    """;

    private static string EmptyRoomPrefabsJson() => """
    {
      "$schema": "roguelike-room-prefabs-v1",
      "version": 1,
      "tile_legend": { "#": "wall", ".": "floor" },
      "rooms": []
    }
    """;

    private static string EmptyLootTablesJson() => """
    {
      "$schema": "roguelike-loot-tables-v1",
      "version": 1,
      "loot_tables": []
    }
    """;

    private static string EmptyTrapsJson() => """
    {
      "$schema": "roguelike-traps-v1",
      "version": 1,
      "traps": []
    }
    """;
}
