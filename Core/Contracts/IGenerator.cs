using System.Collections.Generic;

namespace Roguelike.Core;

public interface IGenerator
{
    LevelData GenerateLevel(WorldState world, int seed, int depth);

    IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data);
}
