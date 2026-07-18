using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class RelicHookContext
{
    public EntityId? TargetId { get; init; }

    public int? DamageAmount { get; init; }

    public string? ItemTag { get; init; }

    public string? EnemyTag { get; init; }

    public int ModifiedValue { get; set; }

    public List<string> LogMessages { get; } = new();
}
