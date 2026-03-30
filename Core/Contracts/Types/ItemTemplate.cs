using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record ItemTemplate(
    string TemplateId,
    string DisplayName,
    string Description,
    ItemCategory Category,
    EquipSlot Slot,
    IReadOnlyDictionary<string, int> StatModifiers,
    string? UseEffect,
    int MaxCharges,
    int MaxStack);
