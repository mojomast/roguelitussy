using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public static class RoomPlacer
{
    private const int LeafPadding = 1;
    private const int MinimumRectRoomSize = 4;

    public static IReadOnlyList<RoomPlacement> PlaceRooms(
        BSPNode root,
        WorldState world,
        Random rng,
        IReadOnlyList<RoomPrefab> prefabs)
    {
        var rooms = new List<RoomPlacement>();

        foreach (var leaf in root.Leaves())
        {
            var room = CreatePlacement(leaf, rng, prefabs);
            Carve(world, room);
            leaf.Room = room;
            rooms.Add(room);
        }

        return rooms;
    }

    private static RoomPlacement CreatePlacement(BSPNode leaf, Random rng, IReadOnlyList<RoomPrefab> prefabs)
    {
        var usableWidth = leaf.Width - (LeafPadding * 2);
        var usableHeight = leaf.Height - (LeafPadding * 2);
        if (usableWidth < MinimumRectRoomSize || usableHeight < MinimumRectRoomSize)
        {
            throw new InvalidOperationException("BSP leaf is too small to place a room.");
        }

        var fittingPrefabs = new List<RoomPrefab>();
        for (var i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i].FitsWithin(usableWidth, usableHeight))
            {
                fittingPrefabs.Add(prefabs[i]);
            }
        }

        if (fittingPrefabs.Count > 0)
        {
            return BuildPrefabRoom(leaf, fittingPrefabs[rng.Next(fittingPrefabs.Count)], rng);
        }

        return BuildRectangularRoom(leaf, rng, usableWidth, usableHeight);
    }

    private static RoomPlacement BuildPrefabRoom(BSPNode leaf, RoomPrefab prefab, Random rng)
    {
        var origin = CenterInLeaf(leaf, prefab.Width, prefab.Height, rng);
        var walkableTiles = new List<Position>();

        for (var y = 0; y < prefab.Height; y++)
        {
            for (var x = 0; x < prefab.Width; x++)
            {
                if (prefab.GetTileType(x, y) != TileType.Wall)
                {
                    walkableTiles.Add(new Position(origin.X + x, origin.Y + y));
                }
            }
        }

        var center = new Position(origin.X + (prefab.Width / 2), origin.Y + (prefab.Height / 2));
        return new RoomPlacement(new RoomData(origin.X, origin.Y, prefab.Width, prefab.Height, center), walkableTiles, origin, prefab);
    }

    private static RoomPlacement BuildRectangularRoom(BSPNode leaf, Random rng, int usableWidth, int usableHeight)
    {
        var roomWidth = rng.Next(MinimumRectRoomSize, usableWidth + 1);
        var roomHeight = rng.Next(MinimumRectRoomSize, usableHeight + 1);
        var origin = CenterInLeaf(leaf, roomWidth, roomHeight, rng);
        var walkableTiles = new List<Position>(roomWidth * roomHeight);

        for (var y = 0; y < roomHeight; y++)
        {
            for (var x = 0; x < roomWidth; x++)
            {
                walkableTiles.Add(new Position(origin.X + x, origin.Y + y));
            }
        }

        var center = new Position(origin.X + (roomWidth / 2), origin.Y + (roomHeight / 2));
        return new RoomPlacement(new RoomData(origin.X, origin.Y, roomWidth, roomHeight, center), walkableTiles, origin, null);
    }

    private static Position CenterInLeaf(BSPNode leaf, int roomWidth, int roomHeight, Random rng)
    {
        var slackX = leaf.Width - (LeafPadding * 2) - roomWidth;
        var slackY = leaf.Height - (LeafPadding * 2) - roomHeight;

        var x = leaf.X + LeafPadding + (slackX / 2);
        var y = leaf.Y + LeafPadding + (slackY / 2);

        if (slackX > 1)
        {
            x = Math.Clamp(x + rng.Next(-1, 2), leaf.X + LeafPadding, leaf.X + LeafPadding + slackX);
        }

        if (slackY > 1)
        {
            y = Math.Clamp(y + rng.Next(-1, 2), leaf.Y + LeafPadding, leaf.Y + LeafPadding + slackY);
        }

        return new Position(x, y);
    }

    private static void Carve(WorldState world, RoomPlacement room)
    {
        if (room.Prefab is null)
        {
            for (var i = 0; i < room.WalkableTiles.Count; i++)
            {
                world.SetTile(room.WalkableTiles[i], TileType.Floor);
            }

            return;
        }

        for (var y = 0; y < room.Prefab.Height; y++)
        {
            for (var x = 0; x < room.Prefab.Width; x++)
            {
                var tile = room.Prefab.GetTileType(x, y);
                if (tile != TileType.Wall)
                {
                    world.SetTile(new Position(room.Origin.X + x, room.Origin.Y + y), tile);
                }
            }
        }
    }
}

public sealed class RoomPlacement
{
    private readonly HashSet<Position> _walkableLookup;

    public RoomPlacement(RoomData room, IReadOnlyList<Position> walkableTiles, Position origin, RoomPrefab? prefab)
    {
        if (walkableTiles.Count == 0)
        {
            throw new ArgumentException("Room placement must include at least one walkable tile.", nameof(walkableTiles));
        }

        Room = room;
        WalkableTiles = walkableTiles;
        Origin = origin;
        Prefab = prefab;
        _walkableLookup = new HashSet<Position>(walkableTiles);

        var connectionPoint = walkableTiles[0];
        var bestDistance = connectionPoint.DistanceTo(room.Center);
        for (var i = 1; i < walkableTiles.Count; i++)
        {
            var distance = walkableTiles[i].DistanceTo(room.Center);
            if (distance < bestDistance)
            {
                connectionPoint = walkableTiles[i];
                bestDistance = distance;
            }
        }

        ConnectionPoint = connectionPoint;
    }

    public RoomData Room { get; }

    public IReadOnlyList<Position> WalkableTiles { get; }

    public Position Origin { get; }

    public RoomPrefab? Prefab { get; }

    public Position ConnectionPoint { get; }

    public bool Contains(Position position) => _walkableLookup.Contains(position);

    public Position GetRandomWalkableTile(Random rng, HashSet<Position> occupied)
    {
        var startIndex = rng.Next(WalkableTiles.Count);

        for (var offset = 0; offset < WalkableTiles.Count; offset++)
        {
            var candidate = WalkableTiles[(startIndex + offset) % WalkableTiles.Count];
            if (!occupied.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Room has no unoccupied walkable tiles.");
    }
}