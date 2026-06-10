using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public static class DeathResolver
{
    public sealed record DeathResolution(
        bool Removed,
        int KillsAwarded,
        ProgressionService.AwardResult ProgressionAward);

    public static DeathResolution ResolveKill(WorldState world, IEntity killer, IEntity victim)
    {
        if (world.GetEntity(victim.Id) is null)
        {
            return new DeathResolution(false, 0, new ProgressionService.AwardResult(0, 0, Array.Empty<int>()));
        }

        victim.Stats.HP = 0;

        var progression = killer.GetComponent<ProgressionComponent>();
        if (progression is null)
        {
            world.RemoveEntity(victim.Id);
            return new DeathResolution(true, 0, new ProgressionService.AwardResult(0, 0, Array.Empty<int>()));
        }

        progression.Kills++;

        var xpValue = victim.GetComponent<XpValueComponent>();
        var award = xpValue is null
            ? new ProgressionService.AwardResult(0, 0, Array.Empty<int>())
            : ProgressionService.AwardExperience(killer, xpValue.Value);

        world.RemoveEntity(victim.Id);
        return new DeathResolution(true, 1, award);
    }

    public static DeathResolution ResolveUnattributedDeath(WorldState world, IEntity victim)
    {
        if (world.GetEntity(victim.Id) is null)
        {
            return new DeathResolution(false, 0, new ProgressionService.AwardResult(0, 0, Array.Empty<int>()));
        }

        victim.Stats.HP = 0;
        world.RemoveEntity(victim.Id);
        return new DeathResolution(true, 0, new ProgressionService.AwardResult(0, 0, Array.Empty<int>()));
    }

    public static void AppendProgressionLogMessages(ICollection<string> logMessages, string killerName, DeathResolution resolution)
    {
        if (resolution.ProgressionAward.ExperienceGained > 0)
        {
            logMessages.Add($"{killerName} gains {resolution.ProgressionAward.ExperienceGained} XP.");
        }

        foreach (var level in resolution.ProgressionAward.ReachedLevels)
        {
            logMessages.Add($"{killerName} reaches level {level}!");
        }
    }
}
