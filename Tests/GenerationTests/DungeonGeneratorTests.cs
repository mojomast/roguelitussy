using Xunit;
using Roguelike.Core;
using Roguelike.Core.Generation;
using Roguelike.Core.AI;

namespace Roguelike.Tests;

public class DungeonGeneratorTests
{
    [Fact]
    public void GenerateLevel_Deterministic_SameSeedSameResult()
    {
        var world1 = new WorldState();
        var world2 = new WorldState();
        var gen = new DungeonGenerator();

        var level1 = gen.GenerateLevel(world1, seed: 42, depth: 1);
        var level2 = gen.GenerateLevel(world2, seed: 42, depth: 1);

        Assert.Equal(level1.PlayerSpawn, level2.PlayerSpawn);
        Assert.Equal(level1.StairsDown, level2.StairsDown);
        Assert.Equal(level1.Rooms.Count, level2.Rooms.Count);

        var grid1 = world1.GetRawGrid();
        var grid2 = world2.GetRawGrid();
        Assert.Equal(grid1, grid2);
    }

    [Fact]
    public void GenerateLevel_DifferentSeeds_ProduceDifferentMaps()
    {
        var world1 = new WorldState();
        var world2 = new WorldState();
        var gen = new DungeonGenerator();

        gen.GenerateLevel(world1, seed: 1, depth: 0);
        gen.GenerateLevel(world2, seed: 999, depth: 0);

        var grid1 = world1.GetRawGrid();
        var grid2 = world2.GetRawGrid();

        bool anyDifference = false;
        for (int i = 0; i < grid1.Length; i++)
        {
            if (grid1[i] != grid2[i]) { anyDifference = true; break; }
        }
        Assert.True(anyDifference);
    }

    [Fact]
    public void GenerateLevel_HasMultipleRooms()
    {
        var world = new WorldState();
        var gen = new DungeonGenerator();

        var level = gen.GenerateLevel(world, seed: 123, depth: 0);

        Assert.True(level.Rooms.Count >= 2, "Expected at least 2 rooms");
    }

    [Fact]
    public void GenerateLevel_PlayerSpawnIsWalkable()
    {
        var world = new WorldState();
        var gen = new DungeonGenerator();

        var level = gen.GenerateLevel(world, seed: 7, depth: 0);

        var tile = world.GetTile(level.PlayerSpawn);
        Assert.Equal(TileType.Floor, tile);
    }

    [Fact]
    public void GenerateLevel_StairsDownExists()
    {
        var world = new WorldState();
        var gen = new DungeonGenerator();

        var level = gen.GenerateLevel(world, seed: 7, depth: 0);

        Assert.Equal(TileType.StairsDown, world.GetTile(level.StairsDown));
    }

    [Fact]
    public void GenerateLevel_PlayerCanReachStairs()
    {
        var world = new WorldState();
        var gen = new DungeonGenerator();

        var level = gen.GenerateLevel(world, seed: 42, depth: 0);

        var pathfinder = new Pathfinder();
        Assert.True(pathfinder.HasPath(level.PlayerSpawn, level.StairsDown, world, maxLength: 500));
    }
}
