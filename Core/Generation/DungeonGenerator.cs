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

    private sealed record FixedEntityCollections(
        List<EnemySpawnData> EnemySpawns,
        List<ItemSpawnData> ItemSpawns,
        List<ChestSpawnData> ChestSpawns,
        List<NpcSpawnData> NpcSpawns);

    public LevelData GenerateLevel(WorldState world, int seed, int depth)
    {
        ArgumentNullException.ThrowIfNull(world);

        var prefabs = ResolvePrefabs(world, depth);
        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var attemptSeed = MixSeed(seed, depth, attempt);
            var rng = new Random(attemptSeed);
            var mapSize = GetMapSize(depth);

            InitializeWorld(world, mapSize.Width, mapSize.Height, seed, depth);

            var tree = BSPNode.Create(mapSize.Width, mapSize.Height, rng);
            var themeTag = ResolveThemeForDepth(depth);
            var useTheme = !string.IsNullOrWhiteSpace(themeTag) && HasEnoughThemeMatches(prefabs, themeTag, tree);
            var rooms = RoomPlacer.PlaceRooms(tree, world, rng, prefabs, useTheme ? themeTag : null);
            CorridorBuilder.Stitch(tree, world, rng);
            DoorSanitizer.Normalize(world);

            if (rooms.Count < 4)
            {
                continue;
            }

            var startRoom = rooms[0];
            var exitRoom = SelectFarthestRoom(startRoom, rooms);
            if (ReferenceEquals(startRoom, exitRoom))
            {
                continue;
            }

            var occupied = new HashSet<Position>();
            var playerSpawn = TryGetExplicitSpawn(startRoom, occupied, out var explicitPlayerSpawn, "player")
                ? explicitPlayerSpawn
                : PlaceStairs(world, startRoom, TileType.StairsUp, rng, occupied);
            world.SetTile(playerSpawn, TileType.StairsUp);

            var stairsDown = TryGetExplicitSpawn(exitRoom, occupied, out var explicitExitSpawn, "stairs_down")
                ? explicitExitSpawn
                : PlaceStairs(world, exitRoom, TileType.StairsDown, rng, occupied);
            world.SetTile(stairsDown, TileType.StairsDown);

            var fixedEntities = CollectFixedEntitySpawns(world.ContentDatabase, rooms, occupied);
            var chestSpawnDetails = CollectChestSpawnDetails(rooms, occupied, fixedEntities.ChestSpawns);
            var forcedEnemySpawns = CollectEnemySpawnDetails(rooms, occupied, fixedEntities.EnemySpawns, "enemy", "enemy_boss");
            var forcedItemSpawns = CollectItemSpawnDetails(rooms, occupied, fixedEntities.ItemSpawns, "item");
            var trapSpawnDetails = CollectTrapSpawnDetails(rooms, occupied);
            var (lockedDoors, keySpawns) = PlaceLockedDoorsAndKeys(world, rooms, playerSpawn, rng, occupied);
            var enemySpawnDetails = PlaceEnemySpawns(rooms, startRoom, depth, rng, occupied, forcedEnemySpawns);
            var itemSpawnDetails = PlaceItemSpawns(rooms, depth, rng, occupied, forcedItemSpawns);

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
                keySpawns);
            if (LevelValidator.Validate(world, level).Count == 0)
            {
                return level;
            }
        }

        throw new InvalidOperationException("Failed to generate a connected dungeon level within the retry limit.");
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
            if (!prefabs[i].Tags.Contains(themeTag))
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

    private static RoomPlacement SelectFarthestRoom(RoomPlacement startRoom, IReadOnlyList<RoomPlacement> rooms)
    {
        var farthestRoom = startRoom;
        var farthestDistance = -1;

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
        Random rng,
        HashSet<Position> occupied,
        IReadOnlyList<EnemySpawnData>? forcedSpawns = null)
    {
        var floor = Math.Max(depth, 0);
        var bonusEnemies = rooms.Sum(room => Math.Max(0, room.Prefab?.EnemyCountBonus ?? 0));
        var enemyCount = Math.Min(MaxEnemies, 3 + (floor * 2) + bonusEnemies);
        var spawns = new List<EnemySpawnData>(enemyCount);

        if (forcedSpawns is not null)
        {
            spawns.AddRange(forcedSpawns);
        }

        for (var i = spawns.Count; i < enemyCount; i++)
        {
            var room = PickSpawnRoom(rooms, startRoom, avoidStartRoom: i < 2, rng);
            var position = PickAvailableTile(rooms, room, rng, occupied);
            occupied.Add(position);
            spawns.Add(new EnemySpawnData(position));
        }

        return spawns;
    }

    private static List<ItemSpawnData> PlaceItemSpawns(
        IReadOnlyList<RoomPlacement> rooms,
        int depth,
        Random rng,
        HashSet<Position> occupied,
        IReadOnlyList<ItemSpawnData>? forcedSpawns = null)
    {
        var floor = Math.Max(depth, 0);
        var itemCount = Math.Min(MaxItems, 2 + floor);
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

    private static RoomPlacement PickSpawnRoom(
        IReadOnlyList<RoomPlacement> rooms,
        RoomPlacement startRoom,
        bool avoidStartRoom,
        Random rng)
    {
        if (!avoidStartRoom || rooms.Count == 1)
        {
            return rooms[rng.Next(rooms.Count)];
        }

        var candidates = new List<RoomPlacement>(rooms.Count - 1);
        for (var i = 0; i < rooms.Count; i++)
        {
            if (!ReferenceEquals(rooms[i], startRoom))
            {
                candidates.Add(rooms[i]);
            }
        }

        return candidates[rng.Next(candidates.Count)];
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

                if (adjacentRooms.Any(room => lockableRooms.Contains(room)))
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

        var keySpawns = new List<Position>();
        if (keyCandidates.Count > 0)
        {
            var keyPosition = keyCandidates[rng.Next(keyCandidates.Count)];
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

        var prefabs = loader.RoomPrefabs.Values
            .Where(room => depth >= room.MinDepth && (room.MaxDepth < 0 || depth <= room.MaxDepth))
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
            foreach (var placement in room.GetSpawnPlacements("chest"))
            {
                if (occupied.Add(placement.Position))
                {
                    spawns.Add(new ChestSpawnData(placement.Position, placement.SpawnPoint.ReferenceId));
                }
            }
        }

        return spawns;
    }

    private static List<TrapSpawnData> CollectTrapSpawnDetails(
        IReadOnlyList<RoomPlacement> rooms,
        HashSet<Position> occupied)
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
                            spawns.Add(new TrapSpawnData(position));
                        }
                    }
                }
            }

            foreach (var placement in room.GetSpawnPlacements("trap"))
            {
                if (occupied.Add(placement.Position))
                {
                    spawns.Add(new TrapSpawnData(placement.Position, placement.SpawnPoint.ReferenceId));
                }
            }
        }

        return spawns;
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
