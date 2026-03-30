using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record CombatEvent(
    int TurnNumber,
    ActionType ActionType,
    IReadOnlyList<DamageResult> DamageResults,
    IReadOnlyList<StatusEffectInstance> StatusEffectsApplied);
