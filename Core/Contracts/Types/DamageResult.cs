namespace Roguelike.Core;

public sealed record DamageResult(
    EntityId AttackerId,
    EntityId DefenderId,
    int RawDamage,
    int FinalDamage,
    DamageType DamageType,
    bool IsCritical,
    bool IsMiss,
    bool IsKill
);
