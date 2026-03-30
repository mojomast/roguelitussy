using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.AITests;

public sealed class AIBrainTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("AI.Brain attacks adjacent hostiles", AdjacentTargetsProduceAttackAction);
        registry.Add("AI.Brain chases visible hostiles", VisibleTargetsProduceChaseMove);
        registry.Add("AI.Brain flees when low health", LowHealthEnemiesRetreatFromThreats);
        registry.Add("AI.Brain patrols after idling", IdleEnemiesEventuallyPatrol);
        registry.Add("AI.Brain factory resolves supported profiles", FactoryCreatesExpectedBrains);
    }

    private static void AdjacentTargetsProduceAttackAction()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var brain = new MeleeRusherBrain();
        var enemy = CreateEnemy(new Position(2, 2));
        var player = CreatePlayer(new Position(3, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);

        Expect.True(action is AttackAction, "Adjacent hostile targets should trigger a melee attack");
    }

    private static void VisibleTargetsProduceChaseMove()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var brain = new MeleeRusherBrain();
        var enemy = CreateEnemy(new Position(2, 2));
        var player = CreatePlayer(new Position(5, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);

        Expect.True(action is MoveAction, "Visible distant targets should trigger a chase move");
        var move = (MoveAction)action;
        Expect.Equal(new Position(1, 0), move.Delta, "Chasing should advance toward the player on open ground");
    }

    private static void LowHealthEnemiesRetreatFromThreats()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var brain = new FleeingBrain();
        var enemy = CreateEnemy(new Position(3, 3), new Stats { HP = 2, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var player = CreatePlayer(new Position(4, 3));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);

        Expect.True(action is MoveAction, "Low-health enemies should choose to move away instead of attacking");
        var move = (MoveAction)action;
        var next = enemy.Position + move.Delta;
        Expect.True(next.DistanceTo(player.Position) > enemy.Position.DistanceTo(player.Position), "Fleeing should increase the distance to the hostile target");
    }

    private static void IdleEnemiesEventuallyPatrol()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var brain = new PatrolGuardBrain();
        var enemy = CreateEnemy(new Position(4, 4), new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 3 });
        var player = CreatePlayer(new Position(0, 0), viewRadius: 2);

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        IAction action = new WaitAction(enemy.Id);
        for (var i = 0; i < 3; i++)
        {
            action = brain.DecideAction(enemy, world, pathfinder);
            Expect.True(action is WaitAction, "Patrol should not start before the configured idle threshold is reached");
        }

        action = brain.DecideAction(enemy, world, pathfinder);

        Expect.True(action is MoveAction, "Idle enemies should begin patrolling after several idle turns");
    }

    private static void FactoryCreatesExpectedBrains()
    {
        Expect.True(BrainFactory.Create("melee_rusher") is MeleeRusherBrain, "Factory should resolve melee_rusher profiles");
        Expect.True(BrainFactory.Create("ranged_kiter") is RangedKiterBrain, "Factory should resolve ranged_kiter profiles");
        Expect.True(BrainFactory.Create("patrol_guard") is PatrolGuardBrain, "Factory should resolve patrol_guard profiles");
        Expect.True(BrainFactory.Create("fleeing") is FleeingBrain, "Factory should resolve fleeing profiles");
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(10, 10);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
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
}