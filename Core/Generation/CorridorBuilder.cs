using System;

namespace Roguelike.Core.Generation;

public static class CorridorBuilder
{
    public static void ConnectRooms(BSPNode node, WorldState world, Random rng)
    {
        if (node.IsLeaf) return;

        if (node.Left is not null) ConnectRooms(node.Left, world, rng);
        if (node.Right is not null) ConnectRooms(node.Right, world, rng);

        if (node.Left is null || node.Right is null) return;

        var leftRoom = node.Left.GetRoom();
        var rightRoom = node.Right.GetRoom();

        if (leftRoom is null || rightRoom is null) return;

        CarveCorridor(leftRoom.Center, rightRoom.Center, world, rng);
    }

    private static void CarveCorridor(Position a, Position b, WorldState world, Random rng)
    {
        bool horizontalFirst = rng.Next(2) == 0;

        if (horizontalFirst)
        {
            CarveHorizontal(a.X, b.X, a.Y, world);
            CarveVertical(a.Y, b.Y, b.X, world);
        }
        else
        {
            CarveVertical(a.Y, b.Y, a.X, world);
            CarveHorizontal(a.X, b.X, b.Y, world);
        }
    }

    private static void CarveHorizontal(int x1, int x2, int y, WorldState world)
    {
        int start = Math.Min(x1, x2);
        int end = Math.Max(x1, x2);
        for (int x = start; x <= end; x++)
        {
            var pos = new Position(x, y);
            if (world.InBounds(pos) && world.GetTile(pos) != TileType.Floor
                && world.GetTile(pos) != TileType.StairsDown
                && world.GetTile(pos) != TileType.StairsUp)
            {
                world.SetTile(pos, TileType.Floor);
            }
        }
    }

    private static void CarveVertical(int y1, int y2, int x, WorldState world)
    {
        int start = Math.Min(y1, y2);
        int end = Math.Max(y1, y2);
        for (int y = start; y <= end; y++)
        {
            var pos = new Position(x, y);
            if (world.InBounds(pos) && world.GetTile(pos) != TileType.Floor
                && world.GetTile(pos) != TileType.StairsDown
                && world.GetTile(pos) != TileType.StairsUp)
            {
                world.SetTile(pos, TileType.Floor);
            }
        }
    }
}
