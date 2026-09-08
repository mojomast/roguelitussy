using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record RaceDefinition(string Id, string HeritageAbilityId);

public static class RaceDefinitions
{
    public const string DefaultId = "human";

    public static readonly IReadOnlyDictionary<string, RaceDefinition> All = new Dictionary<string, RaceDefinition>(StringComparer.Ordinal)
    {
        ["human"] = new("human", "war_cry"),
        ["elf"] = new("elf", "phase_shift"),
        ["dwarf"] = new("dwarf", "ground_slam"),
        ["orc"] = new("orc", "heavy_slam"),
    };

    public static RaceDefinition Get(string? raceId) => All[NormalizeId(raceId)];

    public static string NormalizeId(string? raceId)
    {
        if (string.IsNullOrWhiteSpace(raceId))
        {
            return DefaultId;
        }

        var normalized = raceId.Trim().ToLowerInvariant().Replace(' ', '_');
        return All.ContainsKey(normalized) ? normalized : DefaultId;
    }
}
