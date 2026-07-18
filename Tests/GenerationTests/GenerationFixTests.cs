using System.Linq;
using System.Reflection;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.GenerationTests;

public sealed class GenerationFixTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Generation.LevelValidator treats locked doors as traversable for connectivity", LockedRoomsPassValidation);
        registry.Add("Generation.LevelValidator excludes water from traversable tiles", WaterIsNotTraversable);
        registry.Add("Generation.FloorEventResolver boss floors win over safe floors", BossFloorsWinOverSafeFloors);
        registry.Add("Generation.Safe floors skip enemy spawns", SafeFloorsSkipEnemySpawns);
        registry.Add("Generation.Boss floors guarantee a boss spawn", BossFloorsGuaranteeBossSpawn);
        registry.Add("Generation.Deep floors keep scaling spawn counts", DeepFloorsKeepScalingSpawnCounts);
        registry.Add("Generation.Random enemy spawns always avoid the start room", EnemySpawnsAvoidStartRoom);
        registry.Add("Generation.One key is placed per locked room", OneKeyPerLockedRoom);
        registry.Add("Generation.Trap tiles receive theme-appropriate trap ids", TrapTilesReceiveThemedIds);
        registry.Add("Generation.Spawn tiles exclude doors, water, and traps", SpawnTilesExcludeDoorsWaterAndTraps);
        registry.Add("Generation.Default library supports the prison theme", DefaultLibrarySupportsPrisonTheme);
        registry.Add("Generation.Ragged prefab layouts read as walls instead of throwing", RaggedPrefabRowsAreSafe);
        registry.Add("Generation.Boss floor generation is deterministic", BossFloorGenerationIsDeterministic);
        registry.Add("Generation.Content prefabs generate cleanly at lock-room depths", ContentDepthsGenerateCleanly);
    }

    private static void LockedRoomsPassValidation()
    {
        var world = new WorldState();
        world.InitGrid(10, 5);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }

        // Left room, locked door, sealed right room.
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

        world.SetTile(new Position(4, 2), TileType.LockedDoor);
        world.SetTile(new Position(1, 1), TileType.StairsUp);
        world.SetTile(new Position(3, 3), TileType.StairsDown);

        var level = new LevelData(
            new Position(1, 1),
            new Position(3, 3),
            new List<Position>(),
            new List<Position>(),
            new List<RoomData>
            {
                new(1, 1, 3, 3, new Position(2, 2)),
                new(5, 1, 3, 3, new Position(6, 2)),
                new(1, 4, 1, 1, new Position(1, 4)),
                new(3, 4, 1, 1, new Position(3, 4)),
            },
            LockedDoors: new List<Position> { new(4, 2) },
            KeySpawns: new List<Position> { new(2, 2) });

        var errors = LevelValidator.Validate(world, level);
        Expect.Equal(0, errors.Count, "A room sealed behind a locked door should not fail connectivity validation.");

        Expect.False(LevelValidator.IsTraversable(TileType.LockedDoor), "Locked doors should stay non-traversable by default.");
        Expect.True(LevelValidator.IsTraversable(TileType.LockedDoor, includeLockedDoors: true), "Locked doors should be traversable for connectivity validation.");
    }

    private static void WaterIsNotTraversable()
    {
        Expect.False(LevelValidator.IsTraversable(TileType.Water), "Water is not walkable in the simulation, so it should not count for connectivity.");
        Expect.True(LevelValidator.IsTraversable(TileType.Trap), "Trap tiles remain traversable.");
        Expect.True(LevelValidator.IsTraversable(TileType.Door), "Doors remain traversable.");
    }

    private static void BossFloorsWinOverSafeFloors()
    {
        Expect.Equal(FloorType.BossFloor, FloorEventResolver.ResolveFloorType(15, 42), "Depth 15 should be a boss floor, not a safe floor.");
        Expect.Equal(FloorType.BossFloor, FloorEventResolver.ResolveFloorType(30, 42), "Depth 30 should be a boss floor, not a safe floor.");
        Expect.Equal(FloorType.SafeFloor, FloorEventResolver.ResolveFloorType(5, 42), "Depth 5 should stay a safe floor.");
        Expect.Equal(FloorType.BossFloor, FloorEventResolver.ResolveFloorType(3, 42), "Depth 3 should be a boss floor.");
        Expect.Equal(FloorType.StandardFloor, FloorEventResolver.ResolveFloorType(4, 42), "Depth 4 should be a standard floor.");
        Expect.True(FloorEventResolver.ShouldSkipEnemySpawns(5, 42), "Safe floors should skip enemy spawns.");
        Expect.False(FloorEventResolver.ShouldSkipEnemySpawns(15, 42), "Boss floors should keep their enemy spawns.");
        Expect.True(FloorEventResolver.ResolveFloorEvents(15, 42).HasSpecialRoom(SpecialRoomType.BossRoom), "Boss floor plans should request a boss room.");
    }

    private static void SafeFloorsSkipEnemySpawns()
    {
        var generator = new DungeonGenerator();
        var world = new WorldState();
        var level = generator.GenerateLevel(world, 42, 5);

        Expect.Equal(0, level.EnemySpawns.Count, "Safe floors should have no enemy spawn positions.");
        Expect.Equal(0, level.EnemySpawnDetails?.Count ?? 0, "Safe floors should have no enemy spawn details.");
        Expect.True(level.ItemSpawns.Count > 0, "Safe floors should still place items.");
    }

    private static void BossFloorsGuaranteeBossSpawn()
    {
        var generator = new DungeonGenerator();
        var world = new WorldState();
        var level = generator.GenerateLevel(world, 42, 15);

        var details = level.EnemySpawnDetails ?? System.Array.Empty<EnemySpawnData>();
        Expect.True(details.Any(spawn => spawn.IsBoss), "Boss floors should always include a boss spawn, even without a boss prefab.");
    }

    private static void DeepFloorsKeepScalingSpawnCounts()
    {
        var generator = new DungeonGenerator();
        var world = new WorldState();
        var level = generator.GenerateLevel(world, 42, 11);

        // Depth 11 on a 100x60 map: 3 + 11*2 = 25 enemies and 2 + 11 = 13 items,
        // both above the old hard caps of 20 and 10.
        Expect.Equal(25, level.EnemySpawns.Count, "Deep floors should scale enemy counts past the old hard cap.");
        Expect.Equal(13, level.ItemSpawns.Count, "Deep floors should scale item counts past the old hard cap.");
    }

    private static void EnemySpawnsAvoidStartRoom()
    {
        var generator = new DungeonGenerator();
        foreach (var seed in new[] { 11, 222, 3333 })
        {
            var world = new WorldState();
            var level = generator.GenerateLevel(world, seed, 4);
            var playerRoomIndex = FindRoomIndex(level.PlayerSpawn, level.Rooms);
            Expect.True(playerRoomIndex >= 0, "Player spawn should belong to a generated room.");

            for (var i = 0; i < level.EnemySpawns.Count; i++)
            {
                Expect.False(
                    FindRoomIndex(level.EnemySpawns[i], level.Rooms) == playerRoomIndex,
                    $"Enemy spawn {level.EnemySpawns[i]} (seed {seed}) should not be inside the start room.");
            }
        }
    }

    private static void OneKeyPerLockedRoom()
    {
        var method = typeof(DungeonGenerator).GetMethod("PlaceLockedDoorsAndKeys", BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
        {
            throw new InvalidOperationException("Could not resolve PlaceLockedDoorsAndKeys method.");
        }

        var world = new WorldState();
        world.InitGrid(14, 5);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }

        // Open room, then two locked rooms in a row, each behind its own door.
        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }

            for (var x = 5; x <= 7; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }

            for (var x = 9; x <= 11; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.SetTile(new Position(4, 2), TileType.Door);
        world.SetTile(new Position(8, 2), TileType.Door);

        var lockedPrefab = new RoomPrefab("locked_vault", new[] { "###", "#.#", "###" }, LockDoorsOnEnter: true);
        var openRoom = BuildRoom(1, 1);
        var lockedRoomA = BuildRoom(5, 1, lockedPrefab);
        var lockedRoomB = BuildRoom(9, 1, lockedPrefab);

        var rooms = new List<RoomPlacement> { openRoom, lockedRoomA, lockedRoomB };
        var occupied = new HashSet<Position>();
        var result = method.Invoke(null, new object[] { world, rooms, new Position(2, 2), new Random(9), occupied });
        var (lockedDoors, keySpawns) = ((List<Position>, List<Position>))result!;

        Expect.Equal(2, lockedDoors.Count, "Both doors guarding lockable rooms should be locked.");
        Expect.Equal(2, keySpawns.Count, "One key should be placed for each locked room.");
        Expect.True(keySpawns.All(openRoom.Contains), "Keys should only be placed in reachable non-locked rooms.");
        Expect.True(keySpawns.Distinct().Count() == keySpawns.Count, "Key spawns should not overlap.");
    }

    private static void TrapTilesReceiveThemedIds()
    {
        var method = typeof(DungeonGenerator).GetMethod("CollectTrapSpawnDetails", BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
        {
            throw new InvalidOperationException("Could not resolve CollectTrapSpawnDetails method.");
        }

        var prefab = new RoomPrefab(
            "trap_room",
            new[]
            {
                "#####",
                "#^.^#",
                "#####",
            });
        var origin = new Position(10, 10);
        var walkableTiles = prefab.GetWalkableOffsets().Select(offset => origin + offset).ToArray();
        var room = new RoomPlacement(new RoomData(origin.X, origin.Y, prefab.Width, prefab.Height, new Position(12, 11)), walkableTiles, origin, prefab);

        var themes = new Dictionary<string, string[]>
        {
            ["prison"] = new[] { "spike_trap", "trap_alarm" },
            ["crypt"] = new[] { "trap_poison_gas", "trap_teleport" },
            ["magma"] = new[] { "spike_trap", "trap_gold_drain" },
        };

        foreach (var (theme, allowedIds) in themes)
        {
            var spawns = (List<TrapSpawnData>)method.Invoke(null, new object?[] { new[] { room }, new HashSet<Position>(), theme, 1234 })!;
            Expect.Equal(2, spawns.Count, $"Both '{theme}' trap tiles should be collected.");
            foreach (var spawn in spawns)
            {
                Expect.True(spawn.TrapId is not null, $"Trap tiles should never produce a null trap id ({theme}).");
                Expect.True(allowedIds.Contains(spawn.TrapId!), $"Trap id '{spawn.TrapId}' should match the {theme} theme table.");
            }

            var repeat = (List<TrapSpawnData>)method.Invoke(null, new object?[] { new[] { room }, new HashSet<Position>(), theme, 1234 })!;
            for (var i = 0; i < spawns.Count; i++)
            {
                Expect.Equal(spawns[i].TrapId!, repeat[i].TrapId!, "Trap id selection should be deterministic per seed and position.");
            }
        }
    }

    private static void SpawnTilesExcludeDoorsWaterAndTraps()
    {
        var prefab = new RoomPrefab(
            "hazard_room",
            new[]
            {
                "#####",
                "#~.~#",
                "+...+",
                "#~^~#",
                "#####",
            });
        var origin = new Position(10, 10);
        var walkableTiles = prefab.GetWalkableOffsets().Select(offset => origin + offset).ToArray();
        var room = new RoomPlacement(new RoomData(origin.X, origin.Y, prefab.Width, prefab.Height, new Position(12, 12)), walkableTiles, origin, prefab);

        for (var seed = 0; seed < 20; seed++)
        {
            var tile = room.GetRandomWalkableTile(new Random(seed), new HashSet<Position>());
            var local = new Position(tile.X - origin.X, tile.Y - origin.Y);
            Expect.Equal(TileType.Floor, prefab.GetTileType(local.X, local.Y), "Spawn tiles should only ever be plain floor.");
        }
    }

    private static void DefaultLibrarySupportsPrisonTheme()
    {
        var prisonPrefabs = RoomPrefabLibrary.GetDefaultPrefabs().Count(prefab => prefab.Tags.Contains("prison"));
        Expect.True(prisonPrefabs >= 4, "The default library needs at least four prison prefabs to satisfy the theme threshold.");
    }

    private static void RaggedPrefabRowsAreSafe()
    {
        var prefab = new RoomPrefab(
            "ragged",
            new[]
            {
                "#####",
                "#.#",
                "#####",
            });

        Expect.Equal(TileType.Wall, prefab.GetTileType(3, 1), "Cells beyond a short row should read as wall.");
        Expect.Equal(TileType.Wall, prefab.GetTileType(4, 1), "Cells beyond a short row should read as wall.");
        Expect.Equal(TileType.Floor, prefab.GetTileType(1, 1), "In-bounds cells should still resolve normally.");
        Expect.False(prefab.IsDoor(4, 1), "IsDoor should not throw on ragged rows.");
        Expect.True(prefab.GetWalkableOffsets().Count == 1, "Walkable offsets should only include real cells.");
    }

    private static void BossFloorGenerationIsDeterministic()
    {
        var generator = new DungeonGenerator();
        var firstWorld = new WorldState();
        var secondWorld = new WorldState();

        var first = generator.GenerateLevel(firstWorld, 777, 15);
        var second = generator.GenerateLevel(secondWorld, 777, 15);

        Expect.Equal(Signature(first), Signature(second), "Boss floor generation should be deterministic for the same seed.");
    }

    private static void ContentDepthsGenerateCleanly()
    {
        var content = ContentLoader.LoadFromRepository(throwOnValidationErrors: false);
        Expect.True(content.IsValid, "Content should load without validation errors for depth sweep.");

        var generator = new DungeonGenerator();
        foreach (var seed in new[] { 7, 21 })
        {
            for (var depth = 4; depth <= 9; depth++)
            {
                var world = new WorldState { ContentDatabase = content };
                var level = generator.GenerateLevel(world, seed, depth);
                Expect.Equal(0, generator.ValidateLevel(world, level).Count, $"Depth {depth} seed {seed} should validate cleanly.");

                var floorType = FloorEventResolver.ResolveFloorType(depth, seed);
                var details = level.EnemySpawnDetails ?? System.Array.Empty<EnemySpawnData>();
                if (floorType == FloorType.BossFloor)
                {
                    Expect.True(details.Any(spawn => spawn.IsBoss), $"Boss floor {depth} (seed {seed}) should include a boss spawn.");
                }
                else if (floorType == FloorType.SafeFloor)
                {
                    Expect.Equal(0, details.Count, $"Safe floor {depth} (seed {seed}) should have no enemies.");
                }

                if ((level.LockedDoors?.Count ?? 0) > 0)
                {
                    Expect.True((level.KeySpawns?.Count ?? 0) > 0, $"Floor {depth} (seed {seed}) with locked doors should place at least one key.");
                }

                foreach (var trap in level.TrapSpawnDetails ?? System.Array.Empty<TrapSpawnData>())
                {
                    Expect.True(!string.IsNullOrWhiteSpace(trap.TrapId), $"Floor {depth} (seed {seed}) trap at {trap.Position} should carry a trap id.");
                }
            }
        }
    }

    private static RoomPlacement BuildRoom(int x, int y, RoomPrefab? prefab = null)
    {
        var tiles = new List<Position>();
        for (var dy = 0; dy < 3; dy++)
        {
            for (var dx = 0; dx < 3; dx++)
            {
                tiles.Add(new Position(x + dx, y + dy));
            }
        }

        return new RoomPlacement(new RoomData(x, y, 3, 3, new Position(x + 1, y + 1)), tiles, new Position(x, y), prefab);
    }

    private static string Signature(LevelData level)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(level.PlayerSpawn).Append('|').Append(level.StairsDown).Append('|');
        foreach (var spawn in level.EnemySpawnDetails ?? System.Array.Empty<EnemySpawnData>())
        {
            builder.Append(spawn.Position).Append(spawn.IsBoss).Append(';');
        }

        foreach (var spawn in level.ItemSpawnDetails ?? System.Array.Empty<ItemSpawnData>())
        {
            builder.Append(spawn.Position).Append(';');
        }

        foreach (var trap in level.TrapSpawnDetails ?? System.Array.Empty<TrapSpawnData>())
        {
            builder.Append(trap.Position).Append(trap.TrapId).Append(';');
        }

        foreach (var key in level.KeySpawns ?? System.Array.Empty<Position>())
        {
            builder.Append(key).Append(';');
        }

        return builder.ToString();
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
