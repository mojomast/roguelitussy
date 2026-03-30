using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record RoomPrefab(string Id, IReadOnlyList<string> Rows)
{
    public int Width => Rows.Count == 0 ? 0 : Rows[0].Length;

    public int Height => Rows.Count;

    public bool FitsWithin(int width, int height) => Width <= width && Height <= height;

    public TileType GetTileType(int x, int y)
    {
        return Rows[y][x] switch
        {
            '.' => TileType.Floor,
            '+' => TileType.Door,
            '~' => TileType.Water,
            '^' => TileType.Lava,
            '#' => TileType.Wall,
            _ => TileType.Wall,
        };
    }

    public IReadOnlyList<Position> GetWalkableOffsets()
    {
        var offsets = new List<Position>();

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (GetTileType(x, y) != TileType.Wall)
                {
                    offsets.Add(new Position(x, y));
                }
            }
        }

        return offsets;
    }
}