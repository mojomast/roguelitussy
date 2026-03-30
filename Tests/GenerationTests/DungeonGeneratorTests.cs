using System.Linq;
using System.Text;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.GenerationTests;

public sealed class DungeonGeneratorTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Generation.DungeonGenerator same seed is deterministic", SameSeedProducesSameLevel);
        registry.Add("Generation.DungeonGenerator different seeds change layout", DifferentSeedsProduceDifferentLayouts);
        registry.Add("Generation.DungeonGenerator creates connected maps", GeneratedMapsAreConnected);
        registry.Add("Generation.DungeonGenerator applies spawn rules", GeneratorAppliesSpawnRules);
        registry.Add("Generation.BSPNode starter map produces at least four rooms", StarterMapProducesAtLeastFourRooms);
        registry.Add("Generation.CorridorBuilder creates an L-shaped corridor", CorridorBuilderCreatesConnectedCorridor);
    }

    private static void SameSeedProducesSameLevel()
    {
        var generator = new DungeonGenerator();
        var firstWorld = new WorldState();
        var secondWorld = new WorldState();

        var firstLevel = generator.GenerateLevel(firstWorld, 42, 1);
        var secondLevel = generator.GenerateLevel(secondWorld, 42, 1);

        Expect.Equal(GetWorldSignature(firstWorld), GetWorldSignature(secondWorld), "Same seed should produce the same tile layout");
        Expect.Equal(GetLevelSignature(firstLevel), GetLevelSignature(secondLevel), "Same seed should produce the same level metadata");
    }

    private static void DifferentSeedsProduceDifferentLayouts()
    {
        var generator = new DungeonGenerator();
        var firstWorld = new WorldState();
        var secondWorld = new WorldState();

        generator.GenerateLevel(firstWorld, 42, 1);
        generator.GenerateLevel(secondWorld, 99, 1);

        Expect.False(GetWorldSignature(firstWorld) == GetWorldSignature(secondWorld), "Different seeds should change the generated layout");
    }

    private static void GeneratedMapsAreConnected()
    {
        var generator = new DungeonGenerator();
        var world = new WorldState();
        var level = generator.GenerateLevel(world, 1234, 2);

        var validationErrors = generator.ValidateLevel(world, level);
        Expect.Equal(0, validationErrors.Count, "Generated level should validate cleanly");

        var reachable = LevelValidator.FloodFill(world, level.PlayerSpawn);
        Expect.Equal(CountTraversableTiles(world), reachable.Count, "Flood fill should reach every traversable tile");
        Expect.True(reachable.Contains(level.StairsDown), "Flood fill should reach stairs down");
        Expect.True(level.EnemySpawns.All(reachable.Contains), "Flood fill should reach every enemy spawn");
        Expect.True(level.ItemSpawns.All(reachable.Contains), "Flood fill should reach every item spawn");
    }

    private static void GeneratorAppliesSpawnRules()
    {
        var generator = new DungeonGenerator();
        var world = new WorldState();
        var level = generator.GenerateLevel(world, 777, 2);

        Expect.Equal(TileType.StairsUp, world.GetTile(level.PlayerSpawn), "Player spawn should be placed on stairs up");
        Expect.Equal(7, level.EnemySpawns.Count, "Enemy count should follow the depth formula");
        Expect.Equal(4, level.ItemSpawns.Count, "Item count should follow the depth formula");
        Expect.True(level.Rooms.Count >= 4, "Generated level should contain at least four rooms");

        var playerRoomIndex = FindRoomIndex(level.PlayerSpawn, level.Rooms);
        Expect.True(playerRoomIndex >= 0, "Player spawn should belong to a generated room");

        var firstEnemyRoomIndex = FindRoomIndex(level.EnemySpawns[0], level.Rooms);
        var secondEnemyRoomIndex = FindRoomIndex(level.EnemySpawns[1], level.Rooms);

        Expect.True(firstEnemyRoomIndex >= 0, "First enemy should belong to a generated room");
        Expect.True(secondEnemyRoomIndex >= 0, "Second enemy should belong to a generated room");
        Expect.False(firstEnemyRoomIndex == playerRoomIndex, "First enemy should not spawn in the player start room");
        Expect.False(secondEnemyRoomIndex == playerRoomIndex, "Second enemy should not spawn in the player start room");
    }

    private static void StarterMapProducesAtLeastFourRooms()
    {
        var root = BSPNode.Create(60, 40, new Random(42));
        Expect.True(root.Leaves().Count() >= 4, "Starter map BSP should produce at least four leaves");
    }

    private static void CorridorBuilderCreatesConnectedCorridor()
    {
        var world = new WorldState();
        world.InitGrid(12, 12);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }

        var from = new Position(2, 2);
        var to = new Position(8, 7);
        CorridorBuilder.Connect(world, from, to, new Random(0));

        var reachable = LevelValidator.FloodFill(world, from);
        Expect.True(reachable.Contains(to), "Corridor should connect both endpoints");
        Expect.Equal(from.DistanceTo(to) + 1, CountTraversableTiles(world), "L-shaped corridor should carve the expected number of tiles");
    }

    private static string GetWorldSignature(WorldState world)
    {
        var builder = new StringBuilder(world.Width * world.Height);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                builder.Append((char)('A' + (int)world.GetTile(new Position(x, y))));
            }

            builder.Append('|');
        }

        return builder.ToString();
    }

    private static string GetLevelSignature(LevelData level)
    {
        var builder = new StringBuilder();
        builder.Append(level.PlayerSpawn);
        builder.Append(';');
        builder.Append(level.StairsDown);
        builder.Append(';');

        for (var i = 0; i < level.EnemySpawns.Count; i++)
        {
            builder.Append(level.EnemySpawns[i]);
            builder.Append(';');
        }

        for (var i = 0; i < level.ItemSpawns.Count; i++)
        {
            builder.Append(level.ItemSpawns[i]);
            builder.Append(';');
        }

        for (var i = 0; i < level.Rooms.Count; i++)
        {
            builder.Append(level.Rooms[i].X);
            builder.Append(',');
            builder.Append(level.Rooms[i].Y);
            builder.Append(',');
            builder.Append(level.Rooms[i].Width);
            builder.Append(',');
            builder.Append(level.Rooms[i].Height);
            builder.Append(',');
            builder.Append(level.Rooms[i].Center);
            builder.Append(';');
        }

        return builder.ToString();
    }

    private static int CountTraversableTiles(WorldState world)
    {
        var count = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (LevelValidator.IsTraversable(world.GetTile(new Position(x, y))))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int FindRoomIndex(Position position, IReadOnlyList<RoomData> rooms)
    {
        for (var i = 0; i < rooms.Count; i++)
        {
            if (position.X >= rooms[i].X && position.X < rooms[i].X + rooms[i].Width && position.Y >= rooms[i].Y && position.Y < rooms[i].Y + rooms[i].Height)
            {
                return i;
            }
        }

        return -1;
    }
}