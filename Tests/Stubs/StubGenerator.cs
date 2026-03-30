using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubGenerator : IGenerator
{
    public LevelData GenerateLevel(WorldState world, int seed, int depth)
    {
        world.InitGrid(10, 10);

        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
        {
            bool isBorder = x == 0 || y == 0 || x == 9 || y == 9;
            world.SetTile(new Position(x, y), isBorder ? TileType.Wall : TileType.Floor);
        }

        world.SetTile(new Position(8, 8), TileType.StairsDown);

        return new LevelData(
            PlayerSpawn: new Position(5, 5),
            StairsDown: new Position(8, 8),
            EnemySpawns: new List<Position> { new(2, 2), new(6, 6) },
            ItemSpawns: new List<Position> { new(3, 3) },
            Rooms: new List<RoomData> { new(1, 1, 8, 8, new Position(4, 4)) }
        );
    }

    public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data) => [];
}
