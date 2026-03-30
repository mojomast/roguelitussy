using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class GameLoopTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.GameLoop executes all queued actors", ProcessRoundExecutesAllActors);
        registry.Add("Simulation.GameLoop failed validation still consumes energy", FailedValidationStillConsumesEnergy);
        registry.Add("Simulation.GameLoop aggregates action outcomes", ProcessRoundAggregatesOutcomes);
        registry.Add("Simulation.GameLoop ticks status effects through scheduler", ProcessRoundTicksStatusEffects);
    }

    private static void ProcessRoundExecutesAllActors()
    {
        var world = CreateWorld();
        var actors = new[]
        {
            CreateActor("Alpha", new Position(1, 1), Faction.Player),
            CreateActor("Beta", new Position(2, 1), Faction.Enemy),
            CreateActor("Gamma", new Position(3, 1), Faction.Enemy),
        };

        world.Player = actors[0];
        foreach (var actor in actors)
        {
            world.AddEntity(actor);
        }

        var gameLoop = new GameLoop();
        var scheduler = new ScriptedScheduler(actors.Select(actor => actor.Id));
        var outcome = gameLoop.ProcessRound(world, scheduler, actor => new WaitAction(actor.Id));

        Expect.Equal(1, world.TurnNumber, "GameLoop should begin exactly one round");
        Expect.Equal(0, actors[0].Stats.Energy, "First actor should spend its energy");
        Expect.Equal(0, actors[1].Stats.Energy, "Second actor should spend its energy");
        Expect.Equal(0, actors[2].Stats.Energy, "Third actor should spend its energy");
        Expect.Equal(3, outcome.LogMessages.Count, "Each executed action should contribute to the aggregate log");
    }

    private static void FailedValidationStillConsumesEnergy()
    {
        var world = CreateWorld();
        world.SetTile(new Position(2, 1), TileType.Wall);
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        world.Player = actor;
        world.AddEntity(actor);

        var gameLoop = new GameLoop();
        var scheduler = new ScriptedScheduler(new[] { actor.Id });
        var outcome = gameLoop.ProcessRound(world, scheduler, entity => new MoveAction(entity.Id, new Position(1, 0)));

        Expect.Equal(0, actor.Stats.Energy, "Invalid actions should still consume energy");
        Expect.True(outcome.LogMessages.Any(message => message.Contains("failed", StringComparison.OrdinalIgnoreCase)), "GameLoop should log failed validations");
    }

    private static void ProcessRoundAggregatesOutcomes()
    {
        var world = CreateWorld(seed: 0);
        var attacker = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 20, MaxHP = 20, Attack = 12, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, Energy = 1000 });
        var defender = CreateActor("Enemy", new Position(2, 1), Faction.Enemy, new Stats { HP = 12, MaxHP = 12, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, Energy = 1000 });
        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);

        var gameLoop = new GameLoop();
        var scheduler = new ScriptedScheduler(new[] { attacker.Id });
        var outcome = gameLoop.ProcessRound(world, scheduler, entity => new AttackAction(entity.Id, defender.Id));

        Expect.Equal(1, outcome.CombatEvents.Count, "GameLoop should aggregate combat events");
        Expect.True(outcome.DirtyPositions.Count >= 2, "GameLoop should aggregate dirty positions from actions");
    }

    private static void ProcessRoundTicksStatusEffects()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, Energy = 1000 });
        world.Player = actor;
        world.AddEntity(actor);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, 2, 1);

        var gameLoop = new GameLoop();
        var scheduler = new ScriptedScheduler(new[] { actor.Id });
        gameLoop.ProcessRound(world, scheduler, entity => new WaitAction(entity.Id));

        Expect.Equal(8, actor.Stats.HP, "Status effects should tick as turns are consumed");
    }

    private static WorldState CreateWorld(int seed = 123)
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Seed = seed;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreateActor(string name, Position position, Faction faction, Stats? stats = null)
    {
        return new StubEntity(name, position, faction, stats: stats ?? new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100, Energy = 1000 });
    }

    private sealed class ScriptedScheduler : ITurnScheduler
    {
        private readonly Queue<EntityId> _queue;
        private WorldState? _world;

        public ScriptedScheduler(IEnumerable<EntityId> order)
        {
            _queue = new Queue<EntityId>(order);
        }

        public int EnergyThreshold => 1000;

        public void BeginRound(WorldState world)
        {
            _world = world;
            world.TurnNumber++;
        }

        public bool HasNextActor() => _queue.Count > 0;

        public IEntity? GetNextActor() => _world is null || _queue.Count == 0 ? null : _world.GetEntity(_queue.Dequeue());

        public void ConsumeEnergy(EntityId actorId, int cost)
        {
            if (_world?.GetEntity(actorId) is { } entity)
            {
                entity.Stats.Energy -= cost;
                StatusEffectProcessor.Tick(_world, actorId);
            }
        }

        public void EndRound(WorldState world)
        {
        }

        public void Register(IEntity entity)
        {
        }

        public void Unregister(EntityId id)
        {
        }
    }
}