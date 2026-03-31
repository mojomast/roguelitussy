using System;
using System.Collections.Generic;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class AbilityTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Ability single target deals damage", SingleTargetDealsDamage);
        registry.Add("Simulation.Ability fireball hits area", FireballHitsArea);
        registry.Add("Simulation.Ability blink teleports caster", BlinkTeleportsCaster);
        registry.Add("Simulation.Ability life drain heals caster", LifeDrainHealsCaster);
        registry.Add("Simulation.Ability war cry applies status to enemies and allies", WarCryAppliesStatus);
        registry.Add("Simulation.Ability respects line of sight", RespectsLineOfSight);
        registry.Add("Simulation.Ability validates range", ValidatesRange);
        registry.Add("Simulation.Ability cooldown prevents reuse", CooldownPreventsReuse);
        registry.Add("Simulation.Ability self target applies status", SelfTargetAppliesStatus);
        registry.Add("Simulation.Ability aoe does not hit caster when no self center", AoeNoSelfCenter);
    }

    private static void SingleTargetDealsDamage()
    {
        var world = CreateWorld(seed: 42);
        var caster = CreateActor("Caster", new Position(3, 3), Faction.Player,
            new Stats { HP = 20, MaxHP = 20, Attack = 10, Defense = 2, Accuracy = 0, Evasion = 0, Speed = 100 });
        var target = CreateActor("Target", new Position(4, 3), Faction.Enemy);
        world.Player = caster;
        world.AddEntity(caster);
        world.AddEntity(target);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("arrow_shot", out var ability);

        var action = new CastAbilityAction(caster.Id, ability, target.Position);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Arrow shot should succeed");
        Expect.True(target.Stats.HP < 10, "Arrow shot should damage target");
        Expect.True(outcome.CombatEvents.Count > 0, "Should produce combat events");
        Expect.True(outcome.LogMessages.Count > 0, "Should produce log messages");
    }

    private static void FireballHitsArea()
    {
        var world = CreateWorld(seed: 7);
        var caster = CreateActor("Mage", new Position(1, 1), Faction.Player,
            new Stats { HP = 20, MaxHP = 20, Attack = 8, Defense = 2, Accuracy = 0, Evasion = 0, Speed = 100 });
        var enemy1 = CreateActor("Goblin1", new Position(5, 5), Faction.Enemy);
        var enemy2 = CreateActor("Goblin2", new Position(6, 5), Faction.Enemy);
        var farEnemy = CreateActor("FarGoblin", new Position(9, 9), Faction.Enemy);
        world.Player = caster;
        world.AddEntity(caster);
        world.AddEntity(enemy1);
        world.AddEntity(enemy2);
        world.AddEntity(farEnemy);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("fireball", out var ability);

        var action = new CastAbilityAction(caster.Id, ability, new Position(5, 5));
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Fireball should succeed");
        Expect.True(enemy1.Stats.HP < 10, "Enemy1 in blast radius should take damage");
        Expect.True(enemy2.Stats.HP < 10, "Enemy2 in blast radius should take damage");
        Expect.Equal(10, farEnemy.Stats.HP, "Far enemy should not take damage");
    }

    private static void BlinkTeleportsCaster()
    {
        var world = CreateWorld(seed: 1);
        var caster = CreateActor("Blinker", new Position(2, 2), Faction.Player);
        world.Player = caster;
        world.AddEntity(caster);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("blink", out var ability);

        var targetPos = new Position(7, 7);
        var action = new CastAbilityAction(caster.Id, ability, targetPos);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Blink should succeed");
        Expect.Equal(targetPos, caster.Position, "Caster should teleport to target position");
    }

    private static void LifeDrainHealsCaster()
    {
        var world = CreateWorld(seed: 42);
        var caster = CreateActor("Vampire", new Position(3, 3), Faction.Player,
            new Stats { HP = 10, MaxHP = 20, Attack = 10, Defense = 2, Accuracy = 0, Evasion = 0, Speed = 100 });
        var target = CreateActor("Victim", new Position(4, 3), Faction.Enemy,
            new Stats { HP = 30, MaxHP = 30, Attack = 3, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        world.Player = caster;
        world.AddEntity(caster);
        world.AddEntity(target);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("life_drain", out var ability);

        var hpBefore = caster.Stats.HP;
        var action = new CastAbilityAction(caster.Id, ability, target.Position);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Life drain should succeed");
        Expect.True(target.Stats.HP < 30, "Target should take damage");
        Expect.True(caster.Stats.HP > hpBefore, "Caster should heal from life drain");
    }

    private static void WarCryAppliesStatus()
    {
        var world = CreateWorld(seed: 10);
        var caster = CreateActor("Warrior", new Position(5, 5), Faction.Player);
        var ally = CreateActor("Ally", new Position(6, 5), Faction.Player);
        var enemy = CreateActor("Enemy", new Position(4, 5), Faction.Enemy);
        world.Player = caster;
        world.AddEntity(caster);
        world.AddEntity(ally);
        world.AddEntity(enemy);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("war_cry", out var ability);

        var action = new CastAbilityAction(caster.Id, ability, caster.Position);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "War cry should succeed");
        Expect.True(StatusEffectProcessor.HasEffect(enemy, StatusEffectType.Weakened), "Enemy should be weakened");
        Expect.True(StatusEffectProcessor.HasEffect(ally, StatusEffectType.Empowered), "Ally should be empowered");
        Expect.True(StatusEffectProcessor.HasEffect(caster, StatusEffectType.Empowered), "Caster (ally) should be empowered");
        Expect.False(StatusEffectProcessor.HasEffect(caster, StatusEffectType.Weakened), "Caster should not be weakened");
    }

    private static void RespectsLineOfSight()
    {
        var world = CreateWorld(seed: 0);
        var caster = CreateActor("Archer", new Position(1, 1), Faction.Player);
        var target = CreateActor("Target", new Position(5, 1), Faction.Enemy);
        world.Player = caster;
        world.AddEntity(caster);
        world.AddEntity(target);

        world.SetTile(new Position(3, 1), TileType.Wall);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("arrow_shot", out var ability);

        var action = new CastAbilityAction(caster.Id, ability, target.Position);
        var result = action.Validate(world);

        Expect.Equal(ActionResult.Blocked, result, "Arrow shot should be blocked by wall");
    }

    private static void ValidatesRange()
    {
        var world = CreateWorld(seed: 0);
        var caster = CreateActor("Archer", new Position(1, 1), Faction.Player);
        var target = CreateActor("FarTarget", new Position(1, 9), Faction.Enemy);
        world.Player = caster;
        world.AddEntity(caster);

        // Use a melee-range ability (life_drain, range 1) on a far target
        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("life_drain", out var ability);

        // Target is at distance 8 but ability range is 1
        world.AddEntity(target);
        var action = new CastAbilityAction(caster.Id, ability, target.Position);
        var result = action.Validate(world);

        Expect.Equal(ActionResult.Blocked, result, "Ability should fail when target is out of range");
    }

    private static void CooldownPreventsReuse()
    {
        var world = CreateWorld(seed: 5);
        var caster = CreateActor("Caster", new Position(3, 3), Faction.Player);
        var target = CreateActor("Target", new Position(4, 3), Faction.Enemy,
            new Stats { HP = 50, MaxHP = 50, Attack = 3, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        world.Player = caster;
        world.AddEntity(caster);
        world.AddEntity(target);

        var cooldowns = new CooldownComponent();
        caster.SetComponent(cooldowns);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("arrow_shot", out var ability);

        // First cast should succeed
        var action1 = new CastAbilityAction(caster.Id, ability, target.Position);
        Expect.Equal(ActionResult.Success, action1.Validate(world), "First cast should be valid");

        // Set cooldown
        cooldowns.SetCooldown("arrow_shot", 3);
        var action2 = new CastAbilityAction(caster.Id, ability, target.Position);
        Expect.Equal(ActionResult.Blocked, action2.Validate(world), "Should be blocked while on cooldown");

        // Tick the cooldown enough times
        cooldowns.TickAll();
        cooldowns.TickAll();
        cooldowns.TickAll();
        Expect.False(cooldowns.IsOnCooldown("arrow_shot"), "Cooldown should expire after enough ticks");

        var action3 = new CastAbilityAction(caster.Id, ability, target.Position);
        Expect.Equal(ActionResult.Success, action3.Validate(world), "Should succeed after cooldown expires");
    }

    private static void SelfTargetAppliesStatus()
    {
        var world = CreateWorld(seed: 0);
        var caster = CreateActor("Phaser", new Position(3, 3), Faction.Player);
        world.Player = caster;
        world.AddEntity(caster);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("phase_shift", out var ability);

        var action = new CastAbilityAction(caster.Id, ability, caster.Position);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Phase shift should succeed");
        Expect.True(StatusEffectProcessor.HasEffect(caster, StatusEffectType.Phased), "Caster should have phased status");
    }

    private static void AoeNoSelfCenter()
    {
        var world = CreateWorld(seed: 7);
        var caster = CreateActor("Mage", new Position(1, 1), Faction.Player,
            new Stats { HP = 20, MaxHP = 20, Attack = 8, Defense = 2, Accuracy = 0, Evasion = 0, Speed = 100 });
        var enemy = CreateActor("Enemy", new Position(5, 5), Faction.Enemy);
        world.Player = caster;
        world.AddEntity(caster);
        world.AddEntity(enemy);

        var content = new StubContentDatabase();
        content.TryGetAbilityTemplate("fireball", out var ability);

        // Target the fireball at the enemy position - caster is far so should not be hit
        var action = new CastAbilityAction(caster.Id, ability, enemy.Position);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Fireball should succeed");
        Expect.True(enemy.Stats.HP < 10, "Enemy should take damage");
        Expect.Equal(20, caster.Stats.HP, "Caster outside blast radius should not take damage");
    }

    private static WorldState CreateWorld(int seed = 1)
    {
        var world = new WorldState();
        world.InitGrid(10, 10);
        world.Seed = seed;
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }
        return world;
    }

    private static StubEntity CreateActor(string name, Position position, Faction faction, Stats? stats = null)
    {
        return new StubEntity(name, position, faction, stats: stats);
    }
}
