using System;

namespace Roguelike.Core;

public static class AIStateManager
{
    public static AIStateComponent GetOrCreate(IEntity entity)
    {
        var component = entity.GetComponent<AIStateComponent>();
        if (component is not null)
        {
            return component;
        }

        component = new AIStateComponent();
        entity.SetComponent(component);
        return component;
    }

    public static AIStateComponent UpdateState(IEntity self, IEntity? target, AIProfile profile)
    {
        var memory = GetOrCreate(self);
        var previousState = memory.State;
        var nextState = DetermineState(self, target, previousState, profile, memory);

        if (target is not null)
        {
            memory.TargetId = target.Id;
            memory.LastKnownTargetPosition = target.Position;
        }

        switch (nextState)
        {
            case AIState.Attack:
            case AIState.Chase:
            case AIState.Flee:
                memory.IdleTurns = 0;
                if (nextState != AIState.Flee)
                {
                    memory.PatrolTarget = Position.Invalid;
                    memory.PatrolSteps = 0;
                }

                break;

            case AIState.Patrol:
                memory.IdleTurns = 0;
                if (previousState == AIState.Chase && memory.LastKnownTargetPosition != Position.Invalid && memory.LastKnownTargetPosition != self.Position)
                {
                    memory.PatrolTarget = memory.LastKnownTargetPosition;
                    memory.PatrolSteps = 0;
                }

                if (self.Position == memory.PatrolTarget)
                {
                    memory.PatrolTarget = Position.Invalid;
                    memory.PatrolSteps = 0;
                }

                break;

            default:
                memory.IdleTurns++;
                if (self.Position == memory.PatrolTarget)
                {
                    memory.PatrolTarget = Position.Invalid;
                    memory.PatrolSteps = 0;
                }

                break;
        }

        memory.State = nextState;
        return memory;
    }

    public static AIState DetermineState(IEntity self, IEntity? target, AIState current, AIProfile profile, AIStateComponent memory)
    {
        var lowHpThreshold = (int)Math.Ceiling(self.Stats.MaxHP * profile.FleeThreshold);
        var lowHp = self.Stats.MaxHP > 0 && self.Stats.HP <= lowHpThreshold;

        if (lowHp && target is not null && profile.CanFlee)
        {
            return AIState.Flee;
        }

        if (target is not null && self.Position.ChebyshevTo(target.Position) <= 1)
        {
            return AIState.Attack;
        }

        if (target is not null)
        {
            return AIState.Chase;
        }

        if (current == AIState.Chase && memory.LastKnownTargetPosition != Position.Invalid && memory.LastKnownTargetPosition != self.Position)
        {
            return AIState.Patrol;
        }

        if (current == AIState.Patrol && memory.PatrolTarget != Position.Invalid && memory.PatrolTarget != self.Position)
        {
            return AIState.Patrol;
        }

        if (profile.PatrolsWhenIdle && memory.IdleTurns >= profile.IdleTurnsBeforePatrol)
        {
            return AIState.Patrol;
        }

        return AIState.Idle;
    }

    public static Position GetPatrolTarget(IEntity self, IWorldState world, IPathfinder pathfinder, AIProfile profile)
    {
        var memory = GetOrCreate(self);
        if (memory.PatrolTarget != Position.Invalid
            && self.Position != memory.PatrolTarget
            && memory.PatrolSteps < profile.MaxPatrolSteps
            && pathfinder.HasPath(self.Position, memory.PatrolTarget, world, profile.PatrolRadius * 4))
        {
            return memory.PatrolTarget;
        }

        memory.PatrolSequence++;
        memory.PatrolSteps = 0;
        memory.PatrolTarget = SelectPatrolTarget(self, world, pathfinder, profile, memory.PatrolSequence);
        return memory.PatrolTarget;
    }

    public static void RecordPatrolStep(IEntity self)
    {
        var memory = GetOrCreate(self);
        memory.PatrolSteps++;
    }

    private static Position SelectPatrolTarget(IEntity self, IWorldState world, IPathfinder pathfinder, AIProfile profile, int patrolSequence)
    {
        var best = Position.Invalid;
        long bestScore = long.MinValue;

        // One BFS over reachable tiles replaces the per-candidate A* searches that made
        // retargeting cost hundreds of pathfinding calls.
        var reachable = pathfinder.GetReachable(self.Position, profile.PatrolRadius * 2, world, profile.PhaseThroughWalls);

        foreach (var entry in reachable)
        {
            var candidate = entry.Key;
            if (candidate == self.Position
                || Math.Abs(candidate.X - self.Position.X) > profile.PatrolRadius
                || Math.Abs(candidate.Y - self.Position.Y) > profile.PatrolRadius
                || !world.IsWalkable(candidate))
            {
                continue;
            }

            var score = BuildPatrolScore(self.Position, candidate, patrolSequence, world.TurnNumber);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static long BuildPatrolScore(Position origin, Position candidate, int patrolSequence, int turnNumber)
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + candidate.X;
            hash = (hash * 31) + candidate.Y;
            hash = (hash * 31) + patrolSequence;
            hash = (hash * 31) + turnNumber;

            return ((long)origin.DistanceTo(candidate) << 32) | (uint)hash;
        }
    }
}
