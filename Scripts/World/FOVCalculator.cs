using System;
using Roguelike.Core;

namespace Roguelike.Godot;

/// <summary>
/// Symmetric shadowcasting FOV across 8 octants.
/// Pure math — no Godot dependency.
/// </summary>
public sealed class FOVCalculator : IFOV
{
    public void Compute(
        Position origin,
        int radius,
        Func<Position, bool> blocksLight,
        Action<Position> markVisible)
    {
        markVisible(origin);

        for (int octant = 0; octant < 8; octant++)
        {
            CastOctant(origin, radius, octant, blocksLight, markVisible);
        }
    }

    private static void CastOctant(
        Position origin,
        int radius,
        int octant,
        Func<Position, bool> blocksLight,
        Action<Position> markVisible)
    {
        var shadows = new ShadowLine();

        for (int row = 1; row <= radius; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                var (dx, dy) = TransformOctant(row, col, octant);
                var pos = new Position(origin.X + dx, origin.Y + dy);

                if (origin.ChebyshevTo(pos) > radius)
                    continue;

                float projStart = (col - 0.5f) / row;
                float projEnd = (col + 0.5f) / row;
                var projection = new Shadow(projStart, projEnd);

                if (shadows.IsFullShadow)
                    continue;

                if (shadows.IsInShadow(projection))
                    continue;

                markVisible(pos);

                if (blocksLight(pos))
                {
                    shadows.Add(projection);
                }
            }
        }
    }

    private static (int dx, int dy) TransformOctant(int row, int col, int octant)
    {
        return octant switch
        {
            0 => (col, -row),
            1 => (row, -col),
            2 => (row, col),
            3 => (col, row),
            4 => (-col, row),
            5 => (-row, col),
            6 => (-row, -col),
            7 => (-col, -row),
            _ => (0, 0),
        };
    }

    private struct Shadow
    {
        public float Start;
        public float End;

        public Shadow(float start, float end)
        {
            Start = start;
            End = end;
        }

        public readonly bool Contains(Shadow other) =>
            Start <= other.Start && End >= other.End;
    }

    private sealed class ShadowLine
    {
        private readonly System.Collections.Generic.List<Shadow> _shadows = new();

        public bool IsFullShadow =>
            _shadows.Count == 1 && _shadows[0].Start <= 0 && _shadows[0].End >= 1;

        public bool IsInShadow(Shadow projection)
        {
            foreach (var shadow in _shadows)
            {
                if (shadow.Contains(projection))
                    return true;
            }
            return false;
        }

        public void Add(Shadow shadow)
        {
            int index = 0;
            for (; index < _shadows.Count; index++)
            {
                if (_shadows[index].Start >= shadow.Start)
                    break;
            }

            // Check overlap with previous
            var overlappingPrev = index > 0 && _shadows[index - 1].End > shadow.Start;
            // Check overlap with next
            var overlappingNext = index < _shadows.Count && _shadows[index].Start < shadow.End;

            if (overlappingNext)
            {
                if (overlappingPrev)
                {
                    // Merge previous and next
                    var prev = _shadows[index - 1];
                    prev.End = Math.Max(_shadows[index].End, shadow.End);
                    _shadows[index - 1] = prev;
                    _shadows.RemoveAt(index);
                }
                else
                {
                    // Extend current
                    var current = _shadows[index];
                    current.Start = Math.Min(current.Start, shadow.Start);
                    current.End = Math.Max(current.End, shadow.End);
                    _shadows[index] = current;
                }
            }
            else if (overlappingPrev)
            {
                // Extend previous
                var prev = _shadows[index - 1];
                prev.End = Math.Max(prev.End, shadow.End);
                _shadows[index - 1] = prev;
            }
            else
            {
                _shadows.Insert(index, shadow);
            }
        }
    }
}
