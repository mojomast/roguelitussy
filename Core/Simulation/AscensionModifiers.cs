using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class AscensionModifiers
{
    public static int GetCurrentAscensionLevel() => 0;

    public static List<AscensionModifier> GetActiveModifiers(int level, IContentDatabase content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var clamped = Math.Clamp(level, 0, 10);
        return content.AscensionModifiers.Values
            .Where(modifier => modifier.AscensionLevel <= clamped)
            .OrderBy(modifier => modifier.AscensionLevel)
            .ThenBy(modifier => modifier.ModifierId, StringComparer.Ordinal)
            .ToList();
    }

    public static bool HasModifier(string modifierId, int level, IContentDatabase content) =>
        GetActiveModifiers(level, content).Any(modifier => string.Equals(modifier.ModifierId, modifierId, StringComparison.Ordinal));
}
