using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record AbilityTemplate(
    string AbilityId,
    string DisplayName,
    string Description,
    AbilityTargeting Targeting,
    int EnergyCost,
    int? ManaCost,
    IReadOnlyList<AbilityEffect> Effects);

public sealed record AbilityTargeting(
    string Type,
    int Range,
    int Radius,
    bool RequiresLos,
    bool RequiresWalkable,
    bool HitsAllies,
    string? Center);

public sealed record AbilityEffect(
    string Type,
    DamageType DamageType,
    int BaseValue,
    string? ScalingStat,
    double ScalingFactor,
    string? StatusEffect,
    int StatusChance,
    int StatusDuration,
    string? Filter,
    string? ValueSource,
    double HealFactor,
    string? Destination);
