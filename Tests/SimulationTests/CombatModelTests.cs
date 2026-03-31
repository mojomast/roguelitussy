using System.Collections.Generic;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class CombatModelTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Combat weapon damage range replaces base variance", WeaponDamageRange);
        registry.Add("Simulation.Combat weapon crit chance overrides default", WeaponCritChance);
        registry.Add("Simulation.Combat on-hit burning triggers from Flamebrand", OnHitBurning);
        registry.Add("Simulation.Combat on-hit poison triggers from Viper Fang", OnHitPoison);
        registry.Add("Simulation.Combat empowered status increases damage", EmpoweredDamage);
        registry.Add("Simulation.Combat corroded status reduces armor", CorrodedArmor);
        registry.Add("Simulation.Combat unarmed uses base attack calculation", UnarmedBaseAttack);
        registry.Add("Simulation.Combat attack action uses equipped weapon", AttackActionEquippedWeapon);
    }

    private static void WeaponDamageRange()
    {
        var attacker = new StubEntity("Hero", Position.Zero, Faction.Player, stats: new Stats { HP = 20, MaxHP = 20, Attack = 4, Defense = 1, Accuracy = 0, Evasion = 0 });
        var defender = new StubEntity("Dummy", new Position(1, 0), stats: new Stats { HP = 100, MaxHP = 100, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0 });
        var weapon = new ItemTemplate("sword_iron", "Iron Sword", "Test", ItemCategory.Weapon, EquipSlot.MainHand,
            new Dictionary<string, int>(), null, 0, 1, "common", DamageMin: 5, DamageMax: 5, CritChance: 0, WeaponAccuracy: 0);

        var resolver = new CombatResolver(42);
        // Run multiple attacks, verify damage uses weapon range + attack/2 bonus
        var sawDamage = false;
        for (var i = 0; i < 20; i++)
        {
            var result = resolver.ResolveMeleeAttack(attacker, defender, i, weapon);
            if (!result.IsMiss)
            {
                // weapon base 5, attack bonus 4/2=2, no crit => raw should be 7
                Expect.Equal(7, result.RawDamage, "Weapon damage should be base + attack/2 bonus");
                sawDamage = true;
            }
        }

        Expect.True(sawDamage, "At least one attack should have hit");
    }

    private static void WeaponCritChance()
    {
        var attacker = new StubEntity("Hero", Position.Zero, Faction.Player, stats: new Stats { HP = 20, MaxHP = 20, Attack = 2, Defense = 1, Accuracy = 50, Evasion = 0, Speed = 100 });
        var defender = new StubEntity("Dummy", new Position(1, 0), stats: new Stats { HP = 1000, MaxHP = 1000, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        // 100% crit chance weapon
        var weapon = new ItemTemplate("crit_blade", "Crit Blade", "Test", ItemCategory.Weapon, EquipSlot.MainHand,
            new Dictionary<string, int>(), null, 0, 1, "common", DamageMin: 4, DamageMax: 4, CritChance: 100, WeaponAccuracy: 50);

        var resolver = new CombatResolver(77);
        var hitCount = 0;
        for (var i = 0; i < 20; i++)
        {
            var result = resolver.ResolveMeleeAttack(attacker, defender, i, weapon);
            if (!result.IsMiss)
            {
                Expect.True(result.IsCritical, "All hits should be crits with 100% crit chance weapon");
                hitCount++;
            }
        }

        Expect.True(hitCount > 0, "At least one attack should have hit");
    }

    private static void OnHitBurning()
    {
        var attacker = new StubEntity("Hero", Position.Zero, Faction.Player, stats: new Stats { HP = 20, MaxHP = 20, Attack = 10, Defense = 1, Accuracy = 50, Evasion = 0, Speed = 100 });
        var defender = new StubEntity("Goblin", new Position(1, 0), stats: new Stats { HP = 100, MaxHP = 100, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        // 100% chance burning on hit
        var weapon = new ItemTemplate("flame_test", "Flamebrand", "Test", ItemCategory.Weapon, EquipSlot.MainHand,
            new Dictionary<string, int>(), null, 0, 1, "rare", DamageMin: 5, DamageMax: 12, CritChance: 8, WeaponAccuracy: 80,
            OnHitEffects: new WeaponOnHitEffect[] { new(StatusEffectType.Burning, 100, 3) });

        var resolver = new CombatResolver(99);
        var applied = resolver.ProcessOnHitEffects(defender, weapon);

        Expect.True(applied.Count > 0, "On-hit burning should apply with 100% chance");
        Expect.Equal(StatusEffectType.Burning, applied[0].Type, "Applied effect should be burning");
        Expect.True(StatusEffectProcessor.HasEffect(defender, StatusEffectType.Burning), "Defender should have burning status");
    }

    private static void OnHitPoison()
    {
        var attacker = new StubEntity("Hero", Position.Zero, Faction.Player, stats: new Stats { HP = 20, MaxHP = 20, Attack = 10, Defense = 1, Accuracy = 50, Evasion = 0, Speed = 100 });
        var defender = new StubEntity("Goblin", new Position(1, 0), stats: new Stats { HP = 100, MaxHP = 100, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });
        // 100% chance poison on hit
        var weapon = new ItemTemplate("viper_test", "Viper Fang", "Test", ItemCategory.Weapon, EquipSlot.MainHand,
            new Dictionary<string, int>(), null, 0, 1, "uncommon", DamageMin: 2, DamageMax: 5, CritChance: 15, WeaponAccuracy: 95,
            OnHitEffects: new WeaponOnHitEffect[] { new(StatusEffectType.Poisoned, 100, 5) });

        var resolver = new CombatResolver(101);
        var applied = resolver.ProcessOnHitEffects(defender, weapon);

        Expect.True(applied.Count > 0, "On-hit poison should apply with 100% chance");
        Expect.Equal(StatusEffectType.Poisoned, applied[0].Type, "Applied effect should be poisoned");
        Expect.True(StatusEffectProcessor.HasEffect(defender, StatusEffectType.Poisoned), "Defender should have poisoned status");
    }

    private static void EmpoweredDamage()
    {
        var attacker = new StubEntity("Hero", Position.Zero, Faction.Player, stats: new Stats { HP = 20, MaxHP = 20, Attack = 6, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        StatusEffectProcessor.ApplyEffect(attacker, StatusEffectType.Empowered, 3);

        var resolver = new CombatResolver(42);
        // Use a fixed weapon to get deterministic base
        var weapon = new ItemTemplate("test_sword", "Test", "Test", ItemCategory.Weapon, EquipSlot.MainHand,
            new Dictionary<string, int>(), null, 0, 1, "common", DamageMin: 4, DamageMax: 4, CritChance: 0);

        var damage = resolver.CalculateWeaponDamage(attacker, weapon, false);
        // base 4, attack bonus 6/2=3, empowered +3 => 10
        Expect.Equal(10, damage, "Empowered should add +3 to weapon damage calculation");
    }

    private static void CorrodedArmor()
    {
        var defender = new StubEntity("Defender", Position.Zero, stats: new Stats { HP = 20, MaxHP = 20, Attack = 1, Defense = 6, Accuracy = 0, Evasion = 0, Speed = 100 });
        var resolver = new CombatResolver(42);

        var normalReduction = resolver.ApplyArmor(10, defender, DamageType.Physical);
        // armor=6, reduction=3, final=7

        StatusEffectProcessor.ApplyEffect(defender, StatusEffectType.Corroded, 3);
        var corrodedReduction = resolver.ApplyArmor(10, defender, DamageType.Physical);
        // armor=6-2=4, reduction=2, final=8

        Expect.True(corrodedReduction > normalReduction, "Corroded should result in more damage taken");
        Expect.Equal(8, corrodedReduction, "Corroded should reduce effective armor by 2");
    }

    private static void UnarmedBaseAttack()
    {
        var attacker = new StubEntity("Hero", Position.Zero, Faction.Player, stats: new Stats { HP = 20, MaxHP = 20, Attack = 8, Defense = 1, Accuracy = 0, Evasion = 0, Speed = 100 });
        var defender = new StubEntity("Dummy", new Position(1, 0), stats: new Stats { HP = 100, MaxHP = 100, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });

        var resolver = new CombatResolver(42);
        var hitCount = 0;
        for (var i = 0; i < 20; i++)
        {
            var result = resolver.ResolveMeleeAttack(attacker, defender, i);
            if (!result.IsMiss)
            {
                // Unarmed: attack stat (8) + variance, no weapon fields used
                Expect.True(result.RawDamage >= 1, "Unarmed damage should be at least 1");
                hitCount++;
            }
        }

        Expect.True(hitCount > 0, "At least one unarmed attack should have hit");
    }

    private static void AttackActionEquippedWeapon()
    {
        var db = new StubContentDatabase();
        var world = new WorldState();
        world.InitGrid(5, 5);
        world.Seed = 0;
        world.ContentDatabase = db;

        for (var x = 0; x < 5; x++)
        for (var y = 0; y < 5; y++)
            world.SetTile(new Position(x, y), TileType.Floor);

        var actor = new StubEntity("Hero", new Position(0, 0), Faction.Player, stats: new Stats { HP = 30, MaxHP = 30, Attack = 6, Defense = 3, Accuracy = 50, Evasion = 0, Speed = 100 });
        var target = new StubEntity("Goblin", new Position(1, 0), Faction.Enemy, stats: new Stats { HP = 100, MaxHP = 100, Attack = 3, Defense = 0, Accuracy = 0, Evasion = 0, Speed = 100 });

        var inventory = new InventoryComponent();
        var swordInstance = new ItemInstance { TemplateId = "sword_iron" };
        inventory.Add(swordInstance);
        inventory.TryEquip(swordInstance, EquipSlot.MainHand, new Dictionary<string, int> { ["attack"] = 2 }, out _);
        actor.SetComponent(inventory);

        world.AddEntity(actor);
        world.AddEntity(target);
        world.Player = actor;

        var action = new AttackAction(actor.Id, target.Id);
        var outcome = action.Execute(world);

        Expect.Equal(ActionResult.Success, outcome.Result, "Attack action should succeed");
        Expect.True(outcome.LogMessages.Count > 0, "Attack action should produce log messages");

        // Check that weapon name appears in at least one message
        var hasWeaponRef = false;
        foreach (var msg in outcome.LogMessages)
        {
            if (msg.Contains("Iron Sword"))
            {
                hasWeaponRef = true;
                break;
            }
        }

        Expect.True(hasWeaponRef, "Attack log should mention the equipped weapon name");
    }
}
