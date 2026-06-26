using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class Pathfinder : IPathfinder
{
    public IReadOnlyList<Position> FindPath(Position start, Position goal, IWorldState world, int maxLength = 50, bool phaseThroughWalls = false)
    {
        if (maxLength < 0 || !world.InBounds(start) || !world.InBounds(goal))
        {
            return Array.Empty<Position>();
        }

        if (start == goal)
        {
            return Array.Empty<Position>();
        }

        var frontier = new PriorityQueue<Position, int>();
        var cameFrom = new Dictionary<Position, Position>();
        var costs = new Dictionary<Position, int> { [start] = 0 };

        frontier.Enqueue(start, Heuristic(start, goal));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var neighbor in GetNeighbors(current, goal, world, phaseThroughWalls))
            {
                var nextCost = costs[current] + 1;
                if (nextCost > maxLength)
                {
                    continue;
                }

                if (costs.TryGetValue(neighbor, out var existingCost) && existingCost <= nextCost)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                costs[neighbor] = nextCost;
                frontier.Enqueue(neighbor, nextCost + Heuristic(neighbor, goal));
            }
        }

        return Array.Empty<Position>();
    }

    public bool HasPath(Position start, Position goal, IWorldState world, int maxLength = 50, bool phaseThroughWalls = false)
    {
        return start == goal || FindPath(start, goal, world, maxLength, phaseThroughWalls).Count > 0;
    }

    public IReadOnlyDictionary<Position, int> GetReachable(Position origin, int range, IWorldState world, bool phaseThroughWalls = false)
    {
        var reachable = new Dictionary<Position, int>();
        if (range < 0 || !world.InBounds(origin))
        {
            return reachable;
        }

        var frontier = new Queue<Position>();
        reachable[origin] = 0;
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            var currentDistance = reachable[current];
            if (currentDistance == range)
            {
                continue;
            }

            foreach (var neighbor in GetNeighbors(current, Position.Invalid, world, phaseThroughWalls))
            {
                if (reachable.ContainsKey(neighbor))
                {
                    continue;
                }

                reachable[neighbor] = currentDistance + 1;
                frontier.Enqueue(neighbor);
            }
        }

        return reachable;
    }

    private static IEnumerable<Position> GetNeighbors(Position current, Position goal, IWorldState world, bool phaseThroughWalls)
    {
        foreach (var delta in Position.AllDirections)
        {
            var next = current + delta;
            if (!IsTraversable(next, goal, world, phaseThroughWalls))
            {
                continue;
            }

            if (Math.Abs(delta.X) == 1 && Math.Abs(delta.Y) == 1)
            {
                var adjacentX = current.Offset(delta.X, 0);
                var adjacentY = current.Offset(0, delta.Y);
                if (!IsTraversable(adjacentX, goal, world, phaseThroughWalls) && !IsTraversable(adjacentY, goal, world, phaseThroughWalls))
                {
                    continue;
                }
            }

            yield return next;
        }
    }

    private static bool IsTraversable(Position pos, Position goal, IWorldState world, bool phaseThroughWalls)
    {
        if (!world.InBounds(pos))
        {
            return false;
        }

        if (phaseThroughWalls || world.IsWalkable(pos))
        {
            return true;
        }

        if (goal != Position.Invalid && pos == goal && world.GetEntityAt(pos) is not null)
        {
            return world.GetTile(pos) is TileType.Floor or TileType.StairsDown or TileType.StairsUp or TileType.Trap;
        }

        return false;
    }

    private static int Heuristic(Position from, Position to) => from.ChebyshevTo(to);

    private static IReadOnlyList<Position> ReconstructPath(IReadOnlyDictionary<Position, Position> cameFrom, Position current)
    {
        var path = new List<Position> { current };
        while (cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        path.RemoveAt(0);
        return path;
    }
}
