using System;
using System.Collections.Generic;

namespace Roguelike.Core.AI;

public sealed class Pathfinder : IPathfinder
{
    public IReadOnlyList<Position> FindPath(Position start, Position goal, IWorldState world, int maxLength = 50)
    {
        if (start == goal)
            return Array.Empty<Position>();

        var open = new PriorityQueue<Position, int>();
        var cameFrom = new Dictionary<Position, Position>();
        var gScore = new Dictionary<Position, int> { [start] = 0 };

        open.Enqueue(start, Heuristic(start, goal));

        while (open.Count > 0)
        {
            var current = open.Dequeue();

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            int currentG = gScore[current];
            if (currentG >= maxLength)
                continue;

            foreach (var dir in Position.Cardinals)
            {
                var neighbor = current + dir;

                if (!world.InBounds(neighbor))
                    continue;

                // Goal is always considered walkable (path TO an occupied tile)
                if (neighbor != goal && !world.IsWalkable(neighbor))
                    continue;

                int tentativeG = currentG + 1;

                if (gScore.TryGetValue(neighbor, out int existingG) && tentativeG >= existingG)
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                open.Enqueue(neighbor, tentativeG + Heuristic(neighbor, goal));
            }
        }

        return Array.Empty<Position>();
    }

    public bool HasPath(Position start, Position goal, IWorldState world, int maxLength = 50)
    {
        return FindPath(start, goal, world, maxLength).Count > 0;
    }

    public IReadOnlyDictionary<Position, int> GetReachable(Position origin, int range, IWorldState world)
    {
        var result = new Dictionary<Position, int> { [origin] = 0 };
        var frontier = new Queue<Position>();
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            int currentDist = result[current];

            if (currentDist >= range)
                continue;

            foreach (var dir in Position.Cardinals)
            {
                var neighbor = current + dir;

                if (!world.InBounds(neighbor) || !world.IsWalkable(neighbor))
                    continue;

                if (result.ContainsKey(neighbor))
                    continue;

                result[neighbor] = currentDist + 1;
                frontier.Enqueue(neighbor);
            }
        }

        return result;
    }

    private static int Heuristic(Position a, Position b) => a.DistanceTo(b);

    private static IReadOnlyList<Position> ReconstructPath(Dictionary<Position, Position> cameFrom, Position current)
    {
        var path = new List<Position> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        // Remove the start position — return only the steps to take
        path.RemoveAt(0);
        return path;
    }
}
