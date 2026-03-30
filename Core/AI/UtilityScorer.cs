using System;

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
            WaitAction => ScoreWait(self, target, memory, profile),
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

        var currentDistance = GetPathDistance(self.Position, objective, world, pathfinder);
        var nextDistance = GetPathDistance(next, objective, world, pathfinder);

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

        if (target is not null && profile.PreferredRange > 1)
        {
            var currentRangeDelta = Math.Abs(self.Position.DistanceTo(target.Position) - profile.PreferredRange);
            var nextRangeDelta = Math.Abs(next.DistanceTo(target.Position) - profile.PreferredRange);
            if (nextRangeDelta < currentRangeDelta)
            {
                score += 0.40f;
            }
            else if (nextRangeDelta > currentRangeDelta)
            {
                score -= 0.20f;
            }
        }

        return score;
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

    private static float ScoreWait(IEntity self, IEntity? target, AIStateComponent memory, AIProfile profile)
    {
        if (memory.State == AIState.Idle)
        {
            return profile.WaitWeight;
        }

        if (target is not null && profile.PreferredRange > 1)
        {
            var distance = self.Position.DistanceTo(target.Position);
            if (Math.Abs(distance - profile.PreferredRange) <= 1)
            {
                return 0.35f;
            }
        }

        return memory.State == AIState.Patrol ? 0.05f : 0.01f;
    }

    private static int GetPathDistance(Position from, Position to, IWorldState world, IPathfinder pathfinder)
    {
        if (from == to)
        {
            return 0;
        }

        var path = pathfinder.FindPath(from, to, world, 64);
        return path.Count == 0 ? int.MaxValue : path.Count;
    }
}