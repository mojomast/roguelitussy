using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.AITests;

public sealed class UtilityScorerTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("AI.Utility scorer prefers attacks over waiting when adjacent", AttackOutscoresWaitingWhenAdjacent);
        registry.Add("AI.Utility scorer prefers escape moves while fleeing", FleeMovesOutscoreApproachMoves);
        registry.Add("AI.Utility scorer prefers patrol moves that progress toward patrol targets", PatrolMovesPreferProgress);
    }

    private static void AttackOutscoresWaitingWhenAdjacent()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var self = new StubEntity("Enemy", new Position(2, 2), Faction.Enemy, stats: new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var target = new StubEntity("Player", new Position(3, 2), Faction.Player, stats: new Stats { HP = 8, MaxHP = 8, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var memory = new AIStateComponent { State = AIState.Attack };

        world.Player = target;
        world.AddEntity(target);
        world.AddEntity(self);

        var attackScore = UtilityScorer.ScoreAction(new AttackAction(self.Id, target.Id), self, target, target.Position, world, memory, AIProfiles.MeleeRusher, pathfinder);
        var waitScore = UtilityScorer.ScoreAction(new WaitAction(self.Id), self, target, target.Position, world, memory, AIProfiles.MeleeRusher, pathfinder);

        Expect.True(attackScore > waitScore, "Attacking an adjacent hostile should outscore waiting");
    }

    private static void FleeMovesOutscoreApproachMoves()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var self = new StubEntity("Enemy", new Position(3, 3), Faction.Enemy, stats: new Stats { HP = 2, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var target = new StubEntity("Player", new Position(4, 3), Faction.Player, stats: new Stats { HP = 20, MaxHP = 20, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var memory = new AIStateComponent { State = AIState.Flee };

        world.Player = target;
        world.AddEntity(target);
        world.AddEntity(self);

        var fleeScore = UtilityScorer.ScoreAction(new MoveAction(self.Id, new Position(-1, 0)), self, target, target.Position, world, memory, AIProfiles.Fleeing, pathfinder);
        var approachScore = UtilityScorer.ScoreAction(new MoveAction(self.Id, new Position(1, 0)), self, target, target.Position, world, memory, AIProfiles.Fleeing, pathfinder);

        Expect.True(fleeScore > approachScore, "Fleeing should favor moves that increase distance from danger");
    }

    private static void PatrolMovesPreferProgress()
    {
        var world = CreateWorld();
        var pathfinder = new Pathfinder();
        var self = new StubEntity("Enemy", new Position(2, 2), Faction.Enemy, stats: new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, ViewRadius = 8 });
        var memory = new AIStateComponent { State = AIState.Patrol, PatrolTarget = new Position(5, 2) };

        world.Player = new StubEntity("Player", new Position(0, 0), Faction.Player);

        var forwardScore = UtilityScorer.ScoreAction(new MoveAction(self.Id, new Position(1, 0)), self, null, memory.PatrolTarget, world, memory, AIProfiles.PatrolGuard, pathfinder);
        var backwardScore = UtilityScorer.ScoreAction(new MoveAction(self.Id, new Position(-1, 0)), self, null, memory.PatrolTarget, world, memory, AIProfiles.PatrolGuard, pathfinder);

        Expect.True(forwardScore > backwardScore, "Patrol scoring should reward moves that reduce distance to the patrol target");
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(8, 8);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }
}