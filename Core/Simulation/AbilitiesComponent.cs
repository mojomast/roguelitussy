using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class AbilitiesComponent
{
    public List<EnemyAbilitySlot> Slots { get; } = new();
}

public sealed class EnemyAbilitySlot
{
    public string AbilityId { get; init; } = "";
    public int Cooldown { get; init; }
    public int Priority { get; init; } = 50;
}
