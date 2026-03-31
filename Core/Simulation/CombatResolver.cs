using System;
using System.Collections.Generic;

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

        if (StatusEffectProcessor.HasEffect(defender, StatusEffectType.Frozen))
        {
            hitChance += 20;
        }

        return Math.Clamp(hitChance, 5, 95);
    }

    public bool RollHit(IEntity attacker, IEntity defender) => _rng.Next(100) < CalculateHitChance(attacker, defender);

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
        return isCritical ? rawDamage * 2 : rawDamage;
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
        return isCritical ? rawDamage * 2 : rawDamage;
    }

    public int ApplyArmor(int rawDamage, IEntity defender, DamageType damageType)
    {
        var armor = defender.Stats.Defense + (StatusEffectProcessor.GetMagnitude(defender, StatusEffectType.Shielded) * 3);

        if (StatusEffectProcessor.HasEffect(defender, StatusEffectType.Corroded))
        {
            armor = Math.Max(0, armor - 2);
        }

        var reduction = armor / 2;
        return Math.Max(1, rawDamage - reduction);
    }

    public IReadOnlyList<StatusEffectInstance> ProcessOnHitEffects(IEntity defender, ItemTemplate? weapon)
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

            StatusEffectProcessor.ApplyEffect(defender, effect.StatusEffect, effect.Duration);
            var instance = StatusEffectProcessor.GetEffect(defender, effect.StatusEffect);
            if (instance is not null)
            {
                applied.Add(instance);
            }
        }

        return applied;
    }
}