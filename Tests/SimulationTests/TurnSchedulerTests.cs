using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class TurnSchedulerTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.TurnScheduler tie breaks by registration order", TieBreaksByRegistrationOrder);
        registry.Add("Simulation.TurnScheduler faster actors act more often", FasterActorsActMoreOften);
        registry.Add("Simulation.TurnScheduler consuming energy requires recharge", ConsumingEnergyRequiresRecharge);
        registry.Add("Simulation.TurnScheduler unregister removes actor from order", UnregisterRemovesActor);
        registry.Add("Simulation.TurnScheduler mixed speeds follow expected ratios", MixedSpeedsFollowRatios);
        registry.Add("Simulation.TurnScheduler ignores neutral non-brain NPCs", IgnoresNeutralNonBrainNpcs);
    }

    private static void TieBreaksByRegistrationOrder()
    {
        var world = CreateWorld();
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player, stats: Stats(100));
        var enemy = new StubEntity("Enemy", new Position(2, 1), Faction.Enemy, stats: Stats(100));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var next = scheduler.GetNextActor();
        Expect.NotNull(next, "Scheduler should return the player as the first actor");
        Expect.Equal(player, next!, "Player should act first when energy ties and registration order matches spawn order");
    }

    private static void FasterActorsActMoreOften()
    {
        var world = CreateWorld();
        var fast = new StubEntity("Fast", new Position(1, 1), stats: Stats(200));
        var slow = new StubEntity("Slow", new Position(2, 1), stats: Stats(100));

        world.Player = fast;
        world.AddEntity(fast);
        world.AddEntity(slow);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var turns = TakeTurns(scheduler, 6).ToArray();
        Expect.Equal(4, turns.Count(actor => actor.Id == fast.Id), "A speed 200 actor should act roughly twice as often as speed 100 over the sample window");
        Expect.Equal(2, turns.Count(actor => actor.Id == slow.Id), "The slower actor should receive the remaining turns");
    }

    private static void ConsumingEnergyRequiresRecharge()
    {
        var world = CreateWorld();
        var actor = new StubEntity("Actor", new Position(1, 1), stats: Stats(100));

        world.Player = actor;
        world.AddEntity(actor);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var next = scheduler.GetNextActor();
        Expect.NotNull(next, "Scheduler should return the only registered actor");
        Expect.Equal(actor, next!, "Single actor should get the first turn");
        scheduler.ConsumeEnergy(actor.Id, 1000);

        Expect.Equal(0, scheduler.GetEnergy(actor.Id), "Energy should be spent after acting");
        var afterRecharge = scheduler.GetNextActor();
        Expect.NotNull(afterRecharge, "Scheduler should return the actor again after energy gain");
        Expect.Equal(actor, afterRecharge!, "Actor should become ready again after enough energy gain ticks");
    }

    private static void UnregisterRemovesActor()
    {
        var world = CreateWorld();
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player, stats: Stats(100));
        var enemy = new StubEntity("Enemy", new Position(2, 1), Faction.Enemy, stats: Stats(100));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(enemy);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);
        scheduler.Unregister(enemy.Id);

        var turns = TakeTurns(scheduler, 3).ToArray();
        Expect.True(turns.All(actor => actor.Id == player.Id), "Unregistered actors should never be returned again");
    }

    private static void MixedSpeedsFollowRatios()
    {
        var world = CreateWorld();
        var normal = new StubEntity("Normal", new Position(1, 1), stats: Stats(100));
        var quick = new StubEntity("Quick", new Position(2, 1), stats: Stats(150));
        var sluggish = new StubEntity("Sluggish", new Position(3, 1), stats: Stats(50));

        world.Player = normal;
        world.AddEntity(normal);
        world.AddEntity(quick);
        world.AddEntity(sluggish);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var turns = TakeTurns(scheduler, 12).ToArray();
        Expect.Equal(4, turns.Count(actor => actor.Id == normal.Id), "Speed 100 actor should take four turns in the 12-turn sample");
        Expect.Equal(6, turns.Count(actor => actor.Id == quick.Id), "Speed 150 actor should take six turns in the 12-turn sample");
        Expect.Equal(2, turns.Count(actor => actor.Id == sluggish.Id), "Speed 50 actor should take two turns in the 12-turn sample");
    }

    private static void IgnoresNeutralNonBrainNpcs()
    {
        var world = CreateWorld();
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player, stats: Stats(100));
        var npc = new StubEntity("Quartermaster", new Position(2, 1), Faction.Neutral, stats: Stats(100));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(npc);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var first = scheduler.GetNextActor();
        Expect.Equal(player, first!, "Player should act first in a world with only a neutral NPC nearby.");
        scheduler.ConsumeEnergy(player.Id, 1000);

        var next = scheduler.GetNextActor();
        Expect.Equal(player, next!, "Neutral NPCs without brains should not be scheduled as autonomous actors.");
    }

    private static System.Collections.Generic.IEnumerable<IEntity> TakeTurns(TurnScheduler scheduler, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var actor = scheduler.GetNextActor();
            Expect.NotNull(actor, "Scheduler should continue returning actors while entities remain registered");
            scheduler.ConsumeEnergy(actor!.Id, 1000);
            yield return actor;
        }
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

    private static Stats Stats(int speed) => new() { HP = 10, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = speed };
}