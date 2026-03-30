using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubGenerator : IGenerator
{
    public LevelData GenerateLevel(WorldState world, int seed, int depth)
    {
        world.InitGrid(10, 10);
        world.Depth = depth;
        world.Seed = seed;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.SetTile(new Position(1, 1), TileType.StairsUp);
        world.SetTile(new Position(8, 8), TileType.StairsDown);

        return new LevelData(
            new Position(1, 1),
            new Position(8, 8),
            new[] { new Position(5, 5) },
            new[] { new Position(4, 4) },
            new[] { new RoomData(0, 0, 10, 10, new Position(5, 5)) });
    }

    public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data) => System.Array.Empty<string>();
}
