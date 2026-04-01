using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record PerkTemplate(
    string TemplateId,
    string DisplayName,
    string Description,
    int UnlockLevel,
    IReadOnlyList<PerkEffect> Effects);

public sealed record PerkEffect(
    string Type,
    string? Stat,
    int Value);