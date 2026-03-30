namespace Roguelike.Core;

public sealed record StatusEffectInstance(
    StatusEffectType Type,
    int RemainingTurns,
    int Magnitude);
