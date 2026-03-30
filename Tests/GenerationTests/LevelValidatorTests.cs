using System.Collections.Generic;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.GenerationTests;

public sealed class LevelValidatorTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Generation.LevelValidator rejects disconnected walkable regions", RejectsDisconnectedWalkableRegions);
    }

    private static void RejectsDisconnectedWalkableRegions()
    {
        var world = new WorldState();
        world.InitGrid(8, 6);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }

        world.SetTile(new Position(1, 1), TileType.StairsUp);
        world.SetTile(new Position(2, 1), TileType.Floor);
        world.SetTile(new Position(6, 4), TileType.StairsDown);
        world.SetTile(new Position(5, 4), TileType.Floor);

        var level = new LevelData(
            new Position(1, 1),
            new Position(6, 4),
            new List<Position>(),
            new List<Position>(),
            new List<RoomData>
            {
                new(1, 1, 2, 2, new Position(1, 1)),
                new(5, 4, 2, 2, new Position(6, 4)),
                new(1, 3, 2, 2, new Position(1, 3)),
                new(5, 1, 2, 2, new Position(5, 1)),
            });

        var errors = LevelValidator.Validate(world, level);

        Expect.True(errors.Count > 0, "Disconnected level should fail validation");
    }
}