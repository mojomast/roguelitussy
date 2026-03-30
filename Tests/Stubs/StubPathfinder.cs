using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubPathfinder : IPathfinder
{
    public IReadOnlyList<Position> FindPath(Position start, Position goal, IWorldState world, int maxLength = 50)
    {
        var path = new List<Position> { start };
        var cursor = start;

        while (cursor != goal && path.Count <= maxLength)
        {
            var stepX = System.Math.Sign(goal.X - cursor.X);
            var stepY = System.Math.Sign(goal.Y - cursor.Y);
            cursor = cursor.Offset(stepX, stepY);
            path.Add(cursor);
        }

        return path;
    }

    public bool HasPath(Position start, Position goal, IWorldState world, int maxLength = 50) => true;

    public IReadOnlyDictionary<Position, int> GetReachable(Position origin, int range, IWorldState world)
    {
        var result = new Dictionary<Position, int> { [origin] = 0 };
        foreach (var direction in Position.Cardinals)
        {
            var next = origin + direction;
            if (world.InBounds(next) && world.IsWalkable(next))
            {
                result[next] = 1;
            }
        }

        return result;
    }
}
