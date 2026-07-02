using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class SynergyComponent
{
    public List<string> AppliedPassiveSynergyIds { get; } = new();
}
