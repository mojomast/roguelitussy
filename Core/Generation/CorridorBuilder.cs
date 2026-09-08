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

        var leftPoint = leftRoom.GetConnectionPointTowards(rightRoom.Room.Center);
        var rightPoint = rightRoom.GetConnectionPointTowards(leftRoom.Room.Center);
        ConnectVaried(world, leftPoint, rightPoint, rng);
    }

    public static void ConnectVaried(WorldState world, Position from, Position to, Random rng)
    {
        if (from.X == to.X || from.Y == to.Y || rng.Next(3) < 2)
        {
            Connect(world, from, to, rng);
            return;
        }

        // A midpoint dogleg keeps some corridors from sharing the same L silhouette.
        if (rng.Next(2) == 0)
        {
            var middleX = Math.Min(from.X, to.X) + (Math.Abs(to.X - from.X) / 2);
            CarveHorizontal(world, from.X, middleX, from.Y);
            CarveVertical(world, from.Y, to.Y, middleX);
            CarveHorizontal(world, middleX, to.X, to.Y);
        }
        else
        {
            var middleY = Math.Min(from.Y, to.Y) + (Math.Abs(to.Y - from.Y) / 2);
            CarveVertical(world, from.Y, middleY, from.X);
            CarveHorizontal(world, from.X, to.X, middleY);
            CarveVertical(world, middleY, to.Y, to.X);
        }
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
            CarveCorridorTile(world, new Position(x, y));
        }
    }

    private static void CarveVertical(WorldState world, int y1, int y2, int x)
    {
        var start = Math.Min(y1, y2);
        var end = Math.Max(y1, y2);
        for (var y = start; y <= end; y++)
        {
            CarveCorridorTile(world, new Position(x, y));
        }
    }

    private static void CarveCorridorTile(WorldState world, Position position)
    {
        if (world.GetTile(position) != TileType.Door)
        {
            world.SetTile(position, TileType.Floor);
        }
    }
}
