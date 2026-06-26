using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record TrapTemplate(
    string TemplateId,
    string DisplayName,
    string Description,
    int DamageMin,
    int DamageMax,
    DamageType DamageType,
    string? StatusEffect,
    int StatusDuration,
    int StatusMagnitude,
    IReadOnlyList<string>? AvoidFlags = null,
    int TriggerChance = 100,
    string? AbilityId = null,
    string SpritePath = "",
    IReadOnlyList<int>? SpriteAtlasCoords = null);
