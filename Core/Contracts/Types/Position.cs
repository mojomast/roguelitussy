using System;

namespace Roguelike.Core;

public readonly struct Position : IEquatable<Position>
{
    public readonly int X;
    public readonly int Y;

    public Position(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int DistanceTo(Position other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public int ChebyshevTo(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public Position Offset(int dx, int dy) => new(X + dx, Y + dy);

    public static Position operator +(Position a, Position b) => new(a.X + b.X, a.Y + b.Y);
    public static Position operator -(Position a, Position b) => new(a.X - b.X, a.Y - b.Y);
    public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Position a, Position b) => !(a == b);

    public bool Equals(Position other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Position p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X},{Y})";

    public static readonly Position Zero = new(0, 0);
    public static readonly Position Invalid = new(-1, -1);

    public static readonly Position[] Cardinals =
    {
        new(0, -1), new(0, 1), new(1, 0), new(-1, 0)
    };

    public static readonly Position[] AllDirections =
    {
        new(0, -1), new(0, 1), new(1, 0), new(-1, 0),
        new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)
    };
}
