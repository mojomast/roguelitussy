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
        registry.Add("Generation.RoomPlacement prefers doorway closest to target", RoomPlacementPrefersDoorwayClosestToTarget);
        registry.Add("Generation.CorridorBuilder preserves door endpoints", CorridorBuilderPreservesDoorEndpoints);
        registry.Add("Generation.DoorSanitizer removes orphaned door tiles", DoorSanitizerRemovesOrphanedDoorTiles);
        registry.Add("Generation.RoomPlacement surfaces prefab-authored metadata", RoomPlacementSurfacesPrefabAuthoredMetadata);
        registry.Add("Generation.RoomPlacer skips prefabs with no walkable tiles", RoomPlacerSkipsPrefabsWithNoWalkableTiles);
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

    private static void RoomPlacementPrefersDoorwayClosestToTarget()
    {
        var prefab = new RoomPrefab(
            "door_test",
            new[]
            {
                "##+##",
                "#...#",
                "+...+",
                "#...#",
                "##+##",
            });
        var origin = new Position(10, 10);
        var walkableTiles = prefab.GetWalkableOffsets().Select(offset => origin + offset).ToArray();
        var room = new RoomPlacement(new RoomData(origin.X, origin.Y, prefab.Width, prefab.Height, new Position(12, 12)), walkableTiles, origin, prefab);

        Expect.Equal(new Position(12, 10), room.GetConnectionPointTowards(new Position(12, 0)), "Northward connections should prefer the north door.");
        Expect.Equal(new Position(14, 12), room.GetConnectionPointTowards(new Position(20, 12)), "Eastward connections should prefer the east door.");
    }

    private static void CorridorBuilderPreservesDoorEndpoints()
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
        world.SetTile(from, TileType.Door);
        world.SetTile(to, TileType.Door);

        CorridorBuilder.Connect(world, from, to, new Random(0));

        Expect.Equal(TileType.Door, world.GetTile(from), "Connecting corridors should not overwrite the starting door tile.");
        Expect.Equal(TileType.Door, world.GetTile(to), "Connecting corridors should not overwrite the destination door tile.");
    }

    private static void DoorSanitizerRemovesOrphanedDoorTiles()
    {
        var world = new WorldState();
        world.InitGrid(8, 8);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }

        var orphanDoor = new Position(2, 2);
        world.SetTile(orphanDoor, TileType.Door);
        world.SetTile(new Position(2, 1), TileType.Floor);

        var validDoor = new Position(5, 3);
        world.SetTile(validDoor, TileType.Door);
        world.SetTile(new Position(5, 2), TileType.Floor);
        world.SetTile(new Position(5, 4), TileType.Floor);

        DoorSanitizer.Normalize(world);

        Expect.Equal(TileType.Wall, world.GetTile(orphanDoor), "Doors that only open onto one side should be removed instead of leaving dead-end arches.");
        Expect.Equal(TileType.Door, world.GetTile(validDoor), "Doors that connect two passages should remain intact.");
    }

    private static void RoomPlacementSurfacesPrefabAuthoredMetadata()
    {
        var prefab = new RoomPrefab(
            "metadata_test",
            new[]
            {
                "#####",
                "#.I.#",
                "#.C.#",
                "#...#",
                "##+##",
            },
            new[]
            {
                new RoomPrefabSpawnPoint(2, 1, "item"),
                new RoomPrefabSpawnPoint(2, 2, "chest", "deep_floor_loot"),
                new RoomPrefabSpawnPoint(2, 3, "enemy_boss"),
            },
            new[]
            {
                new RoomPrefabFixedEntity(1, 3, "npc", "field_chronicler"),
            },
            ItemQualityBonus: 2,
            EnemyCountBonus: 1,
            LockDoorsOnEnter: true);

        var origin = new Position(20, 30);
        var walkableTiles = prefab.GetWalkableOffsets().Select(offset => origin + offset).ToArray();
        var room = new RoomPlacement(new RoomData(origin.X, origin.Y, prefab.Width, prefab.Height, new Position(22, 32)), walkableTiles, origin, prefab);

        var itemSpawn = room.GetSpawnPlacements("item").Single();
        var chestSpawn = room.GetSpawnPlacements("chest").Single();
        var fixedNpc = room.GetFixedEntityPlacements().Single();

        Expect.Equal(new Position(22, 31), itemSpawn.Position, "Spawn points should resolve to absolute world positions.");
        Expect.Equal(new Position(22, 32), chestSpawn.Position, "Chest spawn points should resolve to absolute world positions.");
        Expect.Equal("deep_floor_loot", chestSpawn.SpawnPoint.ReferenceId ?? string.Empty, "Chest spawn metadata should preserve authored references.");
        Expect.Equal(new Position(21, 33), fixedNpc.Position, "Fixed entity positions should resolve to absolute world positions.");
        Expect.Equal("field_chronicler", fixedNpc.FixedEntity.TemplateId ?? string.Empty, "Fixed entity metadata should preserve authored templates.");
        Expect.Equal(2, room.Prefab?.ItemQualityBonus ?? -1, "Room prefabs should preserve authored item quality bonuses.");
        Expect.Equal(1, room.Prefab?.EnemyCountBonus ?? -1, "Room prefabs should preserve authored enemy count bonuses.");
        Expect.True(room.Prefab?.LockDoorsOnEnter == true, "Room prefabs should preserve authored room flags for downstream runtime hooks.");
    }

    private static void RoomPlacerSkipsPrefabsWithNoWalkableTiles()
    {
        var root = BSPNode.Create(60, 40, new Random(17));
        var world = new WorldState();
        world.InitGrid(60, 40);

        var invalidPrefab = new RoomPrefab(
            "all_walls",
            new[]
            {
                "########",
                "########",
                "########",
                "########",
            });
        var validPrefab = new RoomPrefab(
            "valid_room",
            new[]
            {
                "#####",
                "#...#",
                "#...#",
                "#####",
            });

        var placements = RoomPlacer.PlaceRooms(root, world, new Random(5), new[] { invalidPrefab, validPrefab });

        Expect.True(placements.Count >= 4, "Room placement should still succeed when invalid prefabs are present.");
        Expect.True(placements.All(room => room.WalkableTiles.Count > 0), "Placed rooms should always expose at least one walkable tile.");
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

        foreach (var spawn in level.EnemySpawnDetails ?? System.Array.Empty<EnemySpawnData>())
        {
            builder.Append(spawn.Position);
            builder.Append(',');
            builder.Append(spawn.TemplateId);
            builder.Append(',');
            builder.Append(spawn.IsBoss);
            builder.Append(';');
        }

        foreach (var spawn in level.ItemSpawnDetails ?? System.Array.Empty<ItemSpawnData>())
        {
            builder.Append(spawn.Position);
            builder.Append(',');
            builder.Append(spawn.TemplateId);
            builder.Append(',');
            builder.Append(spawn.QualityBonus);
            builder.Append(';');
        }

        foreach (var spawn in level.ChestSpawnDetails ?? System.Array.Empty<ChestSpawnData>())
        {
            builder.Append(spawn.Position);
            builder.Append(',');
            builder.Append(spawn.LootTableId);
            builder.Append(';');
        }

        foreach (var spawn in level.NpcSpawns ?? System.Array.Empty<NpcSpawnData>())
        {
            builder.Append(spawn.Position);
            builder.Append(',');
            builder.Append(spawn.TemplateId);
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