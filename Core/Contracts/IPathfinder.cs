using System.Collections.Generic;

namespace Roguelike.Core;

public interface IPathfinder
{
    IReadOnlyList<Position> FindPath(Position start, Position goal, IWorldState world, int maxLength = 50);

    bool HasPath(Position start, Position goal, IWorldState world, int maxLength = 50);

    IReadOnlyDictionary<Position, int> GetReachable(Position origin, int range, IWorldState world);
}
