using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class DungeonGenerator : IGenerator
{
    private const int MaxGenerationAttempts = 10;
    private const int MaxEnemies = 20;
    private const int MaxItems = 10;

    public LevelData GenerateLevel(WorldState world, int seed, int depth)
    {
        ArgumentNullException.ThrowIfNull(world);

        var prefabs = RoomPrefabLibrary.GetDefaultPrefabs();
        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var attemptSeed = MixSeed(seed, depth, attempt);
            var rng = new Random(attemptSeed);
            var mapSize = GetMapSize(depth);

            InitializeWorld(world, mapSize.Width, mapSize.Height, seed, depth);

            var tree = BSPNode.Create(mapSize.Width, mapSize.Height, rng);
            var rooms = RoomPlacer.PlaceRooms(tree, world, rng, prefabs);
            CorridorBuilder.Stitch(tree, world, rng);

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
            var playerSpawn = PlaceStairs(world, startRoom, TileType.StairsUp, rng, occupied);
            var stairsDown = PlaceStairs(world, exitRoom, TileType.StairsDown, rng, occupied);
            var enemySpawns = PlaceEnemySpawns(rooms, startRoom, depth, rng, occupied);
            var itemSpawns = PlaceItemSpawns(rooms, depth, rng, occupied);

            var roomData = new List<RoomData>(rooms.Count);
            for (var i = 0; i < rooms.Count; i++)
            {
                roomData.Add(rooms[i].Room);
            }

            var level = new LevelData(playerSpawn, stairsDown, enemySpawns, itemSpawns, roomData);
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

    private static List<Position> PlaceEnemySpawns(
        IReadOnlyList<RoomPlacement> rooms,
        RoomPlacement startRoom,
        int depth,
        Random rng,
        HashSet<Position> occupied)
    {
        var floor = Math.Max(depth, 0);
        var enemyCount = Math.Min(MaxEnemies, 3 + (floor * 2));
        var spawns = new List<Position>(enemyCount);

        for (var i = 0; i < enemyCount; i++)
        {
            var room = PickSpawnRoom(rooms, startRoom, avoidStartRoom: i < 2, rng);
            var position = PickAvailableTile(rooms, room, rng, occupied);
            occupied.Add(position);
            spawns.Add(position);
        }

        return spawns;
    }

    private static List<Position> PlaceItemSpawns(
        IReadOnlyList<RoomPlacement> rooms,
        int depth,
        Random rng,
        HashSet<Position> occupied)
    {
        var floor = Math.Max(depth, 0);
        var itemCount = Math.Min(MaxItems, 2 + floor);
        var spawns = new List<Position>(itemCount);

        for (var i = 0; i < itemCount; i++)
        {
            var room = rooms[rng.Next(rooms.Count)];
            var position = PickAvailableTile(rooms, room, rng, occupied);
            occupied.Add(position);
            spawns.Add(position);
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
}