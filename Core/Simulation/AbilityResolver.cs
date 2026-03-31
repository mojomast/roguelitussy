using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class AbilityResolver
{
    public static List<IEntity> ResolveTargets(AbilityTemplate ability, IEntity caster, Position targetPos, WorldState world)
    {
        var targets = new List<IEntity>();

        switch (ability.Targeting.Type)
        {
            case "self":
                targets.Add(caster);
                break;

            case "single":
                var entityAtTarget = world.GetEntityAt(targetPos);
                if (entityAtTarget is not null && entityAtTarget.IsAlive)
                {
                    targets.Add(entityAtTarget);
                }
                break;

            case "tile":
                break;

            case "aoe_circle":
                var center = string.Equals(ability.Targeting.Center, "self", StringComparison.OrdinalIgnoreCase)
                    ? caster.Position
                    : targetPos;
                var entitiesInRadius = world.GetEntitiesInRadius(center, ability.Targeting.Radius);
                foreach (var entity in entitiesInRadius)
                {
                    if (!entity.IsAlive)
                    {
                        continue;
                    }

                    targets.Add(entity);
                }
                break;
        }

        return targets;
    }

    public static int CalculateAbilityDamage(AbilityEffect effect, IEntity caster, Random rng)
    {
        var baseDamage = effect.BaseValue;

        if (!string.IsNullOrEmpty(effect.ScalingStat))
        {
            var statValue = GetStatValue(caster, effect.ScalingStat);
            baseDamage += (int)(statValue * effect.ScalingFactor);
        }

        var variance = rng.Next(-1, 2);
        return Math.Max(1, baseDamage + variance);
    }

    public static bool HasLineOfSight(Position from, Position to, IWorldState world)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = Math.Abs(to.Y - from.Y);
        var sx = from.X < to.X ? 1 : -1;
        var sy = from.Y < to.Y ? 1 : -1;
        var err = dx - dy;

        var x = from.X;
        var y = from.Y;

        while (x != to.X || y != to.Y)
        {
            var e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }

            if (x == to.X && y == to.Y)
            {
                break;
            }

            if (world.BlocksSight(new Position(x, y)))
            {
                return false;
            }
        }

        return true;
    }

    public static List<IEntity> FilterByRelation(List<IEntity> targets, IEntity caster, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return targets;
        }

        return filter switch
        {
            "enemies" => targets.Where(t => t.Faction != caster.Faction).ToList(),
            "allies" => targets.Where(t => t.Faction == caster.Faction).ToList(),
            _ => targets,
        };
    }

    private static int GetStatValue(IEntity entity, string stat)
    {
        return stat.ToLowerInvariant() switch
        {
            "attack" => entity.Stats.Attack,
            "defense" => entity.Stats.Defense,
            "accuracy" => entity.Stats.Accuracy,
            "evasion" => entity.Stats.Evasion,
            "speed" => entity.Stats.Speed,
            _ => 0,
        };
    }
}
