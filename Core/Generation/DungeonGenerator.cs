using System;
using System.Collections.Generic;

namespace Roguelike.Core.Generation;

public sealed class DungeonGenerator : IGenerator
{
    private const int MapWidth = 80;
    private const int MapHeight = 50;
    private const int MaxSplitDepth = 5;

    public LevelData GenerateLevel(WorldState world, int seed, int depth)
    {
        var rng = new Random(seed ^ (depth * 7919));

        world.InitGrid(MapWidth, MapHeight);

        // Fill entire grid with walls
        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
                world.SetTile(new Position(x, y), TileType.Wall);

        // Build BSP tree
        var root = new BSPNode(0, 0, MapWidth, MapHeight);
        SplitRecursive(root, rng, 0);

        // Place rooms in leaves
        var rooms = RoomPlacer.PlaceRooms(root, rng);

        // Carve rooms into the grid
        foreach (var room in rooms)
            CarveRoom(world, room);

        // Connect rooms via corridors
        CorridorBuilder.ConnectRooms(root, world, rng);

        // Place doors at room entrances
        PlaceDoors(world, rooms);

        // Player spawns in the center of the first room
        var playerSpawn = rooms[0].Center;

        // Stairs down in the room farthest from player spawn
        int farthestIdx = 0;
        int farthestDist = 0;
        for (int i = 1; i < rooms.Count; i++)
        {
            int dist = playerSpawn.DistanceTo(rooms[i].Center);
            if (dist > farthestDist)
            {
                farthestDist = dist;
                farthestIdx = i;
            }
        }

        var stairsPos = rooms[farthestIdx].Center;
        world.SetTile(stairsPos, TileType.StairsDown);

        // Scatter enemy spawns: 1-2 per room
        var enemySpawns = new List<Position>();
        foreach (var room in rooms)
        {
            int count = rng.Next(1, 3);
            var candidates = GetFloorTilesInRoom(world, room);
            // Remove player spawn and stairs from candidates
            candidates.Remove(playerSpawn);
            candidates.Remove(stairsPos);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int idx = rng.Next(candidates.Count);
                enemySpawns.Add(candidates[idx]);
                candidates.RemoveAt(idx);
            }
        }

        // Scatter item spawns: 0-1 per room
        var itemSpawns = new List<Position>();
        foreach (var room in rooms)
        {
            if (rng.Next(2) == 0) continue;
            var candidates = GetFloorTilesInRoom(world, room);
            candidates.Remove(playerSpawn);
            candidates.Remove(stairsPos);
            // Also avoid positions already used for enemies
            foreach (var e in enemySpawns)
                candidates.Remove(e);
            if (candidates.Count > 0)
            {
                int idx = rng.Next(candidates.Count);
                itemSpawns.Add(candidates[idx]);
            }
        }

        return new LevelData(playerSpawn, stairsPos, enemySpawns, itemSpawns, rooms);
    }

    public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data)
    {
        return LevelValidator.Validate(world, data);
    }

    private static void SplitRecursive(BSPNode node, Random rng, int depth)
    {
        if (depth >= MaxSplitDepth) return;
        if (!node.Split(rng)) return;
        SplitRecursive(node.Left!, rng, depth + 1);
        SplitRecursive(node.Right!, rng, depth + 1);
    }

    private static void CarveRoom(WorldState world, RoomData room)
    {
        for (int y = room.Y; y < room.Y + room.Height; y++)
            for (int x = room.X; x < room.X + room.Width; x++)
                world.SetTile(new Position(x, y), TileType.Floor);
    }

    private static void PlaceDoors(WorldState world, List<RoomData> rooms)
    {
        foreach (var room in rooms)
        {
            // Check the perimeter of the room for corridor connections
            // Top and bottom edges
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                TryPlaceDoor(world, new Position(x, room.Y - 1), new Position(x, room.Y));
                TryPlaceDoor(world, new Position(x, room.Y + room.Height), new Position(x, room.Y + room.Height - 1));
            }
            // Left and right edges
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                TryPlaceDoor(world, new Position(room.X - 1, y), new Position(room.X, y));
                TryPlaceDoor(world, new Position(room.X + room.Width, y), new Position(room.X + room.Width - 1, y));
            }
        }
    }

    private static void TryPlaceDoor(WorldState world, Position corridorSide, Position roomEdge)
    {
        if (!world.InBounds(corridorSide)) return;
        // If corridorSide is a floor tile carved by a corridor, and roomEdge is floor,
        // the wall between them was carved away. Check the wall tile ON the room boundary itself.
        // Actually: we look for a wall tile between a corridor floor and room floor.
        // The corridor carves through walls, so look for the transition point.
        // A door candidate: a floor tile with walls on two opposite sides (narrow passage).
        if (world.GetTile(corridorSide) != TileType.Floor) return;

        // Check if corridorSide is a narrow passage (walls on perpendicular sides)
        var pos = corridorSide;
        bool wallAboveBelow = IsWallOrVoid(world, pos.Offset(0, -1)) && IsWallOrVoid(world, pos.Offset(0, 1));
        bool wallLeftRight = IsWallOrVoid(world, pos.Offset(-1, 0)) && IsWallOrVoid(world, pos.Offset(1, 0));

        if (wallAboveBelow || wallLeftRight)
        {
            world.SetTile(pos, TileType.Door);
        }
    }

    private static bool IsWallOrVoid(WorldState world, Position pos)
    {
        if (!world.InBounds(pos)) return true;
        var tile = world.GetTile(pos);
        return tile is TileType.Wall or TileType.Void;
    }

    private static List<Position> GetFloorTilesInRoom(WorldState world, RoomData room)
    {
        var tiles = new List<Position>();
        for (int y = room.Y; y < room.Y + room.Height; y++)
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                var pos = new Position(x, y);
                if (world.GetTile(pos) == TileType.Floor)
                    tiles.Add(pos);
            }
        return tiles;
    }
}
