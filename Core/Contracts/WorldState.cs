using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed class WorldState : IWorldState
{
    private TileType[] _grid = Array.Empty<TileType>();
    private bool[] _visible = Array.Empty<bool>();
    private bool[] _explored = Array.Empty<bool>();
    private readonly List<IEntity> _entities = new();
    private readonly Dictionary<EntityId, IEntity> _entityById = new();
    private readonly Dictionary<Position, IEntity> _entityByPosition = new();
    private readonly Dictionary<Position, List<ItemInstance>> _groundItems = new();
    private readonly HashSet<Position> _openDoors = new();
    private int _seed;
    private ulong _itemRandomState;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public IReadOnlyList<IEntity> Entities => _entities;

    public IEntity Player { get; set; } = null!;

    public int TurnNumber { get; set; }

    public int Depth { get; set; }

    public int Seed
    {
        get => _seed;
        set
        {
            _seed = value;
            CombatResolver = new CombatResolver(value);
            _itemRandomState = MixItemSeed(value);
        }
    }

    public CombatResolver? CombatResolver { get; set; }

    public ulong CombatRandomState
    {
        get => CombatResolver?.RandomState ?? 0UL;
        set => CombatResolver = value == 0UL ? new CombatResolver(Seed) : new CombatResolver(value);
    }

    public ulong ItemRandomState
    {
        get => _itemRandomState;
        set => _itemRandomState = value == 0UL ? MixItemSeed(Seed) : value;
    }

    internal void RehydrateRandomStates(int seed, ulong combatState, ulong itemState)
    {
        _seed = seed;
        CombatResolver = combatState == 0UL ? new CombatResolver(seed) : new CombatResolver(combatState);
        _itemRandomState = itemState == 0UL ? MixItemSeed(seed) : itemState;
    }

    public IContentDatabase? ContentDatabase { get; set; }

    public Dictionary<EntityId, int> SchedulerOrders { get; } = new();

    public int SchedulerNextOrder { get; set; }

    public EntityId AllocateItemInstanceId()
    {
        var first = NextItemRandom();
        var second = NextItemRandom();
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes[..8], first);
        BitConverter.TryWriteBytes(bytes[8..], second);
        return new EntityId(new Guid(bytes));
    }

    public void InitGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _grid = new TileType[width * height];
        _visible = new bool[width * height];
        _explored = new bool[width * height];
        _entities.Clear();
        _entityById.Clear();
        _entityByPosition.Clear();
        _groundItems.Clear();
        _openDoors.Clear();
        TurnNumber = 0;
    }

    public TileType GetTile(Position pos) => InBounds(pos) ? _grid[(pos.Y * Width) + pos.X] : TileType.Void;

    public void SetTile(Position pos, TileType type)
    {
        if (!InBounds(pos))
        {
            return;
        }

        _grid[(pos.Y * Width) + pos.X] = type;
        if (type is not TileType.Door and not TileType.LockedDoor)
        {
            _openDoors.Remove(pos);
        }
    }

    public bool InBounds(Position pos) => pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

    public bool IsWalkable(Position pos)
    {
        if (!InBounds(pos))
        {
            return false;
        }

        return IsTileWalkable(pos) && (!_entityByPosition.TryGetValue(pos, out var entity) || !entity.BlocksMovement);
    }

    public bool BlocksSight(Position pos)
    {
        if (!InBounds(pos))
        {
            return true;
        }

        var tile = GetTile(pos);
        if (tile is TileType.Void or TileType.Wall or TileType.LockedDoor)
        {
            return true;
        }

        if (tile == TileType.Door && !IsDoorOpen(pos))
        {
            return true;
        }

        return _entityByPosition.TryGetValue(pos, out var entity) && entity.BlocksSight;
    }

    public void AddEntity(IEntity entity)
    {
        if (_entityById.ContainsKey(entity.Id))
        {
            return;
        }

        if (!InBounds(entity.Position))
        {
            throw new ArgumentOutOfRangeException(nameof(entity), $"Entity position {entity.Position} is out of bounds.");
        }

        if (!IsTileWalkable(entity.Position))
        {
            throw new InvalidOperationException($"Tile {entity.Position} is not walkable.");
        }

        if (entity.BlocksMovement && _entityByPosition.ContainsKey(entity.Position))
        {
            throw new InvalidOperationException($"Tile {entity.Position} is already occupied.");
        }

        _entities.Add(entity);
        _entityById[entity.Id] = entity;

        if (entity.BlocksMovement)
        {
            _entityByPosition[entity.Position] = entity;
        }
    }

    public void RemoveEntity(EntityId id)
    {
        if (!_entityById.TryGetValue(id, out var entity))
        {
            return;
        }

        _entities.Remove(entity);
        _entityById.Remove(id);

        if (entity.BlocksMovement && _entityByPosition.TryGetValue(entity.Position, out var atPosition) && atPosition.Id == entity.Id)
        {
            _entityByPosition.Remove(entity.Position);
        }
    }

    public bool MoveEntity(EntityId id, Position newPosition)
    {
        if (!_entityById.TryGetValue(id, out var entity) || !InBounds(newPosition))
        {
            return false;
        }

        var oldPosition = entity.Position;
        if (oldPosition == newPosition)
        {
            return true;
        }

        if (_entityByPosition.TryGetValue(newPosition, out var occupant) && occupant.Id != id)
        {
            return false;
        }

        if (!IsTileWalkable(newPosition))
        {
            return false;
        }

        _entityByPosition.Remove(oldPosition);
        _entityByPosition[newPosition] = entity;
        entity.Position = newPosition;
        return true;
    }

    public bool TrySwapEntities(EntityId firstId, EntityId secondId)
    {
        if (firstId == secondId
            || !_entityById.TryGetValue(firstId, out var first)
            || !_entityById.TryGetValue(secondId, out var second))
        {
            return false;
        }

        var firstPosition = first.Position;
        var secondPosition = second.Position;
        if (firstPosition == secondPosition)
        {
            return true;
        }

        if (!IsTileWalkable(firstPosition) || !IsTileWalkable(secondPosition))
        {
            return false;
        }

        _entityByPosition[firstPosition] = second;
        _entityByPosition[secondPosition] = first;
        first.Position = secondPosition;
        second.Position = firstPosition;
        return true;
    }

    public void UpdateEntityPosition(EntityId id, Position oldPosition, Position newPosition)
    {
        if (!_entityById.TryGetValue(id, out var entity))
        {
            return;
        }

        if (oldPosition == newPosition || !entity.BlocksMovement)
        {
            return;
        }

        _entityByPosition.Remove(oldPosition);
        _entityByPosition[newPosition] = entity;
    }

    public IReadOnlyList<IEntity> GetEntitiesInRadius(Position center, int radius) =>
        _entities
            .Where(entity => center.ChebyshevTo(entity.Position) <= radius)
            .OrderBy(entity => center.ChebyshevTo(entity.Position))
            .ThenBy(entity => entity.Position.Y)
            .ThenBy(entity => entity.Position.X)
            .ToArray();

    public bool HasGroundItems(Position pos) =>
        _groundItems.TryGetValue(pos, out var items) && items.Count > 0;

    public bool IsDoorOpen(Position pos) => GetTile(pos) == TileType.Door && _openDoors.Contains(pos);

    public void SetDoorOpen(Position pos, bool isOpen)
    {
        if (!InBounds(pos) || GetTile(pos) != TileType.Door)
        {
            return;
        }

        if (isOpen)
        {
            _openDoors.Add(pos);
            return;
        }

        _openDoors.Remove(pos);
    }

    public IEntity? GetEntity(EntityId id) => _entityById.TryGetValue(id, out var entity) ? entity : null;

    public IEntity? GetEntityAt(Position pos) => _entityByPosition.TryGetValue(pos, out var entity) ? entity : null;

    public IReadOnlyList<ItemInstance> GetItemsAt(Position pos) =>
        _groundItems.TryGetValue(pos, out var items) ? items : Array.Empty<ItemInstance>();

    public void DropItem(Position pos, ItemInstance item)
    {
        if (!_groundItems.TryGetValue(pos, out var items))
        {
            items = new List<ItemInstance>();
            _groundItems[pos] = items;
        }

        items.Add(item);
    }

    public ItemInstance? PickupItem(Position pos, EntityId? instanceId = null)
    {
        if (!_groundItems.TryGetValue(pos, out var items) || items.Count == 0)
        {
            return null;
        }

        if (instanceId is null)
        {
            var first = items[0];
            items.RemoveAt(0);
            if (items.Count == 0)
            {
                _groundItems.Remove(pos);
            }

            return first;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].InstanceId == instanceId.Value)
            {
                var item = items[i];
                items.RemoveAt(i);
                if (items.Count == 0)
                {
                    _groundItems.Remove(pos);
                }

                return item;
            }
        }

        return null;
    }

    public IReadOnlyDictionary<Position, List<ItemInstance>> GetGroundItems() => _groundItems;

    private ulong NextItemRandom()
    {
        _itemRandomState += 0x9E3779B97F4A7C15UL;
        var value = _itemRandomState;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static ulong MixItemSeed(int seed)
    {
        unchecked
        {
            return ((ulong)(uint)seed << 32) ^ 0xD1B54A32D192ED03UL;
        }
    }

    public bool IsVisible(Position pos) => InBounds(pos) && _visible[(pos.Y * Width) + pos.X];

    public bool IsExplored(Position pos) => InBounds(pos) && _explored[(pos.Y * Width) + pos.X];

    public void SetVisible(Position pos, bool visible)
    {
        if (!InBounds(pos))
        {
            return;
        }

        var index = (pos.Y * Width) + pos.X;
        _visible[index] = visible;
        if (visible)
        {
            _explored[index] = true;
        }
    }

    public void ClearVisibility()
    {
        Array.Clear(_visible, 0, _visible.Length);
    }

    public TileType[] GetRawGrid() => _grid;

    public bool[] GetRawExplored() => _explored;

    private bool IsTileWalkable(Position pos)
    {
        var tile = GetTile(pos);
        return tile is TileType.Floor or TileType.StairsDown or TileType.StairsUp or TileType.Trap || (tile == TileType.Door && IsDoorOpen(pos));
    }
}
