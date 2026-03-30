using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class BSPNode
{
    public BSPNode(int x, int y, int width, int height, int depth = 0)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Depth = depth;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public BSPNode? Left { get; private set; }

    public BSPNode? Right { get; private set; }

    public bool IsLeaf => Left is null && Right is null;

    internal RoomPlacement? Room { get; set; }

    public static BSPNode Create(int mapWidth, int mapHeight, Random rng, int minLeafSize = 12, int maxDepth = 5)
    {
        if (mapWidth < (minLeafSize * 2) + 2)
        {
            throw new ArgumentOutOfRangeException(nameof(mapWidth), "Map width is too small for BSP generation.");
        }

        if (mapHeight < (minLeafSize * 2) + 2)
        {
            throw new ArgumentOutOfRangeException(nameof(mapHeight), "Map height is too small for BSP generation.");
        }

        var root = new BSPNode(1, 1, mapWidth - 2, mapHeight - 2);
        Split(root, rng, minLeafSize, maxDepth);
        return root;
    }

    public IEnumerable<BSPNode> Leaves()
    {
        if (IsLeaf)
        {
            yield return this;
            yield break;
        }

        if (Left is not null)
        {
            foreach (var leaf in Left.Leaves())
            {
                yield return leaf;
            }
        }

        if (Right is not null)
        {
            foreach (var leaf in Right.Leaves())
            {
                yield return leaf;
            }
        }
    }

    internal RoomPlacement? PickRoom(Random rng)
    {
        if (IsLeaf)
        {
            return Room;
        }

        if (Left is null)
        {
            return Right?.PickRoom(rng);
        }

        if (Right is null)
        {
            return Left.PickRoom(rng);
        }

        return rng.Next(2) == 0 ? Left.PickRoom(rng) : Right.PickRoom(rng);
    }

    private static void Split(BSPNode node, Random rng, int minLeafSize, int maxDepth)
    {
        if (node.Depth >= maxDepth)
        {
            return;
        }

        var canSplitHorizontally = node.Height >= minLeafSize * 2;
        var canSplitVertically = node.Width >= minLeafSize * 2;
        if (!canSplitHorizontally && !canSplitVertically)
        {
            return;
        }

        bool splitHorizontal;
        if (canSplitHorizontally && !canSplitVertically)
        {
            splitHorizontal = true;
        }
        else if (!canSplitHorizontally && canSplitVertically)
        {
            splitHorizontal = false;
        }
        else if (node.Height > node.Width)
        {
            splitHorizontal = true;
        }
        else if (node.Width > node.Height)
        {
            splitHorizontal = false;
        }
        else
        {
            splitHorizontal = rng.Next(2) == 0;
        }

        if (splitHorizontal)
        {
            var minSplit = minLeafSize;
            var maxSplit = node.Height - minLeafSize;
            if (maxSplit < minSplit)
            {
                return;
            }

            var split = rng.Next(minSplit, maxSplit + 1);
            node.Left = new BSPNode(node.X, node.Y, node.Width, split, node.Depth + 1);
            node.Right = new BSPNode(node.X, node.Y + split, node.Width, node.Height - split, node.Depth + 1);
        }
        else
        {
            var minSplit = minLeafSize;
            var maxSplit = node.Width - minLeafSize;
            if (maxSplit < minSplit)
            {
                return;
            }

            var split = rng.Next(minSplit, maxSplit + 1);
            node.Left = new BSPNode(node.X, node.Y, split, node.Height, node.Depth + 1);
            node.Right = new BSPNode(node.X + split, node.Y, node.Width - split, node.Height, node.Depth + 1);
        }

        Split(node.Left!, rng, minLeafSize, maxDepth);
        Split(node.Right!, rng, minLeafSize, maxDepth);
    }
}