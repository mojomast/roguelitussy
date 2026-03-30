using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class StatusEffectTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.StatusEffects poison ticks and expires", PoisonTicksAndExpires);
        registry.Add("Simulation.StatusEffects poison stacks", PoisonStacks);
        registry.Add("Simulation.StatusEffects poison caps at five stacks", PoisonCapsAtFiveStacks);
        registry.Add("Simulation.StatusEffects multiple effects tick independently", MultipleEffectsTickIndependently);
        registry.Add("Simulation.StatusEffects haste changes effective speed", HasteChangesEffectiveSpeed);
        registry.Add("Simulation.StatusEffects removal occurs at zero turns", ExpiresAtZeroTurns);
        registry.Add("Simulation.StatusEffects poison can kill", PoisonCanKill);
    }

    private static void PoisonTicksAndExpires()
    {
        var world = CreateWorld();
        var actor = CreateActor();

        world.Player = actor;
        world.AddEntity(actor);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, 3);

        StatusEffectProcessor.Tick(world, actor.Id);
        StatusEffectProcessor.Tick(world, actor.Id);
        StatusEffectProcessor.Tick(world, actor.Id);

        Expect.Equal(14, actor.Stats.HP, "Poison should deal two damage per tick over three turns");
        Expect.False(StatusEffectProcessor.HasEffect(actor, StatusEffectType.Poisoned), "Poison should expire when its duration reaches zero");
    }

    private static void PoisonStacks()
    {
        var world = CreateWorld();
        var actor = CreateActor();

        world.Player = actor;
        world.AddEntity(actor);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, 3);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, 3);

        StatusEffectProcessor.Tick(world, actor.Id);

        Expect.Equal(16, actor.Stats.HP, "Two poison stacks should deal four damage per tick");
        Expect.Equal(2, StatusEffectProcessor.GetMagnitude(actor, StatusEffectType.Poisoned), "Poison magnitude should reflect stack count");
    }

    private static void PoisonCapsAtFiveStacks()
    {
        var actor = CreateActor();
        for (var index = 0; index < 6; index++)
        {
            StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, 4);
        }

        var poison = StatusEffectProcessor.GetEffect(actor, StatusEffectType.Poisoned);
        Expect.NotNull(poison, "Poison should be present after application");
        Expect.Equal(5, poison!.Magnitude, "Poison should cap at five stacks");
        Expect.Equal(4, poison.RemainingTurns, "Further applications should refresh duration rather than exceed stack cap");
    }

    private static void MultipleEffectsTickIndependently()
    {
        var world = CreateWorld();
        var actor = CreateActor();

        world.Player = actor;
        world.AddEntity(actor);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, 2);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Burning, 2);

        StatusEffectProcessor.Tick(world, actor.Id);

        Expect.Equal(15, actor.Stats.HP, "Poison and burning should both apply their own tick values");
    }

    private static void HasteChangesEffectiveSpeed()
    {
        var actor = CreateActor(speed: 100);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Hasted, 2);

        Expect.Equal(150, StatusEffectProcessor.GetEffectiveSpeed(actor), "Haste should set the effective speed used by the scheduler");
    }

    private static void ExpiresAtZeroTurns()
    {
        var world = CreateWorld();
        var actor = CreateActor();

        world.Player = actor;
        world.AddEntity(actor);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Burning, 1);

        StatusEffectProcessor.Tick(world, actor.Id);
        Expect.False(StatusEffectProcessor.HasEffect(actor, StatusEffectType.Burning), "Effects should be removed when their turn counter reaches zero");
    }

    private static void PoisonCanKill()
    {
        var world = CreateWorld();
        var actor = CreateActor(hp: 2);

        world.Player = actor;
        world.AddEntity(actor);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, 1);

        var result = StatusEffectProcessor.Tick(world, actor.Id);
        Expect.True(result.Died, "Ticking poison should report lethal damage");
        Expect.True(world.GetEntity(actor.Id) is null, "Dead entities should be removed from the world");
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(6, 6);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }

    private static StubEntity CreateActor(int hp = 20, int speed = 100) =>
        new("Actor", new Position(1, 1), Faction.Player, stats: new Stats { HP = hp, MaxHP = 20, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = speed });
}