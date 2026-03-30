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