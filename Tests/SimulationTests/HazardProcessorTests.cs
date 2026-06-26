using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class HazardProcessorTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Hazard trap triggers on move", TrapTriggersOnMove);
        registry.Add("Simulation.Hazard trap damage is deterministic", TrapDamageIsDeterministic);
        registry.Add("Simulation.Hazard trap kills are unattributed", TrapKillIsUnattributed);
        registry.Add("Simulation.Hazard phased actor avoids trap", PhasedActorAvoidsTrap);
        registry.Add("Simulation.Hazard flying actor avoids trap", FlyingActorAvoidsTrap);
        registry.Add("Simulation.Hazard NPC swap triggers both tiles", NpcSwapTriggersBothTiles);
        registry.Add("Simulation.Hazard disarmed trap does not trigger", DisarmedTrapDoesNotTrigger);
        registry.Add("Simulation.Hazard trap applies status effect", TrapAppliesStatusEffect);
    }

    private static void TrapTriggersOnMove()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var trap = CreateTrap(world, "spike_trap", new Position(2, 1));

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(trap);

        var startHp = actor.Stats.HP;
        var outcome = new MoveAction(actor.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Move onto a trap tile should succeed");
        Expect.Equal(new Position(2, 1), actor.Position, "Actor should end on the trap tile");
        Expect.True(actor.Stats.HP < startHp, "Trap should damage the actor");
        Expect.False(trap.GetComponent<TrapComponent>()!.IsArmed, "Trap should be disarmed after triggering");
        Expect.True(trap.GetComponent<TrapComponent>()!.IsRevealed, "Trap should be revealed after triggering");
        Expect.True(outcome.LogMessages.Exists(message => message.Contains("triggers", System.StringComparison.Ordinal)), "Outcome should log the trigger");
        Expect.True(outcome.CombatEvents.Exists(e => e.ActionType == ActionType.Move), "Outcome should include a move combat event");
    }

    private static void TrapDamageIsDeterministic()
    {
        var first = RunTrapTrigger(seed: 42, trapId: "spike_trap");
        var second = RunTrapTrigger(seed: 42, trapId: "spike_trap");

        Expect.Equal(first, second, "Same seed should produce identical trap damage");
    }

    private static int RunTrapTrigger(int seed, string trapId)
    {
        var world = CreateWorld(seed);
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var trap = CreateTrap(world, trapId, new Position(2, 1));

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(trap);

        new MoveAction(actor.Id, new Position(1, 0)).Execute(world);
        return actor.Stats.HP;
    }

    private static void TrapKillIsUnattributed()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player, new Stats { HP = 1, MaxHP = 10, Attack = 4, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        var trap = CreateTrap(world, "deadly_pit", new Position(2, 1));

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(trap);

        var outcome = new MoveAction(actor.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Moving onto a lethal trap should still succeed");
        Expect.True(world.GetEntity(actor.Id) is null, "Actor should be removed after dying to the trap");
        Expect.True(outcome.LogMessages.Exists(message => message.Contains("dies to", System.StringComparison.Ordinal)), "Outcome should log an unattributed death");
    }

    private static void PhasedActorAvoidsTrap()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var trap = CreateTrap(world, "spike_trap", new Position(2, 1));

        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Phased, 3);

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(trap);

        var startHp = actor.Stats.HP;
        var outcome = new MoveAction(actor.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Phased move onto a trap should succeed");
        Expect.Equal(startHp, actor.Stats.HP, "Phased actor should not take trap damage");
        Expect.True(trap.GetComponent<TrapComponent>()!.IsArmed, "Trap should remain armed when avoided");
    }

    private static void FlyingActorAvoidsTrap()
    {
        var world = CreateWorld();
        world.ContentDatabase = new StubContentDatabase();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var trap = CreateTrap(world, "ground_only", new Position(2, 1));

        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Flying, 3);

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(trap);

        var startHp = actor.Stats.HP;
        var outcome = new MoveAction(actor.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Flying move onto a trap should succeed");
        Expect.Equal(startHp, actor.Stats.HP, "Flying actor should not take trap damage");
        Expect.True(trap.GetComponent<TrapComponent>()!.IsArmed, "Ground-only trap should remain armed when avoided");
    }

    private static void NpcSwapTriggersBothTiles()
    {
        var world = CreateWorld();
        var player = CreateActor("Player", new Position(1, 1), Faction.Player);
        var npc = new Entity("Guide", new Position(2, 1), new Stats { HP = 10, MaxHP = 10, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 }, Faction.Neutral);
        npc.SetComponent(new NpcComponent { TemplateId = "guide", Role = "advisor", DialogueId = "guide_intro" });

        var playerTileTrap = CreateTrap(world, "spike_trap", new Position(1, 1));
        var npcTileTrap = CreateTrap(world, "spike_trap", new Position(2, 1));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(npc);
        world.AddEntity(playerTileTrap);
        world.AddEntity(npcTileTrap);

        var playerStartHp = player.Stats.HP;
        var npcStartHp = npc.Stats.HP;

        var outcome = new MoveAction(player.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Swap move should succeed");
        Expect.Equal(new Position(2, 1), player.Position, "Player should end in NPC's previous tile");
        Expect.Equal(new Position(1, 1), npc.Position, "NPC should end in player's previous tile");
        Expect.True(player.Stats.HP < playerStartHp, "Player should trigger the trap in the NPC's tile");
        Expect.True(npc.Stats.HP < npcStartHp, "NPC should trigger the trap in the player's tile");
        Expect.True(outcome.LogMessages.Count(message => message.Contains("triggers", System.StringComparison.Ordinal)) >= 2, "Both trap triggers should be logged");
    }

    private static void DisarmedTrapDoesNotTrigger()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var trap = CreateTrap(world, "spike_trap", new Position(2, 1));
        trap.GetComponent<TrapComponent>()!.IsArmed = false;

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(trap);

        var startHp = actor.Stats.HP;
        var outcome = new MoveAction(actor.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Move onto a disarmed trap should succeed");
        Expect.Equal(startHp, actor.Stats.HP, "Disarmed trap should not damage the actor");
    }

    private static void TrapAppliesStatusEffect()
    {
        var world = CreateWorld();
        var actor = CreateActor("Player", new Position(1, 1), Faction.Player);
        var trap = CreateTrap(world, "poison_needle", new Position(2, 1));

        world.Player = actor;
        world.AddEntity(actor);
        world.AddEntity(trap);

        var outcome = new MoveAction(actor.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Move onto a status trap should succeed");
        Expect.True(StatusEffectProcessor.HasEffect(actor, StatusEffectType.Poisoned), "Poison needle should apply poison");
        Expect.True(outcome.CombatEvents.Exists(e => e.StatusEffectsApplied.Count > 0), "Combat event should report applied status effect");
    }

    private static WorldState CreateWorld(int seed = 123)
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

        world.Seed = seed;
        world.ContentDatabase = new StubContentDatabase();
        return world;
    }

    private static StubEntity CreateActor(string name, Position position, Faction faction, Stats? stats = null)
    {
        return new StubEntity(name, position, faction, stats: stats ?? new Stats { HP = 10, MaxHP = 10, Attack = 4, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
    }

    private static Entity CreateTrap(WorldState world, string trapId, Position position)
    {
        world.SetTile(position, TileType.Trap);
        var trap = new Entity(
            "Trap",
            position,
            new Stats { HP = 1, MaxHP = 1, Attack = 0, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 0, ViewRadius = 0 },
            Faction.Neutral,
            blocksMovement: false,
            blocksSight: false);

        trap.SetComponent(new TrapComponent
        {
            TemplateId = trapId,
            IsArmed = true,
            IsRevealed = false,
        });

        return trap;
    }
}
