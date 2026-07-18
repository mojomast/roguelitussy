using System.Collections.Generic;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.AITests;

public sealed class AIFixTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("AI.Solo enemies are not penalized for self-buffs", SoloEnemiesCanSelfBuff);
        registry.Add("AI.Support profile still requires nearby allies to buff", SupportStillRequiresAllies);
        registry.Add("AI.Melee and patrol profiles default to group aggro", BaseProfilesHaveGroupAggro);
        registry.Add("AI.Group aggro pulls packmates onto a shared target", GroupAggroPullsPackmates);
        registry.Add("AI.Patrol retargeting uses a single reachability sweep", PatrolRetargetUsesSingleSweep);
        registry.Add("AI.Pathfinder routes through ally-occupied corridor tiles", PathfinderRoutesThroughAllies);
    }

    private static void SoloEnemiesCanSelfBuff()
    {
        var world = CreateWorld(12, 12);
        world.ContentDatabase = new StubContentDatabase();
        var pathfinder = new Pathfinder();

        var self = CreateEnemy(new Position(2, 2));
        var abilities = new AbilitiesComponent();
        abilities.Slots.Add(new EnemyAbilitySlot { AbilityId = "phase_shift", Cooldown = 3, Priority = 80 });
        self.SetComponent(abilities);

        var target = CreatePlayer(new Position(5, 2));
        world.Player = target;
        world.AddEntity(target);
        world.AddEntity(self);

        var contentDb = (IContentDatabase)world.ContentDatabase!;
        Expect.True(contentDb.TryGetAbilityTemplate("phase_shift", out var template), "Stub content should provide phase_shift.");

        var memory = new AIStateComponent { State = AIState.Chase };
        var cast = new CastAbilityAction(self.Id, template, self.Position);
        var score = UtilityScorer.ScoreAction(cast, self, target, target.Position, world, memory, AIProfiles.MeleeRusher, pathfinder);

        Expect.True(score > 1.0f, $"A solo melee enemy should score self-buffs positively, got {score}.");
    }

    private static void SupportStillRequiresAllies()
    {
        var world = CreateWorld(12, 12);
        world.ContentDatabase = new StubContentDatabase();
        var pathfinder = new Pathfinder();

        var self = CreateEnemy(new Position(2, 2));
        var abilities = new AbilitiesComponent();
        abilities.Slots.Add(new EnemyAbilitySlot { AbilityId = "phase_shift", Cooldown = 3, Priority = 80 });
        self.SetComponent(abilities);

        var target = CreatePlayer(new Position(5, 2));
        world.Player = target;
        world.AddEntity(target);
        world.AddEntity(self);

        var contentDb = (IContentDatabase)world.ContentDatabase!;
        contentDb.TryGetAbilityTemplate("phase_shift", out var template);

        var memory = new AIStateComponent { State = AIState.Chase };
        var cast = new CastAbilityAction(self.Id, template, self.Position);
        var score = UtilityScorer.ScoreAction(cast, self, target, target.Position, world, memory, AIProfiles.Support, pathfinder);

        Expect.True(score < 0f, $"A support with no allies in range should still be penalized for self casts, got {score}.");
    }

    private static void BaseProfilesHaveGroupAggro()
    {
        Expect.True(AIProfiles.MeleeRusher.GroupAggroRange > 0, "Melee rushers should share aggro with nearby packmates by default.");
        Expect.True(AIProfiles.PatrolGuard.GroupAggroRange > 0, "Patrol guards should share aggro with nearby packmates by default.");
    }

    private static void GroupAggroPullsPackmates()
    {
        var world = CreateWorld(12, 12);
        var pathfinder = new Pathfinder();

        // Wall column blocks the follower's line of sight to the player.
        for (var y = 0; y < world.Height; y++)
        {
            if (y != 10)
            {
                world.SetTile(new Position(6, y), TileType.Wall);
            }
        }

        var player = CreatePlayer(new Position(9, 2));
        var alerted = CreateEnemy(new Position(5, 2));
        var follower = CreateEnemy(new Position(4, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(alerted);
        world.AddEntity(follower);

        alerted.SetComponent(new AIStateComponent { State = AIState.Chase, TargetId = player.Id, LastKnownTargetPosition = player.Position });

        var brain = new AIBrain(AIProfiles.MeleeRusher);
        var action = brain.DecideAction(follower, world, pathfinder);

        Expect.True(action is MoveAction, "A packmate near an alerted ally should chase the shared target instead of idling.");
        var memory = follower.GetComponent<AIStateComponent>();
        Expect.True(memory is not null && memory.TargetId == player.Id, "Group aggro should record the shared target.");
    }

    private static void PatrolRetargetUsesSingleSweep()
    {
        var world = CreateWorld(14, 14);
        var pathfinder = new CountingPathfinder(new Pathfinder());
        var guard = CreateEnemy(new Position(6, 6));

        var target = AIStateManager.GetPatrolTarget(guard, world, pathfinder, AIProfiles.PatrolGuard);

        Expect.False(target == Position.Invalid, "Patrol retargeting should still find a destination.");
        Expect.True(System.Math.Abs(target.X - 6) <= AIProfiles.PatrolGuard.PatrolRadius, "Patrol target should stay within the patrol radius on X.");
        Expect.True(System.Math.Abs(target.Y - 6) <= AIProfiles.PatrolGuard.PatrolRadius, "Patrol target should stay within the patrol radius on Y.");
        Expect.Equal(1, pathfinder.GetReachableCalls, "Retargeting should perform exactly one reachability sweep.");
        Expect.Equal(0, pathfinder.FindPathCalls, "Retargeting should not run per-candidate A* searches.");
    }

    private static void PathfinderRoutesThroughAllies()
    {
        var world = new WorldState();
        world.InitGrid(9, 3);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }

        for (var x = 1; x <= 7; x++)
        {
            world.SetTile(new Position(x, 1), TileType.Floor);
        }

        var ally = CreateEnemy(new Position(4, 1));
        world.AddEntity(ally);

        var pathfinder = new Pathfinder();
        var path = pathfinder.FindPath(new Position(1, 1), new Position(7, 1), world, 30);

        Expect.True(path.Count > 0, "An ally standing in a one-wide corridor should not make the far side unreachable.");
        Expect.True(path.Contains(new Position(4, 1)), "The path should pass through the ally-occupied tile at extra cost.");

        var reachable = pathfinder.GetReachable(new Position(1, 1), 10, world);
        Expect.False(reachable.ContainsKey(new Position(5, 1)), "Reachability sweeps should still treat occupied tiles as blocking.");
    }

    private static WorldState CreateWorld(int width, int height)
    {
        var world = new WorldState();
        world.InitGrid(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreateEnemy(Position position)
    {
        return new StubEntity(
            "Enemy",
            position,
            Faction.Enemy,
            stats: new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
    }

    private static StubEntity CreatePlayer(Position position)
    {
        return new StubEntity(
            "Player",
            position,
            Faction.Player,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 5, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
    }

    private sealed class CountingPathfinder : IPathfinder
    {
        private readonly IPathfinder _inner;

        public CountingPathfinder(IPathfinder inner)
        {
            _inner = inner;
        }

        public int FindPathCalls { get; private set; }

        public int GetReachableCalls { get; private set; }

        public IReadOnlyList<Position> FindPath(Position start, Position goal, IWorldState world, int maxLength = 50, bool phaseThroughWalls = false)
        {
            FindPathCalls++;
            return _inner.FindPath(start, goal, world, maxLength, phaseThroughWalls);
        }

        public bool HasPath(Position start, Position goal, IWorldState world, int maxLength = 50, bool phaseThroughWalls = false)
        {
            FindPathCalls++;
            return _inner.HasPath(start, goal, world, maxLength, phaseThroughWalls);
        }

        public IReadOnlyDictionary<Position, int> GetReachable(Position origin, int range, IWorldState world, bool phaseThroughWalls = false)
        {
            GetReachableCalls++;
            return _inner.GetReachable(origin, range, world, phaseThroughWalls);
        }
    }
}
