using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public class AIBrain : IBrain
{
    private readonly AIProfile _profile;

    public AIBrain(AIProfile profile)
    {
        _profile = profile;
    }

    public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder)
    {
        var target = AcquireTarget(self, world, pathfinder);
        var memory = AIStateManager.UpdateState(self, target, _profile);
        var objective = GetObjective(self, world, pathfinder, target, memory);
        var candidates = GenerateCandidates(self, world, target);

        IAction bestAction = new WaitAction(self.Id);
        var bestScore = float.NegativeInfinity;

        foreach (var candidate in candidates)
        {
            if (candidate.Validate(world) != ActionResult.Success)
            {
                continue;
            }

            var score = UtilityScorer.ScoreAction(candidate, self, target, objective, world, memory, _profile, pathfinder);
            if (score > bestScore)
            {
                bestScore = score;
                bestAction = candidate;
            }
        }

        if (memory.State == AIState.Patrol && bestAction is MoveAction)
        {
            AIStateManager.RecordPatrolStep(self);
        }

        return bestAction.Validate(world) == ActionResult.Success ? bestAction : new WaitAction(self.Id);
    }

    private static List<IAction> GenerateCandidates(IEntity self, IWorldState world, IEntity? target)
    {
        var actions = new List<IAction>();

        if (target is not null && self.Position.ChebyshevTo(target.Position) <= 1)
        {
            actions.Add(new AttackAction(self.Id, target.Id));
        }

        foreach (var delta in Position.AllDirections)
        {
            var move = new MoveAction(self.Id, delta);
            if (move.Validate(world) == ActionResult.Success)
            {
                actions.Add(move);
            }
        }

        actions.Add(new WaitAction(self.Id));
        return actions;
    }

    private Position GetObjective(IEntity self, IWorldState world, IPathfinder pathfinder, IEntity? target, AIStateComponent memory)
    {
        if (memory.State == AIState.Patrol)
        {
            var patrolTarget = AIStateManager.GetPatrolTarget(self, world, pathfinder, _profile);
            if (patrolTarget != Position.Invalid)
            {
                return patrolTarget;
            }
        }

        if (target is not null)
        {
            return target.Position;
        }

        return memory.LastKnownTargetPosition;
    }

    private static IEntity? AcquireTarget(IEntity self, IWorldState world, IPathfinder pathfinder)
    {
        IEntity? bestTarget = null;
        var bestDistance = int.MaxValue;

        foreach (var entity in world.Entities)
        {
            if (entity.Id == self.Id || !entity.IsAlive || entity.Faction == self.Faction)
            {
                continue;
            }

            if (!CanSee(self, entity, world))
            {
                continue;
            }

            var path = pathfinder.FindPath(self.Position, entity.Position, world, 64);
            var distance = path.Count == 0 ? self.Position.DistanceTo(entity.Position) : path.Count;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = entity;
                continue;
            }

            if (distance == bestDistance && bestTarget is not null)
            {
                if (entity.Position.Y < bestTarget.Position.Y || (entity.Position.Y == bestTarget.Position.Y && entity.Position.X < bestTarget.Position.X))
                {
                    bestTarget = entity;
                }
            }
        }

        return bestTarget;
    }

    private static bool CanSee(IEntity self, IEntity target, IWorldState world)
    {
        if (self.Stats.ViewRadius <= 0 || self.Position.ChebyshevTo(target.Position) > self.Stats.ViewRadius)
        {
            return false;
        }

        return HasLineOfSight(self.Position, target.Position, world);
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