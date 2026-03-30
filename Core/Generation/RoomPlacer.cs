using System;
using System.Collections.Generic;

namespace Roguelike.Core.Generation;

public static class RoomPlacer
{
    private const int MinRoomSize = 5;
    private const int MaxRoomSize = 15;
    private const int WallPadding = 1;

    public static List<RoomData> PlaceRooms(BSPNode root, Random rng)
    {
        var rooms = new List<RoomData>();
        PlaceRoomsRecursive(root, rng, rooms);
        return rooms;
    }

    private static void PlaceRoomsRecursive(BSPNode node, Random rng, List<RoomData> rooms)
    {
        if (node.IsLeaf)
        {
            int maxW = Math.Min(MaxRoomSize, node.Width - 2 * WallPadding);
            int maxH = Math.Min(MaxRoomSize, node.Height - 2 * WallPadding);

            if (maxW < MinRoomSize || maxH < MinRoomSize) return;

            int roomW = rng.Next(MinRoomSize, maxW + 1);
            int roomH = rng.Next(MinRoomSize, maxH + 1);

            int roomX = node.X + WallPadding + rng.Next(0, maxW - roomW + 1);
            int roomY = node.Y + WallPadding + rng.Next(0, maxH - roomH + 1);

            var center = new Position(roomX + roomW / 2, roomY + roomH / 2);
            var room = new RoomData(roomX, roomY, roomW, roomH, center);
            node.Room = room;
            rooms.Add(room);
            return;
        }

        if (node.Left is not null) PlaceRoomsRecursive(node.Left, rng, rooms);
        if (node.Right is not null) PlaceRoomsRecursive(node.Right, rng, rooms);
    }
}
