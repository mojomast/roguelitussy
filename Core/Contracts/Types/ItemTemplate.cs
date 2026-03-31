using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record WeaponOnHitEffect(StatusEffectType StatusEffect, int Chance, int Duration);

public sealed record ItemTemplate(
    string TemplateId,
    string DisplayName,
    string Description,
    ItemCategory Category,
    EquipSlot Slot,
    IReadOnlyDictionary<string, int> StatModifiers,
    string? UseEffect,
    int MaxCharges,
    int MaxStack,
    string Rarity,
    int DamageMin = 0,
    int DamageMax = 0,
    int CritChance = 0,
    int WeaponAccuracy = 0,
    int SpeedModifier = 0,
    IReadOnlyList<WeaponOnHitEffect>? OnHitEffects = null,
    IReadOnlyDictionary<string, int>? Requirements = null);
