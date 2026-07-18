using System;
using System.Linq;

namespace Roguelike.Core;

public static class UtilityScorer
{
    public static float ScoreAction(
        IAction action,
        IEntity self,
        IEntity? target,
        Position objective,
        IWorldState world,
        AIStateComponent memory,
        AIProfile profile,
        IPathfinder pathfinder)
    {
        return action switch
        {
            AttackAction attack => ScoreAttack(attack, self, target, memory, profile),
            MoveAction move => ScoreMove(move, self, target, objective, world, memory, profile, pathfinder),
            CastAbilityAction cast => ScoreCast(cast, self, target, world, memory, profile),
            WaitAction => ScoreWait(self, target, world, memory, profile),
            _ => 0f,
        };
    }

    private static float ScoreAttack(AttackAction action, IEntity self, IEntity? target, AIStateComponent memory, AIProfile profile)
    {
        if (target is null || action.TargetId != target.Id || self.Position.ChebyshevTo(target.Position) > 1)
        {
            return 0f;
        }

        if (memory.State == AIState.Flee)
        {
            return 0.05f;
        }

        var targetHpRatio = target.Stats.MaxHP <= 0 ? 1f : (float)target.Stats.HP / target.Stats.MaxHP;
        var score = 1.10f + profile.AggressionWeight;
        score += (1f - targetHpRatio) * 0.40f;

        if (memory.State == AIState.Attack)
        {
            score += 0.35f;
        }

        if (profile.PreferredRange > 1)
        {
            score -= 1.50f;
        }

        return score;
    }

    private static float ScoreMove(
        MoveAction action,
        IEntity self,
        IEntity? target,
        Position objective,
        IWorldState world,
        AIStateComponent memory,
        AIProfile profile,
        IPathfinder pathfinder)
    {
        var next = self.Position + action.Delta;

        if (memory.State == AIState.Flee)
        {
            return ScoreFleeMove(self, target, next, profile);
        }

        if (objective == Position.Invalid)
        {
            return 0.10f;
        }

        var currentDistance = GetPathDistance(self.Position, objective, world, pathfinder, profile.PhaseThroughWalls);
        var nextDistance = GetPathDistance(next, objective, world, pathfinder, profile.PhaseThroughWalls);

        var baseScore = memory.State switch
        {
            AIState.Chase or AIState.Attack => profile.ChaseWeight,
            AIState.Patrol => profile.PatrolWeight,
            _ => 0.10f,
        };

        if (currentDistance == int.MaxValue && nextDistance == int.MaxValue)
        {
            return baseScore * 0.25f;
        }

        var score = baseScore;
        if (nextDistance < currentDistance)
        {
            score += 1.00f + ((currentDistance - nextDistance) * 0.10f);
        }
        else if (nextDistance == currentDistance)
        {
            score += 0.05f;
        }
        else
        {
            score -= 0.60f;
        }

        if (target is not null)
        {
            score += ScoreKitingMove(self, target, next, profile, world);
            score += ScoreMinRangeMove(self, target, next, profile);
        }

        if (profile.Name == "support")
        {
            score += ScoreSupportMove(self, world, next, profile);
        }

        return score;
    }

    private static float ScoreKitingMove(IEntity self, IEntity target, Position next, AIProfile profile, IWorldState world)
    {
        if (profile.PreferredRange <= 1)
        {
            return 0f;
        }

        var currentDistance = self.Position.DistanceTo(target.Position);
        var nextDistance = next.DistanceTo(target.Position);
        var currentChebyshev = self.Position.ChebyshevTo(target.Position);
        var nextChebyshev = next.ChebyshevTo(target.Position);
        var score = 0f;

        if (currentChebyshev <= 1)
        {
            score -= 1.50f;
            if (nextChebyshev > currentChebyshev)
            {
                score += 1.80f;
            }
            else if (nextChebyshev < currentChebyshev)
            {
                score -= 1.00f;
            }
        }
        else if (currentDistance < profile.PreferredRange)
        {
            if (nextDistance > currentDistance)
            {
                score += 1.00f;
            }
            else if (nextDistance < currentDistance)
            {
                score -= 0.80f;
            }
        }

        if (nextDistance == profile.PreferredRange)
        {
            score += 1.20f;
        }
        else if (Math.Abs(nextDistance - profile.PreferredRange) <= 1)
        {
            score += 0.30f;
        }

        if (currentDistance < profile.PreferredRange && HasLineOfSight(next, target.Position, world) && !HasLineOfSight(self.Position, target.Position, world))
        {
            score += 0.50f;
        }
        else if (currentDistance >= profile.PreferredRange && HasLineOfSight(self.Position, target.Position, world) && !HasLineOfSight(next, target.Position, world))
        {
            score -= 0.80f;
        }

        return score;
    }

    private static float ScoreMinRangeMove(IEntity self, IEntity target, Position next, AIProfile profile)
    {
        if (profile.MinRange <= 1)
        {
            return 0f;
        }

        var currentDistanceToTarget = self.Position.DistanceTo(target.Position);
        var nextDistanceToTarget = next.DistanceTo(target.Position);
        var score = 0f;

        if (currentDistanceToTarget <= profile.MinRange)
        {
            if (nextDistanceToTarget > currentDistanceToTarget)
            {
                score += 0.70f;
            }
            else if (nextDistanceToTarget < currentDistanceToTarget)
            {
                score -= 0.70f;
            }
        }
        else if (nextDistanceToTarget < profile.MinRange)
        {
            score -= 0.50f;
        }

        return score;
    }

    private static float ScoreSupportMove(IEntity self, IWorldState world, Position next, AIProfile profile)
    {
        var supportRange = profile.SupportRange > 0 ? profile.SupportRange : 4;
        var currentAllies = CountAlliesInRange(self, world, self.Position, supportRange);
        var nextAllies = CountAlliesInRange(self, world, next, supportRange);
        var delta = nextAllies - currentAllies;

        if (delta > 0)
        {
            return delta * 0.55f;
        }

        if (delta < 0)
        {
            return delta * 0.35f;
        }

        return 0f;
    }

    private static int CountAlliesInRange(IEntity self, IWorldState world, Position origin, int range)
    {
        var count = 0;
        foreach (var entity in world.Entities)
        {
            if (entity.Id == self.Id || !entity.IsAlive || entity.Faction != self.Faction || entity.Faction == Faction.Neutral)
            {
                continue;
            }

            if (origin.ChebyshevTo(entity.Position) <= range)
            {
                count++;
            }
        }

        return count;
    }

    private static float ScoreFleeMove(IEntity self, IEntity? target, Position next, AIProfile profile)
    {
        if (target is null)
        {
            return 0.10f;
        }

        var currentDistance = self.Position.DistanceTo(target.Position);
        var nextDistance = next.DistanceTo(target.Position);

        var score = profile.FleeWeight;
        if (nextDistance > currentDistance)
        {
            score += 1.10f + ((nextDistance - currentDistance) * 0.15f);
        }
        else if (nextDistance == currentDistance)
        {
            score -= 0.25f;
        }
        else
        {
            score -= 1.00f;
        }

        if (nextDistance <= 1)
        {
            score -= 0.50f;
        }

        return score;
    }

    private static float ScoreWait(IEntity self, IEntity? target, IWorldState world, AIStateComponent memory, AIProfile profile)
    {
        if (memory.State == AIState.Idle)
        {
            return profile.WaitWeight;
        }

        if (target is not null && profile.WaitWeight >= 0.80f)
        {
            var dist = self.Position.ChebyshevTo(target.Position);
            if (dist > profile.PreferredRange + 1)
            {
                return profile.WaitWeight + 1.50f;
            }
        }

        if (target is not null && profile.PreferredRange > 1)
        {
            var distance = self.Position.DistanceTo(target.Position);
            if (distance < profile.MinRange)
            {
                return 0.01f;
            }

            if (distance == profile.PreferredRange)
            {
                var score = 3.00f;
                if (HasAvailableRangedAbility(self, target, world))
                {
                    score += 0.75f;
                }

                return score;
            }

            if (Math.Abs(distance - profile.PreferredRange) <= 1)
            {
                return 0.85f;
            }
        }

        return memory.State == AIState.Patrol ? 0.05f : 0.01f;
    }

    private static bool HasAvailableRangedAbility(IEntity self, IEntity target, IWorldState world)
    {
        var abilities = self.GetComponent<AbilitiesComponent>();
        if (abilities is null || abilities.Slots.Count == 0)
        {
            return false;
        }

        var cooldowns = self.GetComponent<CooldownComponent>();
        var contentDb = (world as WorldState)?.ContentDatabase;
        if (contentDb is null)
        {
            return false;
        }

        foreach (var slot in abilities.Slots)
        {
            if (cooldowns is not null && cooldowns.IsOnCooldown(slot.AbilityId))
            {
                continue;
            }

            if (!contentDb.TryGetAbilityTemplate(slot.AbilityId, out var template))
            {
                continue;
            }

            if (template.Targeting.Type is "single" or "aoe_circle")
            {
                var range = self.Position.ChebyshevTo(target.Position);
                if (range <= template.Targeting.Range || template.Targeting.Range == 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static float ScoreCast(CastAbilityAction cast, IEntity self, IEntity? target, IWorldState world, AIStateComponent memory, AIProfile profile)
    {
        if (memory.State == AIState.Flee)
        {
            return 0.05f;
        }

        var abilities = self.GetComponent<AbilitiesComponent>();
        var slot = abilities?.Slots.Find(s => s.AbilityId == cast.Ability.AbilityId);
        var priority = slot?.Priority ?? 50;
        var score = priority / 50f;

        score += profile.AggressionWeight * 0.30f;

        if (target is not null && cast.Ability.Targeting.Type != "self")
        {
            var range = self.Position.ChebyshevTo(target.Position);
            var abilityRange = cast.Ability.Targeting.Range;
            if (range <= abilityRange && range > 1)
            {
                score += 1.80f;
            }
        }

        if (cast.Ability.Targeting.Type == "self" || string.Equals(cast.Ability.Targeting.Center, "self", StringComparison.OrdinalIgnoreCase))
        {
            // Only dedicated support profiles weigh ally proximity; every base profile
            // carries a nonzero SupportRange, so gating on it alone would penalize
            // self-buffs and self-heals for solo enemies.
            if (profile.Name == "support" && profile.SupportRange > 0 && self.Faction != Faction.Neutral)
            {
                var alliesInRange = 0;
                foreach (var entity in world.Entities)
                {
                    if (entity.Id == self.Id || !entity.IsAlive || entity.Faction != self.Faction)
                    {
                        continue;
                    }

                    if (self.Position.ChebyshevTo(entity.Position) <= profile.SupportRange)
                    {
                        alliesInRange++;
                    }
                }

                if (alliesInRange == 0)
                {
                    score -= 2.50f;
                }
                else
                {
                    score += 1.50f + (Math.Min(alliesInRange, 3) * 0.50f);
                }
            }
            else
            {
                score += 0.50f;
            }
        }

        if (cast.Ability.Targeting.Type == "aoe_circle")
        {
            score += 0.20f;
        }

        if (memory.State == AIState.Attack)
        {
            score += 0.25f;
        }

        return score;
    }

    private static int GetPathDistance(Position from, Position to, IWorldState world, IPathfinder pathfinder, bool phaseThroughWalls)
    {
        if (from == to)
        {
            return 0;
        }

        var path = pathfinder.FindPath(from, to, world, 64, phaseThroughWalls);
        return path.Count == 0 ? int.MaxValue : path.Count;
    }

    private static bool HasLineOfSight(Position from, Position to, IWorldState world)
    {
        var x0 = from.X;
        var y0 = from.Y;
        var x1 = to.X;
        var y1 = to.Y;

        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (x0 != x1 || y0 != y1)
        {
            var e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }

            var current = new Position(x0, y0);
            if (current == to)
            {
                return true;
            }

            if (world.BlocksSight(current))
            {
                return false;
            }
        }

        return true;
    }
}
