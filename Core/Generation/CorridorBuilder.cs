using System;

namespace Roguelike.Core;

public static class CorridorBuilder
{
    public static void Stitch(BSPNode root, WorldState world, Random rng)
    {
        if (root.Left is null || root.Right is null)
        {
            return;
        }

        Stitch(root.Left, world, rng);
        Stitch(root.Right, world, rng);

        var leftRoom = root.Left.PickRoom(rng);
        var rightRoom = root.Right.PickRoom(rng);
        if (leftRoom is null || rightRoom is null)
        {
            return;
        }

        Connect(world, leftRoom.ConnectionPoint, rightRoom.ConnectionPoint, rng);
    }

    public static void Connect(WorldState world, Position from, Position to, Random rng)
    {
        if (rng.Next(2) == 0)
        {
            CarveHorizontal(world, from.X, to.X, from.Y);
            CarveVertical(world, from.Y, to.Y, to.X);
        }
        else
        {
            CarveVertical(world, from.Y, to.Y, from.X);
            CarveHorizontal(world, from.X, to.X, to.Y);
        }
    }

    private static void CarveHorizontal(WorldState world, int x1, int x2, int y)
    {
        var start = Math.Min(x1, x2);
        var end = Math.Max(x1, x2);
        for (var x = start; x <= end; x++)
        {
            world.SetTile(new Position(x, y), TileType.Floor);
        }
    }

    private static void CarveVertical(WorldState world, int y1, int y2, int x)
    {
        var start = Math.Min(y1, y2);
        var end = Math.Max(y1, y2);
        for (var y = start; y <= end; y++)
        {
            world.SetTile(new Position(x, y), TileType.Floor);
        }
    }
}