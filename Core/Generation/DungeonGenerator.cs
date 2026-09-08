using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed class DungeonGenerator : IGenerator
{
    private const int MaxGenerationAttempts = 10;
    private const int MaxEnemies = 20;
    private const int MaxItems = 10;
    private const int MinThemePrefabMatches = 4;
    private const int EnemyDensityDivisor = 140;
    private const int ItemDensityDivisor = 400;

    private static readonly string[] PrisonTrapIds = { "spike_trap", "trap_alarm" };
    private static readonly string[] CryptTrapIds = { "trap_poison_gas", "trap_teleport" };
    private static readonly string[] MagmaTrapIds = { "spike_trap", "trap_gold_drain" };
    private static readonly string[] DefaultTrapIds = { "spike_trap", "trap_alarm" };
    private static readonly string[] FloorProfileTags = { "combat", "loot", "hazard", "open", "ambush" };

    private sealed record FixedEntityCollections(
        List<EnemySpawnData> EnemySpawns,
        List<ItemSpawnData> ItemSpawns,
        List<ChestSpawnData> ChestSpawns,
        List<NpcSpawnData> NpcSpawns);

    public LevelData GenerateLevel(WorldState world, int seed, int depth)
    {
        ArgumentNullException.ThrowIfNull(world);

        var prefabs = ResolvePrefabs(world, depth);
        var floorPlan = FloorEventResolver.ResolveFloorEvents(depth, seed);
        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            LevelData? level;
            try
            {
                level = TryGenerateLevel(world, seed, depth, attempt, prefabs, floorPlan);
            }
            catch (InvalidOperationException)
            {
                // This attempt produced an unusable layout (e.g. no free spawn tiles);
                // retry with the next mixed attempt seed instead of crashing generation.
                continue;
            }

            if (level is not null)
            {
                return level;
            }
        }

        throw new InvalidOperationException("Failed to generate a connected dungeon level within the retry limit.");
    }

    private static LevelData? TryGenerateLevel(
        WorldState world,
        int seed,
        int depth,
        int attempt,
        IReadOnlyList<RoomPrefab> prefabs,
        FloorEventPlan floorPlan)
    {
        var attemptSeed = MixSeed(seed, depth, attempt);
        var layoutRng = new Random(MixStageSeed(attemptSeed, 0x1f123bb5));
        var placementRng = new Random(MixStageSeed(attemptSeed, 0x5f356495));
        var mapSize = GetMapSize(depth);
        var mapArea = mapSize.Width * mapSize.Height;
        var skipEnemySpawns = floorPlan.FloorType == FloorType.SafeFloor;
        var specialRoomTags = new List<string>(floorPlan.SpecialRooms.Count);
        foreach (var request in floorPlan.SpecialRooms)
        {
            specialRoomTags.Add(request.PrefabTag);
        }

        InitializeWorld(world, mapSize.Width, mapSize.Height, seed, depth);

        var tree = BSPNode.Create(mapSize.Width, mapSize.Height, layoutRng);
        var themeTag = ResolveThemeForDepth(depth);
        var useTheme = !string.IsNullOrWhiteSpace(themeTag) && HasEnoughThemeMatches(prefabs, themeTag, tree);
        var profileTag = ResolveFloorProfile(attemptSeed, prefabs, tree, specialRoomTags);
        var rooms = RoomPlacer.PlaceRooms(tree, world, layoutRng, prefabs, useTheme ? themeTag : null, specialRoomTags, profileTag);
        CorridorBuilder.Stitch(tree, world, layoutRng);
        DoorSanitizer.Normalize(world);

        if (rooms.Count < 4)
        {
            return null;
        }

        var startRoom = rooms[0];
        var exitRoom = SelectExitRoom(world, startRoom, rooms);
        if (ReferenceEquals(startRoom, exitRoom))
        {
            return null;
        }

        var occupied = new HashSet<Position>();
        var playerSpawn = TryGetExplicitSpawn(startRoom, occupied, out var explicitPlayerSpawn, "player")
            ? explicitPlayerSpawn
            : PlaceStairs(world, startRoom, TileType.StairsUp, placementRng, occupied);
        world.SetTile(playerSpawn, TileType.StairsUp);

        var stairsDown = TryGetExplicitSpawn(exitRoom, occupied, out var explicitExitSpawn, "stairs_down")
            ? explicitExitSpawn
            : PlaceStairs(world, exitRoom, TileType.StairsDown, placementRng, occupied);
        world.SetTile(stairsDown, TileType.StairsDown);

        var fixedEntities = CollectFixedEntitySpawns(world.ContentDatabase, rooms, occupied);
        var chestSpawnDetails = CollectChestSpawnDetails(rooms, occupied, fixedEntities.ChestSpawns);
        var forcedEnemySpawns = skipEnemySpawns
            ? new List<EnemySpawnData>()
            : CollectEnemySpawnDetails(rooms, occupied, fixedEntities.EnemySpawns, "enemy", "enemy_boss");
        var forcedItemSpawns = CollectItemSpawnDetails(rooms, occupied, fixedEntities.ItemSpawns, "item");
        var shrineSpawns = CollectShrineSpawnDetails(rooms, floorPlan, occupied);
        var landmarkSpawns = CreateLandmarkDetails(
            rooms,
            startRoom,
            floorPlan,
            shrineSpawns,
            chestSpawnDetails,
            placementRng,
            occupied);
        var trapSpawnDetails = CollectTrapSpawnDetails(rooms, occupied, themeTag, attemptSeed);
        var (lockedDoors, keySpawns) = PlaceLockedDoorsAndKeys(world, rooms, playerSpawn, placementRng, occupied);
        if (floorPlan.FloorType == FloorType.SafeFloor)
        {
            AddSafeFloorRecoveryCache(rooms, startRoom, placementRng, occupied, forcedItemSpawns, chestSpawnDetails);
        }

        var enemySpawnDetails = skipEnemySpawns
            ? new List<EnemySpawnData>()
            : PlaceEnemySpawns(rooms, startRoom, depth, mapArea, placementRng, occupied, forcedEnemySpawns);
        var itemSpawnDetails = PlaceItemSpawns(rooms, depth, mapArea, placementRng, occupied, forcedItemSpawns);

        if (floorPlan.HasSpecialRoom(SpecialRoomType.BossRoom) && !enemySpawnDetails.Any(spawn => spawn.IsBoss))
        {
            // Boss floors always get a boss guarding the exit even when no boss prefab fits.
            var bossPosition = PickAvailableTile(rooms, exitRoom, placementRng, occupied);
            occupied.Add(bossPosition);
            enemySpawnDetails.Add(new EnemySpawnData(bossPosition, IsBoss: true));
        }

        var roomData = new List<RoomData>(rooms.Count);
        for (var i = 0; i < rooms.Count; i++)
        {
            roomData.Add(rooms[i].Room);
        }

        var level = new LevelData(
            playerSpawn,
            stairsDown,
            enemySpawnDetails.Select(spawn => spawn.Position).ToArray(),
            itemSpawnDetails.Select(spawn => spawn.Position).ToArray(),
            roomData,
            chestSpawnDetails.Select(spawn => spawn.Position).ToArray(),
            enemySpawnDetails,
            itemSpawnDetails,
            chestSpawnDetails,
            fixedEntities.NpcSpawns,
            trapSpawnDetails,
            lockedDoors,
            keySpawns,
            floorPlan.FloorType,
            shrineSpawns,
            landmarkSpawns);
        return LevelValidator.Validate(world, level).Count == 0 ? level : null;
    }

    public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data) => LevelValidator.Validate(world, data);

    private static void InitializeWorld(WorldState world, int width, int height, int seed, int depth)
    {
        world.InitGrid(width, height);
        world.Seed = seed;
        world.Depth = depth;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Wall);
            }
        }
    }

    private static (int Width, int Height) GetMapSize(int depth)
    {
        var floor = depth <= 0 ? 1 : depth;
        if (floor <= 3)
        {
            return (60, 40);
        }

        if (floor <= 6)
        {
            return (80, 50);
        }

        return (100, 60);
    }

    private static string? ResolveThemeForDepth(int depth)
    {
        var floor = depth <= 0 ? 1 : depth;
        return floor switch
        {
            <= 3 => "prison",
            <= 6 => "crypt",
            _ => "magma",
        };
    }

    private static bool HasEnoughThemeMatches(IReadOnlyList<RoomPrefab> prefabs, string themeTag, BSPNode tree)
    {
        var leaves = tree.Leaves().ToList();
        var usableCount = 0;

        for (var i = 0; i < prefabs.Count; i++)
        {
            if (!prefabs[i].Tags.Contains(themeTag) || !prefabs[i].HasWalkableTiles)
            {
                continue;
            }

            for (var j = 0; j < leaves.Count; j++)
            {
                var leaf = leaves[j];
                if (prefabs[i].FitsWithin(leaf.Width - (RoomPlacer.LeafPadding * 2), leaf.Height - (RoomPlacer.LeafPadding * 2)))
                {
                    usableCount++;
                    break;
                }
            }
        }

        return usableCount >= MinThemePrefabMatches;
    }

    private static int MixSeed(int seed, int depth, int attempt)
    {
        unchecked
        {
            var floor = depth <= 0 ? 1 : depth;
            return seed ^ (floor * 7919) ^ (attempt * 104729);
        }
    }

    private static int MixStageSeed(int seed, int salt)
    {
        unchecked
        {
            var mixed = seed ^ salt;
            mixed ^= mixed << 13;
            mixed ^= (int)((uint)mixed >> 17);
            mixed ^= mixed << 5;
            return mixed;
        }
    }

    private static string? ResolveFloorProfile(
        int seed,
        IReadOnlyList<RoomPrefab> prefabs,
        BSPNode tree,
        IReadOnlyList<string> specialRoomTags)
    {
        var leaves = tree.Leaves().Skip(1).ToArray();
        if (leaves.Length == 0)
        {
            return null;
        }

        var startIndex = (int)((uint)seed % (uint)FloorProfileTags.Length);
        for (var offset = 0; offset < FloorProfileTags.Length; offset++)
        {
            var tag = FloorProfileTags[(startIndex + offset) % FloorProfileTags.Length];
            if (specialRoomTags.Contains(tag))
            {
                continue;
            }

            for (var prefabIndex = 0; prefabIndex < prefabs.Count; prefabIndex++)
            {
                var prefab = prefabs[prefabIndex];
                if (!prefab.HasWalkableTiles || !prefab.Tags.Contains(tag))
                {
                    continue;
                }

                for (var leafIndex = 0; leafIndex < leaves.Length; leafIndex++)
                {
                    if (prefab.FitsWithin(
                        leaves[leafIndex].Width - (RoomPlacer.LeafPadding * 2),
                        leaves[leafIndex].Height - (RoomPlacer.LeafPadding * 2)))
                    {
                        return tag;
                    }
                }
            }
        }

        return null;
    }

    private static RoomPlacement SelectExitRoom(WorldState world, RoomPlacement startRoom, IReadOnlyList<RoomPlacement> rooms)
    {
        // A placed boss room always hosts the exit so the boss guards the stairs.
        for (var i = 0; i < rooms.Count; i++)
        {
            if (!ReferenceEquals(rooms[i], startRoom) && rooms[i].Prefab?.Tags.Contains("boss") == true)
            {
                return rooms[i];
            }
        }

        return SelectFarthestRoom(world, startRoom, rooms);
    }

    private static RoomPlacement SelectFarthestRoom(WorldState world, RoomPlacement startRoom, IReadOnlyList<RoomPlacement> rooms)
    {
        // Rank rooms by actual walk distance through the carved corridors rather than
        // straight-line distance, so the exit lands at the far end of the traversal.
        var distances = ComputeTraversalDistances(world, startRoom);
        var farthestRoom = startRoom;
        var farthestDistance = -1;

        for (var i = 0; i < rooms.Count; i++)
        {
            if (ReferenceEquals(rooms[i], startRoom))
            {
                continue;
            }

            var distance = GetRoomTraversalDistance(rooms[i], distances);
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthestRoom = rooms[i];
            }
        }

        if (!ReferenceEquals(farthestRoom, startRoom))
        {
            return farthestRoom;
        }

        // Fallback (disconnected layout that validation will reject anyway): Euclidean ranking.
        for (var i = 0; i < rooms.Count; i++)
        {
            if (ReferenceEquals(rooms[i], startRoom))
            {
                continue;
            }

            var distance = startRoom.Room.Center.DistanceTo(rooms[i].Room.Center);
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthestRoom = rooms[i];
            }
        }

        return farthestRoom;
    }

    private static Dictionary<Position, int> ComputeTraversalDistances(WorldState world, RoomPlacement startRoom)
    {
        var distances = new Dictionary<Position, int>();
        var queue = new Queue<Position>();

        foreach (var tile in startRoom.WalkableTiles)
        {
            if (world.InBounds(tile) && LevelValidator.IsTraversable(world.GetTile(tile)) && distances.TryAdd(tile, 0))
            {
                queue.Enqueue(tile);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var nextDistance = distances[current] + 1;
            foreach (var delta in Position.Cardinals)
            {
                var neighbor = current + delta;
                if (!world.InBounds(neighbor)
                    || distances.ContainsKey(neighbor)
                    || !LevelValidator.IsTraversable(world.GetTile(neighbor)))
                {
                    continue;
                }

                distances[neighbor] = nextDistance;
                queue.Enqueue(neighbor);
            }
        }

        return distances;
    }

    private static int GetRoomTraversalDistance(RoomPlacement room, Dictionary<Position, int> distances)
    {
        var best = -1;
        for (var i = 0; i < room.WalkableTiles.Count; i++)
        {
            if (distances.TryGetValue(room.WalkableTiles[i], out var distance) && (best < 0 || distance < best))
            {
                best = distance;
            }
        }

        return best;
    }

    private static Position PlaceStairs(
        WorldState world,
        RoomPlacement room,
        TileType stairsTile,
        Random rng,
        HashSet<Position> occupied)
    {
        var position = room.GetRandomWalkableTile(rng, occupied);
        occupied.Add(position);
        world.SetTile(position, stairsTile);
        return position;
    }

    private static List<EnemySpawnData> PlaceEnemySpawns(
        IReadOnlyList<RoomPlacement> rooms,
        RoomPlacement startRoom,
        int depth,
        int mapArea,
        Random rng,
        HashSet<Position> occupied,
        IReadOnlyList<EnemySpawnData>? forcedSpawns = null)
    {
        var floor = Math.Max(depth, 0);
        var bonusEnemies = rooms.Sum(room => Math.Max(0, room.Prefab?.EnemyCountBonus ?? 0));
        // Cap scales with the map footprint so density holds up on the larger deep floors.
        var enemyCap = Math.Max(MaxEnemies, mapArea / EnemyDensityDivisor);
        var enemyCount = Math.Min(enemyCap, 3 + (floor * 2) + bonusEnemies);
        var spawns = new List<EnemySpawnData>(enemyCount);

        if (forcedSpawns is not null)
        {
            spawns.AddRange(forcedSpawns);
        }

        // Random enemies never spawn in the start room, including the overflow fallback.
        var candidateRooms = new List<RoomPlacement>(rooms.Count);
        for (var i = 0; i < rooms.Count; i++)
        {
            if (!ReferenceEquals(rooms[i], startRoom))
            {
                candidateRooms.Add(rooms[i]);
            }
        }

        if (candidateRooms.Count == 0)
        {
            return spawns;
        }

        for (var i = spawns.Count; i < enemyCount; i++)
        {
            var room = candidateRooms[rng.Next(candidateRooms.Count)];
            var position = PickAvailableTile(candidateRooms, room, rng, occupied);
            occupied.Add(position);
            spawns.Add(new EnemySpawnData(position));
        }

        return spawns;
    }

    private static List<ItemSpawnData> PlaceItemSpawns(
        IReadOnlyList<RoomPlacement> rooms,
        int depth,
        int mapArea,
        Random rng,
        HashSet<Position> occupied,
        IReadOnlyList<ItemSpawnData>? forcedSpawns = null)
    {
        var floor = Math.Max(depth, 0);
        var itemCap = Math.Max(MaxItems, mapArea / ItemDensityDivisor);
        var itemCount = Math.Min(itemCap, 2 + floor);
        var spawns = new List<ItemSpawnData>(itemCount);

        if (forcedSpawns is not null)
        {
            spawns.AddRange(forcedSpawns);
        }

        for (var i = spawns.Count; i < itemCount; i++)
        {
            var room = rooms[rng.Next(rooms.Count)];
            var position = PickAvailableTile(rooms, room, rng, occupied);
            occupied.Add(position);
            spawns.Add(new ItemSpawnData(position, QualityBonus: Math.Max(0, room.Prefab?.ItemQualityBonus ?? 0)));
        }

        return spawns;
    }

    private static void AddSafeFloorRecoveryCache(
        IReadOnlyList<RoomPlacement> rooms,
        RoomPlacement startRoom,
        Random rng,
        HashSet<Position> occupied,
        ICollection<ItemSpawnData> itemSpawns,
        ICollection<ChestSpawnData> chestSpawns)
    {
        // Reserve recovery in the start room before generic item placement, falling back only
        // when its authored geometry has no two free floor tiles.
        var potionPosition = PickAvailableTile(rooms, startRoom, rng, occupied);
        occupied.Add(potionPosition);
        itemSpawns.Add(new ItemSpawnData(potionPosition, "potion_health"));

        var chestPosition = PickAvailableTile(rooms, startRoom, rng, occupied);
        occupied.Add(chestPosition);
        chestSpawns.Add(new ChestSpawnData(chestPosition, "safe_floor_merchant_stock"));
    }

    private static Position PickAvailableTile(
        IReadOnlyList<RoomPlacement> rooms,
        RoomPlacement preferredRoom,
        Random rng,
        HashSet<Position> occupied)
    {
        try
        {
            return preferredRoom.GetRandomWalkableTile(rng, occupied);
        }
        catch (InvalidOperationException)
        {
            var startIndex = rng.Next(rooms.Count);
            for (var offset = 0; offset < rooms.Count; offset++)
            {
                var room = rooms[(startIndex + offset) % rooms.Count];
                try
                {
                    return room.GetRandomWalkableTile(rng, occupied);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        throw new InvalidOperationException("No unoccupied walkable tiles remain for spawn placement.");
    }

    private static (List<Position> LockedDoors, List<Position> KeySpawns) PlaceLockedDoorsAndKeys(
        WorldState world,
        IReadOnlyList<RoomPlacement> rooms,
        Position playerSpawn,
        Random rng,
        HashSet<Position> occupied)
    {
        var lockedDoors = new List<Position>();
        var lockableRooms = new HashSet<RoomPlacement>();

        foreach (var room in rooms)
        {
            if (room.Prefab is { LockDoorsOnEnter: true } || room.Room.Tags?.Contains("locked") == true)
            {
                lockableRooms.Add(room);
            }
        }

        if (lockableRooms.Count == 0)
        {
            return (lockedDoors, new List<Position>());
        }

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.GetTile(position) != TileType.Door)
                {
                    continue;
                }

                var adjacentRooms = new HashSet<RoomPlacement>();
                foreach (var delta in Position.AllDirections)
                {
                    var neighbor = position + delta;
                    if (!world.InBounds(neighbor) || !world.IsWalkable(neighbor))
                    {
                        continue;
                    }

                    foreach (var room in rooms)
                    {
                        if (room.Contains(neighbor))
                        {
                            adjacentRooms.Add(room);
                            break;
                        }
                    }
                }

                var lockingRooms = adjacentRooms.Where(room => lockableRooms.Contains(room)).ToList();
                if (lockingRooms.Count > 0)
                {
                    world.SetTile(position, TileType.LockedDoor);
                    lockedDoors.Add(position);
                }
            }
        }

        var reachable = ComputeReachablePositions(world, playerSpawn);
        var keyCandidates = new List<Position>();
        foreach (var room in rooms)
        {
            if (lockableRooms.Contains(room))
            {
                continue;
            }

            foreach (var tile in room.WalkableTiles)
            {
                if (reachable.Contains(tile) && !occupied.Contains(tile) && world.GetTile(tile) == TileType.Floor)
                {
                    keyCandidates.Add(tile);
                }
            }
        }

        // A key is consumed per door, not per room. Reduce the lock set before
        // committing it when the reachable part of the floor cannot supply enough keys.
        var keysNeeded = Math.Min(lockedDoors.Count, keyCandidates.Count);
        for (var i = lockedDoors.Count - 1; i >= keysNeeded; i--)
        {
            world.SetTile(lockedDoors[i], TileType.Door);
            lockedDoors.RemoveAt(i);
        }

        var keySpawns = new List<Position>(keysNeeded);
        for (var i = 0; i < keysNeeded; i++)
        {
            var candidateIndex = rng.Next(keyCandidates.Count);
            var keyPosition = keyCandidates[candidateIndex];
            keyCandidates.RemoveAt(candidateIndex);
            occupied.Add(keyPosition);
            keySpawns.Add(keyPosition);
        }

        return (lockedDoors, keySpawns);
    }

    private static HashSet<Position> ComputeReachablePositions(WorldState world, Position origin)
    {
        var reachable = new HashSet<Position>();
        var queue = new Queue<Position>();

        if (!world.InBounds(origin))
        {
            return reachable;
        }

        queue.Enqueue(origin);
        reachable.Add(origin);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var delta in Position.Cardinals)
            {
                var neighbor = current + delta;
                if (!world.InBounds(neighbor) || reachable.Contains(neighbor))
                {
                    continue;
                }

                var tile = world.GetTile(neighbor);
                if (tile is TileType.Wall or TileType.LockedDoor or TileType.Void)
                {
                    continue;
                }

                if (!world.IsWalkable(neighbor))
                {
                    continue;
                }

                reachable.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return reachable;
    }

    private static IReadOnlyList<RoomPrefab> ResolvePrefabs(WorldState world, int depth)
    {
        if (world.ContentDatabase is not ContentLoader loader || loader.RoomPrefabs.Count == 0)
        {
            return RoomPrefabLibrary.GetDefaultPrefabs();
        }

        var effectiveDepth = Math.Max(1, depth);
        var prefabs = loader.RoomPrefabs.Values
            .OrderBy(room => room.Id, StringComparer.Ordinal)
            .Where(room => effectiveDepth >= room.MinDepth && (room.MaxDepth < 0 || effectiveDepth <= room.MaxDepth))
            .Where(room => room.Layout.Count > 0)
            .Select(room => new RoomPrefab(
                room.Id,
                room.Layout,
                room.SpawnPoints.Select(spawnPoint => new RoomPrefabSpawnPoint(spawnPoint.X, spawnPoint.Y, spawnPoint.Type, spawnPoint.TrapId)).ToArray(),
                room.FixedEntities.Select(fixedEntity => new RoomPrefabFixedEntity(fixedEntity.X, fixedEntity.Y, fixedEntity.EntityType, fixedEntity.TemplateId)).ToArray(),
                room.ItemQualityBonus,
                room.EnemyCountBonus,
                room.LockDoorsOnEnter,
                room.Tags.AsReadOnly()))
            .ToArray();

        return prefabs.Length > 0 ? prefabs : RoomPrefabLibrary.GetDefaultPrefabs();
    }

    private static bool TryGetExplicitSpawn(
        RoomPlacement room,
        HashSet<Position> occupied,
        out Position position,
        params string[] types)
    {
        foreach (var placement in room.GetSpawnPlacements(types))
        {
            if (occupied.Add(placement.Position))
            {
                position = placement.Position;
                return true;
            }
        }

        position = Position.Invalid;
        return false;
    }

    private static List<EnemySpawnData> CollectEnemySpawnDetails(
        IReadOnlyList<RoomPlacement> rooms,
        HashSet<Position> occupied,
        IReadOnlyList<EnemySpawnData>? seededSpawns = null,
        params string[] types)
    {
        var spawns = new List<EnemySpawnData>();
        if (seededSpawns is not null)
        {
            spawns.AddRange(seededSpawns);
        }

        foreach (var room in rooms)
        {
            foreach (var placement in room.GetSpawnPlacements(types))
            {
                if (occupied.Add(placement.Position))
                {
                    spawns.Add(new EnemySpawnData(
                        placement.Position,
                        IsBoss: string.Equals(placement.SpawnPoint.Type, "enemy_boss", StringComparison.OrdinalIgnoreCase)));
                }
            }
        }

        return spawns;
    }

    private static List<ItemSpawnData> CollectItemSpawnDetails(
        IReadOnlyList<RoomPlacement> rooms,
        HashSet<Position> occupied,
        IReadOnlyList<ItemSpawnData>? seededSpawns = null,
        params string[] types)
    {
        var spawns = new List<ItemSpawnData>();
        if (seededSpawns is not null)
        {
            spawns.AddRange(seededSpawns);
        }

        foreach (var room in rooms)
        {
            foreach (var placement in room.GetSpawnPlacements(types))
            {
                if (occupied.Add(placement.Position))
                {
                    spawns.Add(new ItemSpawnData(
                        placement.Position,
                        QualityBonus: Math.Max(0, room.Prefab?.ItemQualityBonus ?? 0)));
                }
            }
        }

        return spawns;
    }

    private static List<ChestSpawnData> CollectChestSpawnDetails(
        IReadOnlyList<RoomPlacement> rooms,
        HashSet<Position> occupied,
        IReadOnlyList<ChestSpawnData>? seededSpawns = null)
    {
        var spawns = new List<ChestSpawnData>();
        if (seededSpawns is not null)
        {
            spawns.AddRange(seededSpawns);
        }

        foreach (var room in rooms)
        {
            foreach (var placement in room.GetSpawnPlacements("chest", "curse_chest"))
            {
                if (occupied.Add(placement.Position))
                {
                    var lootTableId = string.Equals(placement.SpawnPoint.Type, "curse_chest", StringComparison.OrdinalIgnoreCase)
                        ? "curse_room_chest_loot"
                        : placement.SpawnPoint.ReferenceId;
                    spawns.Add(new ChestSpawnData(placement.Position, lootTableId));
                }
            }
        }

        return spawns;
    }

    private static List<ShrineSpawnData> CollectShrineSpawnDetails(
        IReadOnlyList<RoomPlacement> rooms,
        FloorEventPlan floorPlan,
        HashSet<Position> occupied)
    {
        var spawns = new List<ShrineSpawnData>();
        foreach (var request in floorPlan.SpecialRooms)
        {
            if (request.Type != SpecialRoomType.ShrineRoom)
            {
                continue;
            }

            foreach (var room in rooms)
            {
                if (room.Prefab?.Tags.Contains(request.PrefabTag) != true)
                {
                    continue;
                }

                foreach (var placement in room.GetSpawnPlacements("shrine"))
                {
                    if (occupied.Add(placement.Position))
                    {
                        spawns.Add(new ShrineSpawnData(placement.Position, request.EventId));
                    }
                }

                break;
            }
        }

        return spawns;
    }

    private static List<LandmarkSpawnData> CreateLandmarkDetails(
        IReadOnlyList<RoomPlacement> rooms,
        RoomPlacement startRoom,
        FloorEventPlan floorPlan,
        IList<ShrineSpawnData> shrineSpawns,
        IList<ChestSpawnData> chestSpawns,
        Random rng,
        HashSet<Position> occupied)
    {
        var landmarks = new List<LandmarkSpawnData>();
        foreach (var request in floorPlan.SpecialRooms)
        {
            if (request.Type == SpecialRoomType.ShrineRoom)
            {
                var shrine = shrineSpawns.FirstOrDefault(spawn => spawn.EventId == request.EventId);
                if (shrine is not null)
                {
                    landmarks.Add(new LandmarkSpawnData(shrine.Position, request.Type, request.EventId));
                    continue;
                }
            }
            else if (request.Type == SpecialRoomType.CurseRoom
                && chestSpawns.Any(spawn => spawn.LootTableId == "curse_room_chest_loot"))
            {
                var chest = chestSpawns.First(spawn => spawn.LootTableId == "curse_room_chest_loot");
                landmarks.Add(new LandmarkSpawnData(chest.Position, request.Type, request.EventId));
                continue;
            }

            // Requested special rooms are best-effort prefab placements. Keep the
            // reward contract reliable when a suitable leaf was unavailable.
            var fallbackPosition = PickAvailableTile(rooms, startRoom, rng, occupied);
            occupied.Add(fallbackPosition);
            if (request.Type == SpecialRoomType.ShrineRoom)
            {
                shrineSpawns.Add(new ShrineSpawnData(fallbackPosition, request.EventId));
            }
            else if (request.Type == SpecialRoomType.CurseRoom)
            {
                chestSpawns.Add(new ChestSpawnData(fallbackPosition, "curse_room_chest_loot"));
            }

            landmarks.Add(new LandmarkSpawnData(fallbackPosition, request.Type, request.EventId, IsFallback: true));
        }

        return landmarks;
    }

    private static List<TrapSpawnData> CollectTrapSpawnDetails(
        IReadOnlyList<RoomPlacement> rooms,
        HashSet<Position> occupied,
        string? themeTag,
        int seed)
    {
        var spawns = new List<TrapSpawnData>();

        foreach (var room in rooms)
        {
            if (room.Prefab is null)
            {
                continue;
            }

            for (var y = 0; y < room.Prefab.Height; y++)
            {
                for (var x = 0; x < room.Prefab.Width; x++)
                {
                    if (room.Prefab.GetTileType(x, y) == TileType.Trap)
                    {
                        var position = new Position(room.Origin.X + x, room.Origin.Y + y);
                        if (occupied.Add(position))
                        {
                            spawns.Add(new TrapSpawnData(position, ResolveTrapId(themeTag, position, seed)));
                        }
                    }
                }
            }

            foreach (var placement in room.GetSpawnPlacements("trap"))
            {
                if (occupied.Add(placement.Position))
                {
                    spawns.Add(new TrapSpawnData(
                        placement.Position,
                        placement.SpawnPoint.ReferenceId ?? ResolveTrapId(themeTag, placement.Position, seed)));
                }
            }
        }

        return spawns;
    }

    private static string ResolveTrapId(string? themeTag, Position position, int seed)
    {
        var options = themeTag switch
        {
            "prison" => PrisonTrapIds,
            "crypt" => CryptTrapIds,
            "magma" => MagmaTrapIds,
            _ => DefaultTrapIds,
        };

        unchecked
        {
            // Seed-and-position hash keeps trap flavor deterministic for a given level.
            var hash = seed;
            hash = (hash * 31) + position.X;
            hash = (hash * 31) + position.Y;
            return options[(int)((uint)hash % (uint)options.Length)];
        }
    }

    private static FixedEntityCollections CollectFixedEntitySpawns(
        IContentDatabase? content,
        IReadOnlyList<RoomPlacement> rooms,
        HashSet<Position> occupied)
    {
        var enemySpawns = new List<EnemySpawnData>();
        var itemSpawns = new List<ItemSpawnData>();
        var chestSpawns = new List<ChestSpawnData>();
        var npcSpawns = new List<NpcSpawnData>();

        foreach (var room in rooms)
        {
            foreach (var placement in room.GetFixedEntityPlacements())
            {
                if (!occupied.Add(placement.Position))
                {
                    continue;
                }

                switch (ResolveFixedEntityType(content, placement.FixedEntity))
                {
                    case "enemy":
                        enemySpawns.Add(new EnemySpawnData(placement.Position, placement.FixedEntity.TemplateId));
                        break;
                    case "item":
                        itemSpawns.Add(new ItemSpawnData(
                            placement.Position,
                            placement.FixedEntity.TemplateId,
                            Math.Max(0, room.Prefab?.ItemQualityBonus ?? 0)));
                        break;
                    case "chest":
                        chestSpawns.Add(new ChestSpawnData(placement.Position, placement.FixedEntity.TemplateId));
                        break;
                    case "npc" when !string.IsNullOrWhiteSpace(placement.FixedEntity.TemplateId):
                        npcSpawns.Add(new NpcSpawnData(placement.Position, placement.FixedEntity.TemplateId!));
                        break;
                }
            }
        }

        return new FixedEntityCollections(enemySpawns, itemSpawns, chestSpawns, npcSpawns);
    }

    private static string ResolveFixedEntityType(IContentDatabase? content, RoomPrefabFixedEntity fixedEntity)
    {
        if (!string.IsNullOrWhiteSpace(fixedEntity.EntityType))
        {
            return fixedEntity.EntityType!.Trim().ToLowerInvariant();
        }

        if (string.IsNullOrWhiteSpace(fixedEntity.TemplateId) || content is null)
        {
            return string.Empty;
        }

        if (content.TryGetItemTemplate(fixedEntity.TemplateId, out _))
        {
            return "item";
        }

        if (content.TryGetEnemyTemplate(fixedEntity.TemplateId, out _))
        {
            return "enemy";
        }

        if (content.TryGetNpcTemplate(fixedEntity.TemplateId, out _))
        {
            return "npc";
        }

        return string.Empty;
    }
}
