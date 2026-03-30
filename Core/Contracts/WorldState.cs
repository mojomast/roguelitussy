using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class WorldState : IWorldState
{
    private TileType[] _grid = Array.Empty<TileType>();
    private bool[] _visible = Array.Empty<bool>();
    private bool[] _explored = Array.Empty<bool>();

    public int Width { get; private set; }
    public int Height { get; private set; }

    private readonly List<IEntity> _entities = new();
    private readonly Dictionary<EntityId, IEntity> _entityById = new();
    private readonly Dictionary<Position, IEntity> _blockingByPos = new();

    public IReadOnlyList<IEntity> Entities => _entities;
    public IEntity Player { get; set; } = null!;

    public int TurnNumber { get; set; }
    public int Depth { get; set; }
    public int Seed { get; set; }

    public void InitGrid(int width, int height)
    {
        Width = width;
        Height = height;
        int size = width * height;
        _grid = new TileType[size];
        _visible = new bool[size];
        _explored = new bool[size];
    }

    public TileType GetTile(Position pos) =>
        InBounds(pos) ? _grid[pos.Y * Width + pos.X] : TileType.Void;

    public void SetTile(Position pos, TileType type)
    {
        if (InBounds(pos))
            _grid[pos.Y * Width + pos.X] = type;
    }

    public bool InBounds(Position pos) =>
        pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

    public bool IsWalkable(Position pos) =>
        InBounds(pos) &&
        GetTile(pos) is TileType.Floor or TileType.Door or TileType.StairsDown
            or TileType.StairsUp or TileType.Water &&
        !_blockingByPos.ContainsKey(pos);

    public bool BlocksSight(Position pos) =>
        !InBounds(pos) || GetTile(pos) is TileType.Wall or TileType.Void;

    public void AddEntity(IEntity entity)
    {
        _entities.Add(entity);
        _entityById[entity.Id] = entity;
        if (entity.BlocksMovement)
            _blockingByPos[entity.Position] = entity;
    }

    public void RemoveEntity(EntityId id)
    {
        if (_entityById.TryGetValue(id, out var entity))
        {
            _entities.Remove(entity);
            _entityById.Remove(id);
            _blockingByPos.Remove(entity.Position);
        }
    }

    public void UpdateEntityPosition(EntityId id, Position oldPos, Position newPos)
    {
        if (_entityById.TryGetValue(id, out var entity))
        {
            if (entity.BlocksMovement)
            {
                _blockingByPos.Remove(oldPos);
                _blockingByPos[newPos] = entity;
            }
        }
    }

    public IEntity? GetEntity(EntityId id) =>
        _entityById.TryGetValue(id, out var e) ? e : null;

    public IEntity? GetEntityAt(Position pos) =>
        _blockingByPos.TryGetValue(pos, out var e) ? e : null;

    public bool IsVisible(Position pos) =>
        InBounds(pos) && _visible[pos.Y * Width + pos.X];

    public bool IsExplored(Position pos) =>
        InBounds(pos) && _explored[pos.Y * Width + pos.X];

    public void SetVisible(Position pos, bool visible)
    {
        if (!InBounds(pos)) return;
        int idx = pos.Y * Width + pos.X;
        _visible[idx] = visible;
        if (visible) _explored[idx] = true;
    }

    public void ClearVisibility()
    {
        Array.Clear(_visible, 0, _visible.Length);
    }

    public TileType[] GetRawGrid() => _grid;
    public bool[] GetRawExplored() => _explored;
}
