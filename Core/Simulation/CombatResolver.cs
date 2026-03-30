using System;

namespace Roguelike.Core;

public sealed class CombatResolver
{
    private readonly Random _rng;

    public CombatResolver(Random rng)
    {
        _rng = rng;
    }

    public DamageResult ResolveAttack(IEntity attacker, IEntity defender, IWorldState world)
    {
        int hitChance = Math.Clamp(80 + attacker.Stats.Accuracy - defender.Stats.Evasion, 5, 95);
        int hitRoll = _rng.Next(100);

        if (hitRoll >= hitChance)
        {
            return new DamageResult(
                AttackerId: attacker.Id,
                DefenderId: defender.Id,
                RawDamage: 0,
                FinalDamage: 0,
                DamageType: DamageType.Physical,
                IsCritical: false,
                IsMiss: true,
                IsKill: false
            );
        }

        bool isCrit = _rng.Next(100) < 5;

        int rawDamage = attacker.Stats.Attack;

        // Add weapon bonus if equipped
        var inventory = attacker.GetComponent<Simulation.Inventory>();
        if (inventory != null)
        {
            // Check for weapon stat modifiers (would need content DB lookup in a full implementation)
        }

        if (isCrit)
            rawDamage *= 2;

        int finalDamage = Math.Max(1, rawDamage - defender.Stats.Defense);

        defender.Stats.HP -= finalDamage;
        bool isKill = !defender.Stats.IsAlive;

        return new DamageResult(
            AttackerId: attacker.Id,
            DefenderId: defender.Id,
            RawDamage: rawDamage,
            FinalDamage: finalDamage,
            DamageType: DamageType.Physical,
            IsCritical: isCrit,
            IsMiss: false,
            IsKill: isKill
        );
    }
}
