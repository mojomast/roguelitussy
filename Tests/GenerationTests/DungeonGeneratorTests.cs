using System.Linq;
using System.Reflection;
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
        registry.Add("Generation.RoomPrefab maps '^' to trap tile", RoomPrefabMapsTrapTile);
        registry.Add("Generation.CollectTrapSpawnDetails extracts tile and spawn-point traps", CollectTrapSpawnDetailsExtractsTraps);
        registry.Add("Generation.LevelValidator confirms trap positions are reachable", LevelValidatorConfirmsTrapReachability);
        registry.Add("Generation.Trap positions do not overlap enemy or item spawns", TrapPositionsDoNotOverlapSpawns);
        registry.Add("Generation.Floor theme constrains prefab selection", FloorThemeConstrainsPrefabSelection);
        registry.Add("Generation.Fallback when theme has few matching prefabs", FallbackWhenThemeHasFewMatchingPrefabs);
        registry.Add("Generation.Locked doors block rooms and keys are reachable", LockedDoorsBlockRoomsAndKeysAreReachable);
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

    private static void RoomPrefabMapsTrapTile()
    {
        var prefab = new RoomPrefab(
            "trap_room",
            new[]
            {
                "#####",
                "#.^.#",
                "#####",
            });

        Expect.Equal(TileType.Trap, prefab.GetTileType(2, 1), "'^' should map to TileType.Trap.");
        Expect.Equal(TileType.Floor, prefab.GetTileType(1, 1), "'.' should still map to TileType.Floor.");
        Expect.Equal(TileType.Wall, prefab.GetTileType(0, 0), "'#' should still map to TileType.Wall.");
    }

    private static void CollectTrapSpawnDetailsExtractsTraps()
    {
        var prefab = new RoomPrefab(
            "trap_room",
            new[]
            {
                "#####",
                "#.^.#",
                "#...#",
                "#####",
            },
            new[]
            {
                new RoomPrefabSpawnPoint(1, 2, "trap", "spike_trap"),
            });

        var origin = new Position(10, 10);
        var walkableTiles = prefab.GetWalkableOffsets().Select(offset => origin + offset).ToArray();
        var room = new RoomPlacement(new RoomData(origin.X, origin.Y, prefab.Width, prefab.Height, new Position(12, 11)), walkableTiles, origin, prefab);
        var occupied = new HashSet<Position>();

        var method = typeof(DungeonGenerator).GetMethod("CollectTrapSpawnDetails", BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
        {
            throw new InvalidOperationException("CollectTrapSpawnDetails method not found.");
        }

        var result = (List<TrapSpawnData>)method.Invoke(null, new object[] { new[] { room }, occupied })!;

        Expect.Equal(2, result.Count, "Should collect both trap tiles and trap spawn points.");
        Expect.True(result.Any(spawn => spawn.Position == new Position(12, 11) && spawn.TrapId is null), "Trap tile position should be collected.");
        Expect.True(result.Any(spawn => spawn.Position == new Position(11, 12) && spawn.TrapId == "spike_trap"), "Trap spawn point with trap_id should be collected.");
        Expect.Equal(2, occupied.Count, "Trap positions should be added to the occupied set.");
    }

    private static void LevelValidatorConfirmsTrapReachability()
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
        world.SetTile(new Position(2, 1), TileType.Trap);
        world.SetTile(new Position(3, 1), TileType.StairsDown);

        var level = new LevelData(
            new Position(1, 1),
            new Position(3, 1),
            new List<Position>(),
            new List<Position>(),
            new List<RoomData>
            {
                new(1, 1, 3, 1, new Position(2, 1)),
                new(1, 2, 1, 1, new Position(1, 2)),
                new(3, 2, 1, 1, new Position(3, 2)),
                new(1, 3, 3, 1, new Position(2, 3)),
            },
            TrapSpawnDetails: new List<TrapSpawnData>
            {
                new(new Position(2, 1), "spike_trap"),
            });

        var errors = LevelValidator.Validate(world, level);
        Expect.Equal(0, errors.Count, "Reachable trap position should not cause validation errors.");
        Expect.True(LevelValidator.IsTraversable(TileType.Trap), "Trap tiles should be traversable.");
    }

    private static void TrapPositionsDoNotOverlapSpawns()
    {
        var prefab = new RoomPrefab(
            "trap_spawn_overlap",
            new[]
            {
                "#####",
                "#^..#",
                "#####",
            },
            new[]
            {
                new RoomPrefabSpawnPoint(1, 1, "enemy"),
                new RoomPrefabSpawnPoint(3, 1, "item"),
            });

        var origin = new Position(20, 30);
        var walkableTiles = prefab.GetWalkableOffsets().Select(offset => origin + offset).ToArray();
        var room = new RoomPlacement(new RoomData(origin.X, origin.Y, prefab.Width, prefab.Height, new Position(22, 31)), walkableTiles, origin, prefab);
        var occupied = new HashSet<Position>();

        var trapMethod = typeof(DungeonGenerator).GetMethod("CollectTrapSpawnDetails", BindingFlags.NonPublic | BindingFlags.Static);
        var enemyMethod = typeof(DungeonGenerator).GetMethod("CollectEnemySpawnDetails", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IReadOnlyList<RoomPlacement>), typeof(HashSet<Position>), typeof(IReadOnlyList<EnemySpawnData>), typeof(string[]) }, null);
        var itemMethod = typeof(DungeonGenerator).GetMethod("CollectItemSpawnDetails", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(IReadOnlyList<RoomPlacement>), typeof(HashSet<Position>), typeof(IReadOnlyList<ItemSpawnData>), typeof(string[]) }, null);

        if (trapMethod is null || enemyMethod is null || itemMethod is null)
        {
            throw new InvalidOperationException("Could not resolve generator collection methods.");
        }

        var traps = (List<TrapSpawnData>)trapMethod.Invoke(null, new object[] { new[] { room }, occupied })!;
        var enemies = (List<EnemySpawnData>)enemyMethod.Invoke(null, new object[] { new[] { room }, occupied, Array.Empty<EnemySpawnData>(), new[] { "enemy", "enemy_boss" } })!;
        var items = (List<ItemSpawnData>)itemMethod.Invoke(null, new object[] { new[] { room }, occupied, Array.Empty<ItemSpawnData>(), new[] { "item" } })!;

        Expect.Equal(1, traps.Count, "Trap tile should be collected.");
        Expect.Equal(new Position(21, 31), traps[0].Position, "Trap should be at the '^' tile position.");
        Expect.Equal(0, enemies.Count, "Enemy spawn on the same tile as a trap should be skipped.");
        Expect.Equal(1, items.Count, "Item spawn on a different tile should still be collected.");
        Expect.Equal(new Position(23, 31), items[0].Position, "Item spawn should be on the free tile.");
    }

    private static void FloorThemeConstrainsPrefabSelection()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        Expect.True(content.IsValid, "Content should load without validation errors for theme test");

        var generator = new DungeonGenerator();
        const int seed = 12345;

        var depth1World = new WorldState { ContentDatabase = content };
        var depth1Level = generator.GenerateLevel(depth1World, seed, 1);
        var depth1Match = CountThemeMatches(depth1Level.Rooms, "prison");
        Expect.True(depth1Match.TotalPrefabRooms > 0, "Depth 1 should place at least one prefab room");
        Expect.True(depth1Match.MatchingCount * 2 >= depth1Match.TotalPrefabRooms, "Depth 1 should use mostly prison-themed prefabs");

        var depth4World = new WorldState { ContentDatabase = content };
        var depth4Level = generator.GenerateLevel(depth4World, seed, 4);
        var depth4Match = CountThemeMatches(depth4Level.Rooms, "crypt");
        Expect.True(depth4Match.TotalPrefabRooms > 0, "Depth 4 should place at least one prefab room");
        Expect.True(depth4Match.MatchingCount * 2 >= depth4Match.TotalPrefabRooms, "Depth 4 should use mostly crypt-themed prefabs");

        var depth7World = new WorldState { ContentDatabase = content };
        var depth7Level = generator.GenerateLevel(depth7World, seed, 7);
        var depth7Match = CountThemeMatches(depth7Level.Rooms, "magma");
        Expect.True(depth7Match.TotalPrefabRooms > 0, "Depth 7 should place at least one prefab room");
        Expect.True(depth7Match.MatchingCount * 2 >= depth7Match.TotalPrefabRooms, "Depth 7 should use mostly magma-themed prefabs");

        var depth1World2 = new WorldState { ContentDatabase = content };
        var depth1Level2 = generator.GenerateLevel(depth1World2, seed, 1);
        Expect.Equal(GetLevelSignature(depth1Level), GetLevelSignature(depth1Level2), "Same seed and depth should produce the same level signature");
    }

    private static (int MatchingCount, int TotalPrefabRooms) CountThemeMatches(IReadOnlyList<RoomData> rooms, string themeTag)
    {
        var matching = 0;
        var total = 0;
        for (var i = 0; i < rooms.Count; i++)
        {
            var tags = rooms[i].Tags;
            if (tags is null || tags.Count == 0)
            {
                continue;
            }

            total++;
            if (tags.Contains(themeTag))
            {
                matching++;
            }
        }

        return (matching, total);
    }

    private static void FallbackWhenThemeHasFewMatchingPrefabs()
    {
        var root = BSPNode.Create(60, 40, new Random(42));
        var world = new WorldState();
        world.InitGrid(60, 40);

        var themePrefab = new RoomPrefab(
            "large_prison",
            new[]
            {
                "###############",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "#.............#",
                "###############",
            },
            DefinedTags: new[] { "prison" });

        var smallPrefabA = new RoomPrefab(
            "small_a",
            new[]
            {
                "#####",
                "#...#",
                "#...#",
                "#...#",
                "#####",
            },
            DefinedTags: new[] { "generic" });

        var smallPrefabB = new RoomPrefab(
            "small_b",
            new[]
            {
                "#######",
                "#.....#",
                "#.....#",
                "#.....#",
                "#.....#",
                "#######",
            },
            DefinedTags: new[] { "generic" });

        var smallPrefabC = new RoomPrefab(
            "small_c",
            new[]
            {
                "#########",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#.......#",
                "#########",
            },
            DefinedTags: new[] { "generic" });

        var prefabs = new[] { themePrefab, smallPrefabA, smallPrefabB, smallPrefabC };
        var rooms = RoomPlacer.PlaceRooms(root, world, new Random(7), prefabs, "prison");
        CorridorBuilder.Stitch(root, world, new Random(7));
        DoorSanitizer.Normalize(world);

        Expect.True(rooms.Count >= 4, "Fallback should still place at least four rooms");
        var reachable = LevelValidator.FloodFill(world, rooms[0].Room.Center);
        for (var i = 1; i < rooms.Count; i++)
        {
            Expect.True(reachable.Contains(rooms[i].Room.Center), $"Room {i} should be reachable from room 0 via corridors");
        }
    }

    private static void LockedDoorsBlockRoomsAndKeysAreReachable()
    {
        var method = typeof(DungeonGenerator).GetMethod("PlaceLockedDoorsAndKeys", BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
        {
            throw new InvalidOperationException("Could not resolve PlaceLockedDoorsAndKeys method.");
        }

        var world = new WorldState();
        world.InitGrid(10, 5);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }

        // Two 3x3 rooms at left and right, connected by a door at (4, 2).
        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        for (var y = 1; y <= 3; y++)
        {
            for (var x = 5; x <= 7; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.SetTile(new Position(4, 2), TileType.Door);

        var leftRoom = new RoomPlacement(
            new RoomData(1, 1, 3, 3, new Position(2, 2)),
            new List<Position> { new(1, 1), new(1, 2), new(1, 3), new(2, 1), new(2, 2), new(2, 3), new(3, 1), new(3, 2), new(3, 3) },
            new Position(1, 1),
            null);

        var rightRoom = new RoomPlacement(
            new RoomData(5, 1, 3, 3, new Position(6, 2)),
            new List<Position> { new(5, 1), new(5, 2), new(5, 3), new(6, 1), new(6, 2), new(6, 3), new(7, 1), new(7, 2), new(7, 3) },
            new Position(5, 1),
            new RoomPrefab("locked_vault", new[] { "###", "#.#", "###" }, LockDoorsOnEnter: true));

        var rooms = new List<RoomPlacement> { leftRoom, rightRoom };
        var occupied = new HashSet<Position>();
        var playerSpawn = new Position(2, 2);
        var rng = new Random(123);

        var result = method.Invoke(null, new object[] { world, rooms, playerSpawn, rng, occupied });
        var (lockedDoors, keySpawns) = ((List<Position>, List<Position>))result!;

        Expect.True(lockedDoors.Count > 0, "Lockable room should have its connecting door converted to a locked door.");
        Expect.True(lockedDoors.Contains(new Position(4, 2)), "The connecting door should be locked.");
        Expect.Equal(TileType.LockedDoor, world.GetTile(new Position(4, 2)), "World tile should be LockedDoor.");
        Expect.True(keySpawns.Count > 0, "A key should be placed for the locked room.");
        Expect.True(leftRoom.Contains(keySpawns[0]), "Key should be placed in a reachable non-locked room.");

        var reachable = LevelValidator.FloodFill(world, playerSpawn);
        Expect.True(reachable.Contains(keySpawns[0]), "Key spawn should be reachable from the player start before unlocking.");
        Expect.False(reachable.Contains(new Position(6, 2)), "Locked room should not be reachable before unlocking.");
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
            builder.Append(',');
            var tags = level.Rooms[i].Tags;
            if (tags is not null)
            {
                for (var j = 0; j < tags.Count; j++)
                {
                    builder.Append(tags[j]);
                    builder.Append(':');
                }
            }

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

        foreach (var spawn in level.TrapSpawnDetails ?? System.Array.Empty<TrapSpawnData>())
        {
            builder.Append(spawn.Position);
            builder.Append(',');
            builder.Append(spawn.TrapId);
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
