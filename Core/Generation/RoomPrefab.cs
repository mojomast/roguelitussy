using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record RoomPrefabSpawnPoint(int X, int Y, string Type, string? ReferenceId = null);

public sealed record RoomPrefabFixedEntity(int X, int Y, string? EntityType = null, string? TemplateId = null);

public sealed record RoomPrefabSpawnPlacement(Position Position, RoomPrefabSpawnPoint SpawnPoint);

public sealed record RoomPrefabFixedEntityPlacement(Position Position, RoomPrefabFixedEntity FixedEntity);

public sealed record RoomPrefab(
    string Id,
    IReadOnlyList<string> Rows,
    IReadOnlyList<RoomPrefabSpawnPoint>? DefinedSpawnPoints = null,
    IReadOnlyList<RoomPrefabFixedEntity>? DefinedFixedEntities = null,
    int? ItemQualityBonus = null,
    int? EnemyCountBonus = null,
    bool LockDoorsOnEnter = false,
    IReadOnlyList<string>? DefinedTags = null)
{
    public IReadOnlyList<RoomPrefabSpawnPoint> SpawnPoints { get; } = DefinedSpawnPoints ?? Array.Empty<RoomPrefabSpawnPoint>();

    public IReadOnlyList<RoomPrefabFixedEntity> FixedEntities { get; } = DefinedFixedEntities ?? Array.Empty<RoomPrefabFixedEntity>();

    public IReadOnlyList<string> Tags { get; } = DefinedTags ?? Array.Empty<string>();

    public bool HasWalkableTiles => GetWalkableOffsets().Count > 0;

    public int Width => Rows.Count == 0 ? 0 : Rows[0].Length;

    public int Height => Rows.Count;

    public bool FitsWithin(int width, int height) => Width <= width && Height <= height;

    public TileType GetTileType(int x, int y)
    {
        // Guard against ragged layout rows so a short row reads as wall instead of throwing.
        if (y < 0 || y >= Rows.Count)
        {
            return TileType.Wall;
        }

        var row = Rows[y];
        if (x < 0 || x >= row.Length)
        {
            return TileType.Wall;
        }

        return row[x] switch
        {
            '.' or 'P' or 'S' or 'I' or 'C' or '<' or '>' => TileType.Floor,
            '^' => TileType.Trap,
            '+' => TileType.Door,
            '~' => TileType.Water,
            '#' => TileType.Wall,
            _ => TileType.Wall,
        };
    }

    public bool IsDoor(int x, int y) =>
        y >= 0 && y < Rows.Count && x >= 0 && x < Rows[y].Length && Rows[y][x] == '+';

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
