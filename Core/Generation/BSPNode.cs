using System;

namespace Roguelike.Core.Generation;

public sealed class BSPNode
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }

    public BSPNode? Left { get; private set; }
    public BSPNode? Right { get; private set; }
    public RoomData? Room { get; set; }

    public bool IsLeaf => Left is null && Right is null;

    private const int MinPartitionSize = 8;

    public BSPNode(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool Split(Random rng)
    {
        if (!IsLeaf) return false;

        bool splitHorizontal;
        if (Width > Height && Width / (double)Height >= 1.25)
            splitHorizontal = false;
        else if (Height > Width && Height / (double)Width >= 1.25)
            splitHorizontal = true;
        else
            splitHorizontal = rng.Next(2) == 0;

        int max = (splitHorizontal ? Height : Width) - MinPartitionSize;
        if (max < MinPartitionSize) return false;

        int splitPos = (int)(((splitHorizontal ? Height : Width) * (0.4 + rng.NextDouble() * 0.2)));
        if (splitPos < MinPartitionSize) splitPos = MinPartitionSize;
        if ((splitHorizontal ? Height : Width) - splitPos < MinPartitionSize)
            return false;

        if (splitHorizontal)
        {
            Left = new BSPNode(X, Y, Width, splitPos);
            Right = new BSPNode(X, Y + splitPos, Width, Height - splitPos);
        }
        else
        {
            Left = new BSPNode(X, Y, splitPos, Height);
            Right = new BSPNode(X + splitPos, Y, Width - splitPos, Height);
        }

        return true;
    }

    public RoomData? GetRoom()
    {
        if (Room is not null) return Room;
        if (Left is not null)
        {
            var leftRoom = Left.GetRoom();
            if (leftRoom is not null) return leftRoom;
        }
        if (Right is not null)
        {
            var rightRoom = Right.GetRoom();
            if (rightRoom is not null) return rightRoom;
        }
        return null;
    }
}
