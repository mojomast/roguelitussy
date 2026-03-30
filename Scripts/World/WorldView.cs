using System.Collections.Generic;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public enum FogTileState
{
    Visible,
    Explored,
    Hidden,
}

public partial class WorldView : Node2D
{
    public const int TileSize = 16;

    private TileMapLayer _floorLayer = new() { Name = "TileMapLayer_Floor" };
    private TileMapLayer _wallLayer = new() { Name = "TileMapLayer_Walls" };
    private TileMapLayer _objectLayer = new() { Name = "TileMapLayer_Objects" };
    private TileMapLayer _fogLayer = new() { Name = "TileMapLayer_Fog" };
    private Node2D _entityLayer = new() { Name = "EntityLayer" };
    private Camera2D _camera = new() { Name = "Camera2D" };
    private readonly FOVCalculator _fov = new();
    private readonly CameraController _cameraController = new();
    private readonly AnimationController _animationController = new();
    private readonly HashSet<Position> _visibleTiles = new();
    private readonly HashSet<Position> _exploredTiles = new();
    private readonly Dictionary<EntityId, Position> _entitySnapshot = new();
    private readonly EntityRenderer _entityRenderer;

    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private IWorldState? _world;

    public WorldView()
    {
        _entityRenderer = new EntityRenderer(_entityLayer, _animationController);
        _cameraController.Bind(_camera);
    }

    public TileMapLayer FloorLayer => _floorLayer;

    public TileMapLayer WallLayer => _wallLayer;

    public TileMapLayer ObjectLayer => _objectLayer;

    public TileMapLayer FogLayer => _fogLayer;

    public Node2D EntityLayerNode => _entityLayer;

    public Camera2D Camera => _camera;

    public EntityRenderer EntityRenderer => _entityRenderer;

    public AnimationController Animations => _animationController;

    public IReadOnlyCollection<Position> VisibleTiles => _visibleTiles;

    public IReadOnlyCollection<Position> ExploredTiles => _exploredTiles;

    public override void _Ready()
    {
        _floorLayer = GetNode<TileMapLayer>("TileMapLayer_Floor");
        _wallLayer = GetNode<TileMapLayer>("TileMapLayer_Walls");
        _objectLayer = GetNode<TileMapLayer>("TileMapLayer_Objects");
        _fogLayer = GetNode<TileMapLayer>("TileMapLayer_Fog");
        _entityLayer = GetNode<Node2D>("EntityLayer");
        _camera = GetNode<Camera2D>("Camera2D");

        _entityRenderer.BindLayer(_entityLayer);
        _cameraController.Bind(_camera);

        _gameManager = GetNodeOrNull<GameManager>("/root/GameManager");
        BindEventBus(GetNodeOrNull<EventBus>("/root/EventBus"));

        if (_gameManager?.World is not null)
        {
            BindWorld(_gameManager.World);
        }
    }

    public void BindWorld(IWorldState world)
    {
        _world = world;
        RenderFullMap(world);
    }

    public void BindEventBus(EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.FloorChanged -= OnFloorChanged;
            _eventBus.DamageDealt -= OnDamageDealt;
            _eventBus.EntityMoved -= OnEntityMoved;
            _eventBus.EntitySpawned -= OnEntitySpawned;
            _eventBus.EntityRemoved -= OnEntityRemoved;
        }

        _eventBus = eventBus;
        if (_eventBus is null)
        {
            return;
        }

        _eventBus.TurnCompleted += OnTurnCompleted;
        _eventBus.FloorChanged += OnFloorChanged;
        _eventBus.DamageDealt += OnDamageDealt;
        _eventBus.EntityMoved += OnEntityMoved;
        _eventBus.EntitySpawned += OnEntitySpawned;
        _eventBus.EntityRemoved += OnEntityRemoved;
    }

    public void RenderFullMap(IWorldState world)
    {
        _world = world;

        _floorLayer.Clear();
        _wallLayer.Clear();
        _objectLayer.Clear();
        _fogLayer.Clear();
        _visibleTiles.Clear();
        _exploredTiles.Clear();

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                RenderTile(new Position(x, y), world.GetTile(new Position(x, y)));
            }
        }

        _entityRenderer.BindWorld(world);
        SnapshotEntities();
        RecalculateFov();
    }

    public void RecalculateFov()
    {
        if (_world is null)
        {
            return;
        }

        _visibleTiles.Clear();
        var player = _world.Player;
        _fov.Compute(
            player.Position,
            player.Stats.ViewRadius,
            pos => !_world.InBounds(pos) || _world.BlocksSight(pos),
            pos =>
            {
                if (_world.InBounds(pos))
                {
                    _visibleTiles.Add(pos);
                    _exploredTiles.Add(pos);
                }
            });

        _visibleTiles.Add(player.Position);
        _exploredTiles.Add(player.Position);
        UpdateFogLayer();
        _entityRenderer.UpdateVisibility(_visibleTiles);
        _cameraController.CenterOn(player.Position, TileSize);
    }

    public FogTileState GetFogState(Position position)
    {
        if (_visibleTiles.Contains(position))
        {
            return FogTileState.Visible;
        }

        return _exploredTiles.Contains(position) ? FogTileState.Explored : FogTileState.Hidden;
    }

    public bool IsEntityVisible(EntityId entityId)
    {
        return _entityRenderer.GetSprite(entityId)?.Visible ?? false;
    }

    public static Vector2 ToCanvasPosition(Position position)
    {
        return CameraController.CenterOf(position, TileSize);
    }

    private void RenderTile(Position position, TileType tileType)
    {
        var cellPosition = new Vector2I(position.X, position.Y);
        switch (tileType)
        {
            case TileType.Floor:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(0, 0));
                break;
            case TileType.StairsDown:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(2, 0));
                break;
            case TileType.StairsUp:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(3, 0));
                break;
            case TileType.Water:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(6, 0));
                break;
            case TileType.Lava:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(7, 0));
                break;
            case TileType.Wall:
                _wallLayer.SetCell(cellPosition, 0, new Vector2I(1, 0));
                break;
            case TileType.Door:
                _wallLayer.SetCell(cellPosition, 0, new Vector2I(4, 0));
                break;
        }
    }

    private void UpdateFogLayer()
    {
        if (_world is null)
        {
            return;
        }

        for (var y = 0; y < _world.Height; y++)
        {
            for (var x = 0; x < _world.Width; x++)
            {
                var position = new Position(x, y);
                var cellPosition = new Vector2I(x, y);

                if (_visibleTiles.Contains(position))
                {
                    _fogLayer.EraseCell(cellPosition);
                }
                else if (_exploredTiles.Contains(position))
                {
                    _fogLayer.SetCell(cellPosition, 0, new Vector2I(9, 0));
                }
                else
                {
                    _fogLayer.SetCell(cellPosition, 0, new Vector2I(8, 0));
                }
            }
        }
    }

    private void OnTurnCompleted()
    {
        if (_world is null)
        {
            return;
        }

        AnimateEntityMovesFromSnapshot();
        _entityRenderer.SyncEntities(_world.Entities);
        RecalculateFov();
        SnapshotEntities();
    }

    private void OnFloorChanged(int depth)
    {
        if (_gameManager?.World is not null)
        {
            BindWorld(_gameManager.World);
        }
    }

    private void OnDamageDealt(DamageResult damage)
    {
        var attacker = _world?.GetEntity(damage.AttackerId);
        var defender = _world?.GetEntity(damage.DefenderId);

        Roguelike.Core.Position attackerPosition = attacker?.Position ?? _entitySnapshot.GetValueOrDefault(damage.AttackerId, Roguelike.Core.Position.Invalid);
        Roguelike.Core.Position defenderPosition = defender?.Position ?? _entityRenderer.GetLastKnownPosition(damage.DefenderId) ?? Roguelike.Core.Position.Invalid;

        if (attackerPosition != Roguelike.Core.Position.Invalid && defenderPosition != Roguelike.Core.Position.Invalid)
        {
            _entityRenderer.AnimateAttack(damage.AttackerId, defenderPosition);
        }

        _entityRenderer.AnimateDamage(damage.DefenderId);
        if (damage.IsKill)
        {
            _entityRenderer.RemoveEntity(damage.DefenderId, animateDeath: true);
        }
    }

    private void OnEntityMoved(EntityId entityId, Position from, Position to)
    {
        _entityRenderer.AnimateMove(entityId, to);
        _entitySnapshot[entityId] = to;
    }

    private void OnEntitySpawned(IEntity entity)
    {
        _entityRenderer.UpsertEntity(entity);
        _entitySnapshot[entity.Id] = entity.Position;
    }

    private void OnEntityRemoved(EntityId entityId)
    {
        _entityRenderer.RemoveEntity(entityId, animateDeath: true);
        _entitySnapshot.Remove(entityId);
    }

    private void AnimateEntityMovesFromSnapshot()
    {
        if (_world is null)
        {
            return;
        }

        foreach (var entity in _world.Entities)
        {
            if (_entitySnapshot.TryGetValue(entity.Id, out var previousPosition) && previousPosition != entity.Position)
            {
                _entityRenderer.AnimateMove(entity.Id, entity.Position);
            }
        }
    }

    private void SnapshotEntities()
    {
        _entitySnapshot.Clear();
        if (_world is null)
        {
            return;
        }

        foreach (var entity in _world.Entities)
        {
            _entitySnapshot[entity.Id] = entity.Position;
        }
    }
}