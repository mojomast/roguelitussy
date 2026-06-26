using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.AITests;

public sealed class AIBrainTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("AI.Brain attacks adjacent hostiles", AdjacentTargetsProduceAttackAction);
        registry.Add("AI.Brain ignores neutral chests when acquiring targets", IgnoresNeutralChestsWhenAcquiringTargets);
        registry.Add("AI.Brain chases visible hostiles", VisibleTargetsProduceChaseMove);
        registry.Add("AI.Brain flees when low health", LowHealthEnemiesRetreatFromThreats);
        registry.Add("AI.Brain patrols after idling", IdleEnemiesEventuallyPatrol);
        registry.Add("AI.Brain factory resolves supported profiles", FactoryCreatesExpectedBrains);
        registry.Add("AI.Brain ambusher waits behind cover and strikes when player enters corridor", AmbusherWaitsBehindCoverAndStrikes);
        registry.Add("AI.Brain kiter holds preferred range and retreats when closed", KiterHoldsPreferredRange);
        registry.Add("AI.Brain support moves toward distant ally and stays near cluster", SupportMovesTowardAllies);
        registry.Add("AI.Brain group aggro shares target with ally outside line of sight", GroupAggroSharesTarget);
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

    private static void IgnoresNeutralChestsWhenAcquiringTargets()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var brain = new MeleeRusherBrain();
        var enemy = CreateEnemy(new Position(2, 2));
        var player = CreatePlayer(new Position(5, 2));
        var chest = new Entity("Treasure Chest", new Position(3, 2), new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 0, ViewRadius = 0 }, Faction.Neutral);
        chest.SetComponent(new ChestComponent { LootTableId = "chest_loot" });

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);
        world.AddEntity(chest);

        var action = brain.DecideAction(enemy, world, pathfinder);

        Expect.False(action is AttackAction attack && attack.TargetId == chest.Id, "AI must not attack adjacent neutral chests.");
        Expect.True(action is MoveAction, "AI should ignore the chest and chase the visible player instead.");
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

    private static void AmbusherWaitsBehindCoverAndStrikes()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var profile = AIProfiles.Ambush with { AggroRange = 6 };
        var brain = new AmbushBrain(profile);

        for (var y = 0; y < world.Height; y++)
        {
            world.SetTile(new Position(4, y), TileType.Wall);
        }

        world.SetTile(new Position(4, 4), TileType.Floor);

        var enemy = CreateEnemy(new Position(3, 5));
        var player = CreatePlayer(new Position(5, 3));
        var memory = AIStateManager.GetOrCreate(enemy);
        memory.LastKnownTargetPosition = player.Position;

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var action = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(action is MoveAction, "Ambusher should move toward a chokepoint when the target is not visible");
        var move = (MoveAction)action;
        var next = enemy.Position + move.Delta;
        Expect.True(next.Y < enemy.Position.Y, "Ambusher should move toward the corridor opening");

        world.RemoveEntity(player.Id);
        var visiblePlayer = CreatePlayer(new Position(3, 3));
        world.Player = visiblePlayer;
        world.AddEntity(visiblePlayer);

        var strikeAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(strikeAction is AttackAction || strikeAction is MoveAction, "Ambusher should strike or close when the target becomes visible");
    }

    private static void KiterHoldsPreferredRange()
    {
        var world = CreateWorld();
        world.InitGrid(12, 12);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var pathfinder = new Pathfinder();
        var profile = AIProfiles.RangedKiter with { PreferredRange = 4, MinRange = 2, AggroRange = 10 };
        var brain = new RangedKiterBrain(profile);

        var enemy = CreateEnemy(new Position(2, 2));
        var player = CreatePlayer(new Position(6, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var holdAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(holdAction is WaitAction, "Kiter already at preferred range should hold position");

        world.RemoveEntity(player.Id);
        var closerPlayer = CreatePlayer(new Position(5, 2));
        world.Player = closerPlayer;
        world.AddEntity(closerPlayer);

        var retreatAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(retreatAction is MoveAction, "Kiter inside preferred range should retreat");
        var retreatMove = (MoveAction)retreatAction;
        var retreatNext = enemy.Position + retreatMove.Delta;
        Expect.True(retreatNext.DistanceTo(closerPlayer.Position) > enemy.Position.DistanceTo(closerPlayer.Position), "Retreat should increase distance to the target");

        world.RemoveEntity(closerPlayer.Id);
        var fartherPlayer = CreatePlayer(new Position(8, 2));
        world.Player = fartherPlayer;
        world.AddEntity(fartherPlayer);

        var advanceAction = brain.DecideAction(enemy, world, pathfinder);
        Expect.True(advanceAction is MoveAction, "Kiter beyond preferred range should advance");
        var advanceMove = (MoveAction)advanceAction;
        var advanceNext = enemy.Position + advanceMove.Delta;
        Expect.True(advanceNext.DistanceTo(fartherPlayer.Position) < enemy.Position.DistanceTo(fartherPlayer.Position), "Advance should decrease distance to the target");
    }

    private static void SupportMovesTowardAllies()
    {
        var world = CreateWorld();
        world.InitGrid(12, 12);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var pathfinder = new Pathfinder();
        var profile = AIProfiles.Support with { SupportRange = 3, AggroRange = 8 };
        var brain = new SupportBrain(profile);

        var support = CreateEnemy(new Position(2, 2), new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var ally = new StubEntity("Ally", new Position(7, 2), Faction.Enemy, stats: new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });

        world.Player = new StubEntity("Player", new Position(0, 0), Faction.Player);
        world.AddEntity(support);
        world.AddEntity(ally);

        var action = brain.DecideAction(support, world, pathfinder);
        Expect.True(action is MoveAction, "Support with no visible enemy should move toward allies");
        var move = (MoveAction)action;
        var next = support.Position + move.Delta;
        Expect.True(next.DistanceTo(ally.Position) < support.Position.DistanceTo(ally.Position), "Support should close the distance to the ally");

        support.Position = new Position(6, 2);
        world.UpdateEntityPosition(support.Id, new Position(2, 2), support.Position);
        var ally2 = new StubEntity("Ally2", new Position(7, 3), Faction.Enemy, stats: new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        world.AddEntity(ally2);

        var clusterAction = brain.DecideAction(support, world, pathfinder);
        Expect.True(clusterAction is WaitAction or MoveAction, "Support near multiple allies should stay or微调 position");
    }

    private static void GroupAggroSharesTarget()
    {
        var world = CreateWorld();
        world.InitGrid(12, 12);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        for (var y = 3; y < world.Height; y++)
        {
            world.SetTile(new Position(5, y), TileType.Wall);
        }

        var pathfinder = new Pathfinder();
        var profile = AIProfiles.MeleeRusher with { GroupAggroRange = 5 };
        var brain = new MeleeRusherBrain(profile);

        var spotter = CreateEnemy(new Position(2, 2));
        var follower = CreateEnemy(new Position(2, 5));
        var player = CreatePlayer(new Position(7, 2));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(spotter);
        world.AddEntity(follower);

        _ = brain.DecideAction(spotter, world, pathfinder);

        var followerAction = brain.DecideAction(follower, world, pathfinder);
        Expect.True(followerAction is MoveAction, "Follower outside individual LoS but within group aggro range should share the spotter's target");
        var move = (MoveAction)followerAction;
        var next = follower.Position + move.Delta;
        Expect.True(next.DistanceTo(player.Position) < follower.Position.DistanceTo(player.Position), "Follower should move toward the shared target");
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
