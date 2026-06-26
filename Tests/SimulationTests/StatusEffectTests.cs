using Godotussy;
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
        registry.Add("Simulation.StatusEffects sourced poison kill awards XP", SourcedPoisonKillAwardsXp);
        registry.Add("Simulation.StatusEffects corroded stacks to authored cap", CorrodedStacksToAuthoredCap);
        registry.Add("Simulation.StatusEffects burning and frozen remove each other", BurningAndFrozenRemoveEachOther);
        registry.Add("Simulation.StatusEffects stunned actor skips turn", StunnedActorSkipsTurn);
        registry.Add("Simulation.StatusEffects frozen actor skips turn", FrozenActorSkipsTurn);
        registry.Add("Simulation.StatusEffects phased actor moves through walls", PhasedActorMovesThroughWalls);
        registry.Add("Simulation.StatusEffects immune entity takes zero physical damage", ImmuneEntityTakesZeroPhysicalDamage);
        registry.Add("Simulation.StatusEffects authored tick damage changes runtime tick damage", AuthoredTickDamageChangesRuntimeTickDamage);
        registry.Add("Simulation.StatusEffects data driven speed modifiers replace hardcoded defaults", DataDrivenSpeedModifiersReplaceHardcodedDefaults);
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

    private static void SourcedPoisonKillAwardsXp()
    {
        var world = CreateWorld();
        var attacker = CreateActor();
        attacker.SetComponent(new ProgressionComponent());
        var victim = new StubEntity("Victim", new Position(2, 1), Faction.Enemy, stats: new Stats { HP = 2, MaxHP = 2, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        victim.SetComponent(new XpValueComponent { Value = 9 });

        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(victim);
        StatusEffectProcessor.ApplyEffect(victim, StatusEffectType.Poisoned, 1, sourceEntityId: attacker.Id);

        var result = StatusEffectProcessor.Tick(world, victim.Id);
        var progression = attacker.GetComponent<ProgressionComponent>()!;

        Expect.True(result.Died, "Sourced poison should report a kill when lethal");
        Expect.Equal(1, progression.Kills, "Delayed status kills should increment source kill credit");
        Expect.Equal(9, progression.Experience, "Delayed status kills should award victim XP to the source");
        Expect.True(world.GetEntity(victim.Id) is null, "Killed victim should be removed");
    }

    private static void CorrodedStacksToAuthoredCap()
    {
        var actor = CreateActor();

        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Corroded, 2);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Corroded, 2);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Corroded, 2);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Corroded, 2);

        Expect.Equal(3, StatusEffectProcessor.GetMagnitude(actor, StatusEffectType.Corroded), "Corroded should stack to the authored max of three");
    }

    private static void BurningAndFrozenRemoveEachOther()
    {
        var actor = CreateActor();

        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Frozen, 2);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Burning, 2);
        Expect.False(StatusEffectProcessor.HasEffect(actor, StatusEffectType.Frozen), "Burning should remove frozen on apply");
        Expect.True(StatusEffectProcessor.HasEffect(actor, StatusEffectType.Burning), "Burning should remain after removing frozen");

        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Frozen, 2);
        Expect.False(StatusEffectProcessor.HasEffect(actor, StatusEffectType.Burning), "Frozen should remove burning on apply");
        Expect.True(StatusEffectProcessor.HasEffect(actor, StatusEffectType.Frozen), "Frozen should remain after removing burning");
    }

    private static void StunnedActorSkipsTurn()
    {
        var world = CreateWorld();
        var stunned = CreateActor(speed: 100);
        var normal = CreateActor(speed: 100);
        normal.Position = new Position(2, 1);
        world.Player = stunned;
        world.AddEntity(stunned);
        world.AddEntity(normal);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);
        stunned.Stats.Energy = scheduler.EnergyThreshold;
        normal.Stats.Energy = scheduler.EnergyThreshold;
        scheduler.Register(stunned);
        scheduler.Register(normal);

        StatusEffectProcessor.ApplyEffect(stunned, StatusEffectType.Stunned, 2);
        var next = scheduler.GetNextActor();

        Expect.NotNull(next, "Scheduler should still find a ready actor");
        Expect.Equal(normal.Id, next!.Id, "A stunned actor should be skipped in favor of the next ready actor");
        Expect.True(scheduler.GetEnergy(stunned.Id) < scheduler.EnergyThreshold, "Skipping should consume the stunned actor's energy");
    }

    private static void FrozenActorSkipsTurn()
    {
        var world = CreateWorld();
        var frozen = CreateActor(speed: 100);
        var normal = CreateActor(speed: 100);
        normal.Position = new Position(2, 1);
        world.Player = frozen;
        world.AddEntity(frozen);
        world.AddEntity(normal);

        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);
        frozen.Stats.Energy = scheduler.EnergyThreshold;
        normal.Stats.Energy = scheduler.EnergyThreshold;
        scheduler.Register(frozen);
        scheduler.Register(normal);

        StatusEffectProcessor.ApplyEffect(frozen, StatusEffectType.Frozen, 2);
        var next = scheduler.GetNextActor();

        Expect.NotNull(next, "Scheduler should still find a ready actor");
        Expect.Equal(normal.Id, next!.Id, "A frozen actor should be skipped in favor of the next ready actor");
        Expect.True(scheduler.GetEnergy(frozen.Id) < scheduler.EnergyThreshold, "Skipping should consume the frozen actor's energy");
    }

    private static void PhasedActorMovesThroughWalls()
    {
        var world = CreateWorld();
        var actor = CreateActor();
        world.Player = actor;
        world.AddEntity(actor);
        world.SetTile(new Position(2, 1), TileType.Wall);

        Expect.Equal(ActionResult.Blocked, new MoveAction(actor.Id, new Position(1, 0)).Validate(world), "Wall should block normal movement");

        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Phased, 2);
        var outcome = new MoveAction(actor.Id, new Position(1, 0)).Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Phased actor should move through walls");
        Expect.Equal(new Position(2, 1), actor.Position, "Phased actor should end on the wall tile");
    }

    private static void ImmuneEntityTakesZeroPhysicalDamage()
    {
        var defender = new StubEntity("Defender", Position.Zero, stats: new Stats { HP = 10, MaxHP = 10, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0 });
        StatusEffectProcessor.ApplyEffect(defender, StatusEffectType.Phased, 2);

        var resolver = new CombatResolver(23);
        var physical = resolver.ApplyArmor(10, defender, DamageType.Physical);
        var fire = resolver.ApplyArmor(10, defender, DamageType.Fire);

        Expect.Equal(0, physical, "immune_physical should nullify physical damage");
        Expect.Equal(10, fire, "immune_physical should not affect non-physical damage");
    }

    private static void AuthoredTickDamageChangesRuntimeTickDamage()
    {
        var world = CreateWorld();
        var actor = CreateActor();
        world.Player = actor;
        world.AddEntity(actor);

        var content = new StubContentDatabase();
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, content, 3);
        StatusEffectProcessor.Tick(world, actor.Id, content);

        Expect.Equal(18, actor.Stats.HP, "Stub content poison should deal two damage per tick");

        actor.Stats.HP = 20;
        var customContent = new StubContentDatabase();
        customContent.StatusEffects["poisoned"].TickEffects[0].Value = 3;
        StatusEffectProcessor.RemoveEffect(actor, StatusEffectType.Poisoned);
        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Poisoned, customContent, 3);
        StatusEffectProcessor.Tick(world, actor.Id, customContent);

        Expect.Equal(17, actor.Stats.HP, "Changing authored tick damage should change runtime tick damage");
    }

    private static void DataDrivenSpeedModifiersReplaceHardcodedDefaults()
    {
        var actor = CreateActor(speed: 100);
        var content = new StubContentDatabase();

        StatusEffectProcessor.ApplyEffect(actor, StatusEffectType.Hasted, content, 2);
        var effectiveSpeed = StatusEffectProcessor.GetEffectiveSpeed(actor, content);

        Expect.Equal(150, effectiveSpeed, "Data-driven haste should multiply speed by authored value");
    }

    private static void StatusAppliedEventCarriesAffectedTargetId()
    {
        var world = CreateWorld();
        var caster = new StubEntity("Caster", new Position(1, 1), Faction.Player,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var target = new StubEntity("Target", new Position(2, 1), Faction.Enemy,
            stats: new Stats { HP = 20, MaxHP = 20, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        world.Player = caster;
        world.AddEntity(caster);
        world.AddEntity(target);

        var gameManager = new GameManager();
        var bus = new EventBus();
        gameManager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), new StubContentDatabase(), new StubSaveManager(), bus);

        EntityId? capturedTargetId = null;
        bus.StatusEffectApplied += (entityId, effect) => capturedTargetId = entityId;

        var ability = new AbilityTemplate(
            "guaranteed_poison",
            "Guaranteed Poison",
            "Test ability that always applies poison to a single target.",
            new AbilityTargeting("single", 8, 0, false, false, false, null),
            1000,
            null,
            new AbilityEffect[]
            {
                new("apply_status", DamageType.Poison, 0, null, 0.0, "poisoned", 100, 3, null, null, 0.0, null),
            });

        var outcome = gameManager.ProcessPlayerAction(new CastAbilityAction(caster.Id, ability, target.Position));

        Expect.Equal(ActionResult.Success, outcome.Result, "Cast ability should succeed.");
        Expect.True(capturedTargetId.HasValue, "StatusEffectApplied event should have been emitted.");
        Expect.Equal(target.Id, capturedTargetId!.Value, "StatusEffectApplied event should carry the target's id, not the caster's id.");
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
