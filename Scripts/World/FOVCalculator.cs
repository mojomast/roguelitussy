using System;
using System.Collections.Generic;
using Roguelike.Core;

namespace Godotussy;

public sealed class FOVCalculator : IFOV
{
    private static readonly int[,] OctantMultipliers =
    {
        { 1, 0, 0, -1, -1, 0, 0, 1 },
        { 0, 1, -1, 0, 0, -1, 1, 0 },
        { 0, 1, 1, 0, 0, -1, -1, 0 },
        { 1, 0, 0, 1, -1, 0, 0, -1 },
    };

    public void Compute(Position origin, int radius, Func<Position, bool> blocksLight, Action<Position> markVisible)
    {
        foreach (var position in ComputeVisible(origin, radius, blocksLight))
        {
            markVisible(position);
        }
    }

    public HashSet<Position> ComputeVisible(Position origin, int radius, Func<Position, bool> blocksLight)
    {
        var visible = new HashSet<Position>();
        if (radius < 0)
        {
            return visible;
        }

        visible.Add(origin);

        for (var octant = 0; octant < 8; octant++)
        {
            CastLight(
                origin,
                row: 1,
                startSlope: 1.0,
                endSlope: 0.0,
                radius,
                OctantMultipliers[0, octant],
                OctantMultipliers[1, octant],
                OctantMultipliers[2, octant],
                OctantMultipliers[3, octant],
                blocksLight,
                visible);
        }

        return visible;
    }

    private static void CastLight(
        Position origin,
        int row,
        double startSlope,
        double endSlope,
        int radius,
        int xx,
        int xy,
        int yx,
        int yy,
        Func<Position, bool> blocksLight,
        ISet<Position> visible)
    {
        if (startSlope < endSlope)
        {
            return;
        }

        var radiusSquared = radius * radius;
        for (var distance = row; distance <= radius; distance++)
        {
            var blocked = false;
            var nextStartSlope = startSlope;

            var deltaY = -distance;
            for (var deltaX = -distance; deltaX <= 0; deltaX++)
            {
                var leftSlope = (deltaX - 0.5) / (deltaY + 0.5);
                var rightSlope = (deltaX + 0.5) / (deltaY - 0.5);

                if (startSlope < rightSlope)
                {
                    continue;
                }

                if (endSlope > leftSlope)
                {
                    break;
                }

                var currentX = origin.X + (deltaX * xx) + (deltaY * xy);
                var currentY = origin.Y + (deltaX * yx) + (deltaY * yy);
                var position = new Position(currentX, currentY);
                var opaque = blocksLight(position);

                if ((deltaX * deltaX) + (deltaY * deltaY) <= radiusSquared
                    && (!IsCornerOccluded(origin, position, blocksLight) || opaque))
                {
                    visible.Add(position);
                }

                if (blocked)
                {
                    if (opaque)
                    {
                        nextStartSlope = rightSlope;
                        continue;
                    }

                    blocked = false;
                    startSlope = nextStartSlope;
                }
                else if (opaque && distance < radius)
                {
                    blocked = true;
                    CastLight(origin, distance + 1, startSlope, leftSlope, radius, xx, xy, yx, yy, blocksLight, visible);
                    nextStartSlope = rightSlope;
                }
            }

            if (blocked)
            {
                break;
            }
        }
    }

    private static bool IsCornerOccluded(Position origin, Position target, Func<Position, bool> blocksLight)
    {
        var stepX = Math.Sign(target.X - origin.X);
        var stepY = Math.Sign(target.Y - origin.Y);
        if (stepX == 0 || stepY == 0)
        {
            return false;
        }

        var horizontalNeighbor = new Position(target.X - stepX, target.Y);
        var verticalNeighbor = new Position(target.X, target.Y - stepY);
        return blocksLight(horizontalNeighbor) || blocksLight(verticalNeighbor);
    }
}