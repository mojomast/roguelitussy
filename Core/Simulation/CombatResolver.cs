using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class CombatResolver
{
    private const int BaseHitChance = 80;
    private const int DefaultCritChance = 5;
    private readonly DeterministicRandom _rng;

    public CombatResolver(int seed)
        : this(new DeterministicRandom(seed))
    {
    }

    public CombatResolver(ulong state)
        : this(new DeterministicRandom(state))
    {
    }

    public CombatResolver(Random rng)
        : this(new DeterministicRandom(rng.Next()))
    {
    }

    private CombatResolver(DeterministicRandom rng)
    {
        _rng = rng;
    }

    public ulong RandomState => _rng.State;

    public int NextRandom(int maxExclusive) => _rng.Next(maxExclusive);

    public int NextRandom(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

    public int CalculateHitChance(IEntity attacker, IEntity defender, ItemTemplate? weapon = null)
    {
        var hitChance = BaseHitChance + ((attacker.Stats.Attack - defender.Stats.Defense) * 2) + attacker.Stats.Accuracy - defender.Stats.Evasion;

        if (weapon is not null)
        {
            hitChance += weapon.WeaponAccuracy;
        }

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

        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Blinded))
        {
            hitChance -= 25;
        }

        if (StatusEffectProcessor.HasEffect(defender, StatusEffectType.Frozen))
        {
            hitChance += 20;
        }

        return Math.Clamp(hitChance, 5, 95);
    }

    public DamageResult ResolveMeleeAttack(IEntity attacker, IEntity defender, int turnNumber)
    {
        return ResolveMeleeAttack(attacker, defender, turnNumber, null);
    }

    public DamageResult ResolveMeleeAttack(IEntity attacker, IEntity defender, int turnNumber, ItemTemplate? weapon)
    {
        if (_rng.Next(100) >= CalculateHitChance(attacker, defender, weapon))
        {
            return new DamageResult(attacker.Id, defender.Id, 0, 0, DamageType.Physical, false, true, false);
        }

        var critChance = weapon is not null ? weapon.CritChance : DefaultCritChance;
        if (attacker.Stats.Accuracy - defender.Stats.Evasion > 40)
        {
            critChance = Math.Max(critChance, 15);
        }

        var isCritical = _rng.Next(100) < critChance;
        var rawDamage = weapon is not null && weapon.DamageMax > 0
            ? CalculateWeaponDamage(attacker, weapon, isCritical)
            : CalculateRawDamage(attacker, isCritical);
        var finalDamage = ApplyArmor(rawDamage, defender, DamageType.Physical);
        var isKill = defender.Stats.HP - finalDamage <= 0;

        return new DamageResult(attacker.Id, defender.Id, rawDamage, finalDamage, DamageType.Physical, isCritical, false, isKill);
    }

    public int CalculateWeaponDamage(IEntity attacker, ItemTemplate weapon, bool isCritical)
    {
        var baseDamage = weapon.DamageMin == weapon.DamageMax
            ? weapon.DamageMin
            : _rng.Next(weapon.DamageMin, weapon.DamageMax + 1);

        var attackBonus = attacker.Stats.Attack / 2;
        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Weakened))
        {
            attackBonus = Math.Max(0, attackBonus - 2);
        }

        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Empowered))
        {
            attackBonus += 3;
        }

        var rawDamage = Math.Max(1, baseDamage + attackBonus);
        return isCritical ? ApplyCriticalMultiplier(rawDamage) : rawDamage;
    }

    public int CalculateRawDamage(IEntity attacker, bool isCritical)
    {
        var attack = attacker.Stats.Attack;
        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Weakened))
        {
            attack = Math.Max(1, attack - 2);
        }

        if (StatusEffectProcessor.HasEffect(attacker, StatusEffectType.Empowered))
        {
            attack += 3;
        }

        var variance = _rng.Next(-2, 3);
        var rawDamage = Math.Max(1, attack + variance);
        return isCritical ? ApplyCriticalMultiplier(rawDamage) : rawDamage;
    }

    private static int ApplyCriticalMultiplier(int rawDamage) => Math.Max(1, (int)Math.Ceiling(rawDamage * 1.5));

    public int ApplyArmor(int rawDamage, IEntity defender, DamageType damageType)
    {
        if (damageType == DamageType.Physical && StatusEffectProcessor.HasFlag(defender, "immune_physical"))
        {
            return 0;
        }

        var armor = defender.Stats.Defense + (StatusEffectProcessor.GetMagnitude(defender, StatusEffectType.Shielded) * 3);

        if (StatusEffectProcessor.HasEffect(defender, StatusEffectType.Corroded))
        {
            armor = Math.Max(0, armor - 2);
        }

        var reduction = armor / 2;
        return Math.Max(1, rawDamage - reduction);
    }

    public IReadOnlyList<StatusEffectInstance> ProcessOnHitEffects(IEntity defender, ItemTemplate? weapon, EntityId? sourceEntityId = null)
    {
        var applied = new List<StatusEffectInstance>();
        if (weapon?.OnHitEffects is null)
        {
            return applied;
        }

        foreach (var effect in weapon.OnHitEffects)
        {
            if (effect.Chance <= 0 || effect.Duration <= 0)
            {
                continue;
            }

            if (_rng.Next(100) >= effect.Chance)
            {
                continue;
            }

            StatusEffectProcessor.ApplyEffect(defender, effect.StatusEffect, effect.Duration, sourceEntityId: sourceEntityId);
            var instance = StatusEffectProcessor.GetEffect(defender, effect.StatusEffect);
            if (instance is not null)
            {
                applied.Add(instance);
            }
        }

        return applied;
    }
}
