using System;

namespace Roguelike.Core;

public sealed class CombatResolver
{
    private const int BaseHitChance = 80;
    private const int DefaultCritChance = 5;
    private readonly Random _rng;

    public CombatResolver(int seed)
        : this(new Random(seed))
    {
    }

    public CombatResolver(Random rng)
    {
        _rng = rng;
    }

    public int CalculateHitChance(IEntity attacker, IEntity defender)
    {
        var hitChance = BaseHitChance + ((attacker.Stats.Attack - defender.Stats.Defense) * 2) + attacker.Stats.Accuracy - defender.Stats.Evasion;
        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Invisible))
        {
            hitChance += 10;
        }

        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Weakened))
        {
            hitChance -= 10;
        }

        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Stunned))
        {
            hitChance -= 20;
        }

        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Frozen))
        {
            hitChance -= 10;
        }

        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Burning))
        {
            hitChance -= 5;
        }

        if (StatusEffectProcessor.HasEffect(defender, StatusEffectType.Frozen))
        {
            hitChance += 20;
        }

        return Math.Clamp(hitChance, 5, 95);
    }

    public bool RollHit(IEntity attacker, IEntity defender) => _rng.Next(100) < CalculateHitChance(attacker, defender);

    public DamageResult ResolveMeleeAttack(IEntity attacker, IEntity defender, int turnNumber)
    {
        if (!RollHit(attacker, defender))
        {
            return new DamageResult(attacker.Id, defender.Id, 0, 0, DamageType.Physical, false, true, false);
        }

        var isCritical = _rng.Next(100) < DefaultCritChance;
        var rawDamage = CalculateRawDamage(attacker, isCritical);
        var finalDamage = ApplyArmor(rawDamage, defender, DamageType.Physical);
        var isKill = defender.Stats.HP - finalDamage <= 0;

        return new DamageResult(attacker.Id, defender.Id, rawDamage, finalDamage, DamageType.Physical, isCritical, false, isKill);
    }

    public int CalculateRawDamage(IEntity attacker, bool isCritical)
    {
        var attack = attacker.Stats.Attack;
        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Weakened))
        {
            attack = Math.Max(1, attack - 2);
        }

        var variance = _rng.Next(-2, 3);
        var rawDamage = Math.Max(1, attack + variance);
        return isCritical ? rawDamage * 2 : rawDamage;
    }

    public int ApplyArmor(int rawDamage, IEntity defender, DamageType damageType)
    {
        var armor = defender.Stats.Defense + (StatusEffectProcessor.GetMagnitude(defender, StatusEffectType.Shielded) * 3);
        var reduction = armor / 2;
        return Math.Max(1, rawDamage - reduction);
    }
}