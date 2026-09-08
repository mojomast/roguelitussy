using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class CombatResolverTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.CombatResolver computes hit chance from attack and defense", ComputesHitChance);
        registry.Add("Simulation.CombatResolver clamps hit chance", ClampsHitChance);
        registry.Add("Simulation.CombatResolver frozen defender is easier to hit", FrozenDefenderBonus);
        registry.Add("Simulation.CombatResolver armor cannot reduce damage below one", ArmorCannotReduceBelowOne);
        registry.Add("Simulation.CombatResolver resolves deterministic melee hits", ResolvesDeterministicHit);
        registry.Add("Simulation.CombatResolver authored on-hit stacking and refresh", () => OnHitStatusRulesFollowContent(true));
        registry.Add("Simulation.CombatResolver legacy on-hit stacking and refresh", () => OnHitStatusRulesFollowContent(false));
        registry.Add("Simulation.CombatResolver melee action propagates status content", () => WeaponActionPropagatesContent(false));
        registry.Add("Simulation.CombatResolver ranged action propagates status content", () => WeaponActionPropagatesContent(true));
    }

    private static void OnHitStatusRulesFollowContent(bool withContent)
    {
        var content = withContent ? ContentLoader.LoadFromRepository() : null;
        var attacker = new StubEntity("Attacker", Position.Zero);
        var defender = new StubEntity("Defender", new Position(1, 0));
        var weapon = new ItemTemplate("test_weapon", "Test Weapon", "", ItemCategory.Weapon, EquipSlot.MainHand,
            new Dictionary<string, int>(), null, 0, 1, "common", OnHitEffects: new[]
            {
                new WeaponOnHitEffect(StatusEffectType.Poisoned, 100, 3),
                new WeaponOnHitEffect(StatusEffectType.Stunned, 100, 3),
            });
        StatusEffectProcessor.ApplyEffect(defender, StatusEffectType.Stunned, 1);
        var resolver = new CombatResolver(7);

        Expect.Equal(2, resolver.ProcessOnHitEffects(defender, weapon, attacker.Id, content).Count, "Both effects should apply");
        Expect.Equal(2, resolver.ProcessOnHitEffects(defender, weapon, attacker.Id, content).Count, "Both effects should reapply");

        var poison = StatusEffectProcessor.GetEffect(defender, StatusEffectType.Poisoned)!;
        Expect.Equal(withContent ? 1 : 2, poison.Magnitude, "Weapon poison must follow content stacking rules");
        Expect.True(poison.SourceEntityId == attacker.Id, "Weapon status attribution must survive content propagation");
        Expect.Equal(withContent ? 1 : 3, StatusEffectProcessor.GetEffect(defender, StatusEffectType.Stunned)!.RemainingTurns,
            "Weapon stun must follow content refresh rules");
    }

    private static void WeaponActionPropagatesContent(bool ranged)
    {
        var content = new StubContentDatabase();
        var authored = ContentLoader.LoadFromRepository();
        var statuses = (Dictionary<string, StatusEffectDefinition>)content.StatusEffects;
        Expect.True(authored.TryGetStatusEffect("poisoned", out var poisonDefinition), "Authored poison should exist");
        statuses["poisoned"] = poisonDefinition;
        var items = (Dictionary<string, ItemTemplate>)content.ItemTemplates;
        var weapon = new ItemTemplate("test_weapon", "Test Weapon", "", ItemCategory.Weapon, EquipSlot.MainHand,
            new Dictionary<string, int>(), null, 0, 1, "common", DamageMin: 1, DamageMax: 1,
            OnHitEffects: new[] { new WeaponOnHitEffect(StatusEffectType.Poisoned, 100, 3) }, Tags: new[] { "ranged" });
        items[weapon.TemplateId] = weapon;
        var world = new WorldState { ContentDatabase = content, Seed = 7 };
        world.InitGrid(8, 3);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var attacker = new StubEntity("Attacker", new Position(1, 1), Faction.Player,
            stats: new Stats { HP = 100, MaxHP = 100, Attack = 1, Accuracy = 100 });
        var defender = new StubEntity("Defender", new Position(ranged ? 5 : 2, 1), Faction.Enemy,
            stats: new Stats { HP = 1000, MaxHP = 1000 });
        var inventory = new InventoryComponent();
        attacker.SetComponent(inventory);
        var item = new ItemInstance { TemplateId = weapon.TemplateId };
        inventory.Add(item);
        Expect.True(inventory.TryEquip(item, EquipSlot.MainHand, weapon.StatModifiers, out _), "Test weapon should equip");
        inventory.Add(new ItemInstance { TemplateId = RangedAttackAction.ArrowTemplateId, StackCount = 20 });
        world.Player = attacker;
        world.AddEntity(attacker);
        world.AddEntity(defender);
        world.SetVisible(defender.Position, true);
        IAction action = ranged ? new RangedAttackAction(attacker.Id, defender.Id) : new AttackAction(attacker.Id, defender.Id);
        var hits = 0;
        for (var attempt = 0; attempt < 20 && hits < 2; attempt++)
        {
            var outcome = action.Execute(world);
            Expect.Equal(ActionResult.Success, outcome.Result, "Weapon action should succeed");
            if (outcome.CombatEvents.SelectMany(evt => evt.DamageResults).Any(damage => !damage.IsMiss))
            {
                hits++;
            }
        }

        Expect.Equal(2, hits, "Seeded attacks should land twice within the bounded attempts");
        Expect.Equal(1, StatusEffectProcessor.GetMagnitude(defender, StatusEffectType.Poisoned),
            "Weapon actions must forward world content so authored poison does not stack");
        Expect.True(StatusEffectProcessor.GetEffect(defender, StatusEffectType.Poisoned)!.SourceEntityId == attacker.Id,
            "Action-applied poison must retain attacker attribution");
    }

    private static void ComputesHitChance()
    {
        var attacker = new StubEntity("Attacker", Position.Zero, stats: new Stats { HP = 10, MaxHP = 10, Attack = 10, Defense = 1, Accuracy = 0, Evasion = 0 });
        var defender = new StubEntity("Defender", new Position(1, 0), stats: new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 5, Accuracy = 0, Evasion = 0 });
        var resolver = new CombatResolver(7);

        Expect.Equal(90, resolver.CalculateHitChance(attacker, defender), "Hit chance should follow the documented formula");
    }

    private static void ClampsHitChance()
    {
        var attacker = new StubEntity("Attacker", Position.Zero, stats: new Stats { HP = 10, MaxHP = 10, Attack = 1, Defense = 0, Accuracy = 0, Evasion = 0 });
        var accurateDefender = new StubEntity("AccurateDefender", new Position(1, 0), stats: new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 20, Accuracy = 0, Evasion = 0 });
        var clampedDefender = new StubEntity("ClampedDefender", new Position(1, 0), stats: new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 60, Accuracy = 0, Evasion = 0 });
        var resolver = new CombatResolver(11);

        Expect.Equal(79, resolver.CalculateHitChance(new StubEntity("Accurate", Position.Zero, stats: new Stats { HP = 10, MaxHP = 10, Attack = 1, Defense = 0, Accuracy = 37, Evasion = 0 }), accurateDefender), "Accuracy and evasion should adjust hit chance when present");
        Expect.Equal(5, resolver.CalculateHitChance(attacker, clampedDefender), "Hit chance should clamp to a minimum of five percent");
    }

    private static void FrozenDefenderBonus()
    {
        var attacker = new StubEntity("Attacker", Position.Zero, stats: new Stats { HP = 10, MaxHP = 10, Attack = 5, Defense = 0, Accuracy = 0, Evasion = 0 });
        var defender = new StubEntity("Defender", new Position(1, 0), stats: new Stats { HP = 10, MaxHP = 10, Attack = 3, Defense = 5, Accuracy = 0, Evasion = 0 });
        StatusEffectProcessor.ApplyEffect(defender, StatusEffectType.Frozen, 2);

        var resolver = new CombatResolver(19);
        Expect.Equal(95, resolver.CalculateHitChance(attacker, defender), "Frozen defenders should gain the documented hit chance penalty");
    }

    private static void ArmorCannotReduceBelowOne()
    {
        var defender = new StubEntity("Defender", Position.Zero, stats: new Stats { HP = 10, MaxHP = 10, Attack = 2, Defense = 999, Accuracy = 0, Evasion = 0 });
        var resolver = new CombatResolver(23);

        Expect.Equal(1, resolver.ApplyArmor(1, defender, DamageType.Physical), "Armor should never reduce hit damage below one");
    }

    private static void ResolvesDeterministicHit()
    {
        var attacker = new StubEntity("Attacker", Position.Zero, stats: new Stats { HP = 20, MaxHP = 20, Attack = 12, Defense = 1, Accuracy = 0, Evasion = 0 });
        var defender = new StubEntity("Defender", new Position(1, 0), stats: new Stats { HP = 12, MaxHP = 12, Attack = 3, Defense = 1, Accuracy = 0, Evasion = 0 });
        var resolver = new CombatResolver(0);

        var damage = resolver.ResolveMeleeAttack(attacker, defender, 1);

        Expect.False(damage.IsMiss, "Seeded melee attack should hit in this scenario");
        Expect.True(damage.FinalDamage >= 1, "Successful hits should deal at least one damage");
        Expect.Equal(attacker.Id, damage.AttackerId, "Damage result should record the attacker");
    }
}
