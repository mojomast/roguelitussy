using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class BossPhaseComponent
{
    public int CurrentPhase { get; set; } = 1;

    public List<int> TriggeredPhases { get; } = new();
}
