using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubPathfinder : IPathfinder
{
    public IReadOnlyList<Position> FindPath(
        Position start, Position goal, IWorldState world, int maxLength = 50)
    {
        var path = new List<Position> { start };
        var current = start;
        int steps = 0;

        while (current != goal && steps < maxLength)
        {
            int dx = goal.X.CompareTo(current.X);
            int dy = goal.Y.CompareTo(current.Y);
            current = new Position(current.X + dx, current.Y + dy);
            path.Add(current);
            steps++;
        }

        return path;
    }

    public bool HasPath(Position start, Position goal, IWorldState world, int maxLength = 50)
        => true;

    public IReadOnlyDictionary<Position, int> GetReachable(
        Position origin, int range, IWorldState world)
    {
        var result = new Dictionary<Position, int>();
        for (int dy = -range; dy <= range; dy++)
        for (int dx = -range; dx <= range; dx++)
        {
            var pos = origin.Offset(dx, dy);
            int dist = origin.DistanceTo(pos);
            if (dist <= range && world.InBounds(pos))
                result[pos] = dist;
        }
        return result;
    }
}
