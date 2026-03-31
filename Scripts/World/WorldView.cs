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
    public const int TileSize = 40;
    private const float SourceArtTileSize = 16f;

    private TileMapLayer _floorLayer = new() { Name = "TileMapLayer_Floor" };
    private TileMapLayer _wallLayer = new() { Name = "TileMapLayer_Walls" };
    private TileMapLayer _objectLayer = new() { Name = "TileMapLayer_Objects" };
    private TileMapLayer _fogLayer = new() { Name = "TileMapLayer_Fog" };
    private Node2D _tileArtLayer = new() { Name = "TileArtLayer" };
    private Node2D _entityLayer = new() { Name = "EntityLayer" };
    private Camera2D _camera = new() { Name = "Camera2D" };
    private readonly FOVCalculator _fov = new();
    private readonly CameraController _cameraController = new();
    private readonly AnimationController _animationController = new();
    private readonly HashSet<Position> _visibleTiles = new();
    private readonly HashSet<Position> _exploredTiles = new();
    private readonly Dictionary<EntityId, Position> _entitySnapshot = new();
    private readonly Dictionary<Position, Node> _tileArt = new();
    private readonly Dictionary<Position, string> _tileMarkers = new();
    private readonly EntityRenderer _entityRenderer;

    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private IWorldState? _world;

    public WorldView()
    {
        _entityRenderer = new EntityRenderer(_entityLayer, _animationController);
        _cameraController.Bind(_camera);
        HideLegacyTilemapVisuals();
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
        _tileArtLayer = GetNodeOrNull<Node2D>("TileArtLayer") ?? _tileArtLayer;
        _entityLayer = GetNode<Node2D>("EntityLayer");
        _camera = GetNode<Camera2D>("Camera2D");

        _entityRenderer.BindLayer(_entityLayer);
        _cameraController.Bind(_camera);
        HideLegacyTilemapVisuals();

        _gameManager = GetNodeOrNull<GameManager>("/root/GameManager");
        BindEventBus(GetNodeOrNull<EventBus>("/root/EventBus"));

        if (_gameManager?.World is not null)
        {
            BindWorld(_gameManager.World);
        }
    }

    public override void _Process(double delta)
    {
        _animationController.Advance(delta);
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
            _eventBus.FovRecalculated -= OnFovRecalculated;
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
        _eventBus.FovRecalculated += OnFovRecalculated;
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
        ClearTileArt();

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

        if (_world is WorldState mutableWorld)
        {
            mutableWorld.ClearVisibility();
        }

        _visibleTiles.Clear();
        var player = _world.Player;
        if (player is null)
        {
            SyncVisibilityFromWorld();
            return;
        }

        _fov.Compute(
            player.Position,
            player.Stats.ViewRadius,
            pos => !_world.InBounds(pos) || _world.BlocksSight(pos),
            pos =>
            {
                if (_world.InBounds(pos))
                {
                    if (_world is WorldState mutable)
                    {
                        mutable.SetVisible(pos, true);
                    }

                    _visibleTiles.Add(pos);
                    _exploredTiles.Add(pos);
                }
            });

        _visibleTiles.Add(player.Position);
        if (_world is WorldState world)
        {
            world.SetVisible(player.Position, true);
        }

        SyncVisibilityFromWorld();
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
        var isDoorOpen = tileType == TileType.Door && _world is WorldState world && world.IsDoorOpen(position);
        RenderTileArt(position, tileType);
        switch (tileType)
        {
            case TileType.Floor:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(0, 0));
                _wallLayer.EraseCell(cellPosition);
                break;
            case TileType.StairsDown:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(2, 0));
                _wallLayer.EraseCell(cellPosition);
                break;
            case TileType.StairsUp:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(3, 0));
                _wallLayer.EraseCell(cellPosition);
                break;
            case TileType.Water:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(6, 0));
                _wallLayer.EraseCell(cellPosition);
                break;
            case TileType.Lava:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(7, 0));
                _wallLayer.EraseCell(cellPosition);
                break;
            case TileType.Wall:
                _floorLayer.EraseCell(cellPosition);
                _wallLayer.SetCell(cellPosition, 0, new Vector2I(1, 0));
                break;
            case TileType.Door:
                _floorLayer.SetCell(cellPosition, 0, new Vector2I(0, 0));
                if (isDoorOpen)
                {
                    _wallLayer.EraseCell(cellPosition);
                }
                else
                {
                    _wallLayer.SetCell(cellPosition, 0, new Vector2I(4, 0));
                }
                break;
        }
    }

    private void ClearTileArt()
    {
        foreach (var tile in _tileArt.Values)
        {
            _tileArtLayer.RemoveChild(tile);
            tile.QueueFree();
        }

        _tileArt.Clear();
        _tileMarkers.Clear();
    }

    private void RenderTileArt(Position position, TileType tileType)
    {
        var isDoorOpen = tileType == TileType.Door && _world is WorldState world && world.IsDoorOpen(position);
        var texture = WorldArtCatalog.GetTileTexture(tileType, isDoorOpen);
        var marker = WorldArtCatalog.GetTileMarker(tileType, isDoorOpen);
        var container = new Node2D
        {
            Name = $"Tile_{position.X}_{position.Y}",
            Position = new Vector2(position.X * TileSize, position.Y * TileSize),
        };

        if (texture is not null)
        {
            var sprite = new Sprite2D
            {
                Name = "Texture",
                Position = new Vector2(TileSize * 0.5f, TileSize * 0.5f),
                Scale = ResolveTextureScale(),
                Texture = texture,
            };
            container.AddChild(sprite);
        }
        else
        {
            var tile = new ColorRect
            {
                Name = "Fallback",
                Position = Vector2.Zero,
                Size = new Vector2(TileSize, TileSize),
                Color = ResolveTileColor(tileType),
            };
            container.AddChild(tile);
        }

        if (!string.IsNullOrWhiteSpace(marker))
        {
            var markerLabel = new Label
            {
                Name = "Marker",
                Position = new Vector2(4f, 9f),
                Size = new Vector2(TileSize - 8f, TileSize - 12f),
                Text = marker,
            };
            container.AddChild(markerLabel);
            _tileMarkers[position] = marker;
        }

        _tileArtLayer.AddChild(container);
        _tileArt[position] = container;
    }

    private void HideLegacyTilemapVisuals()
    {
        _floorLayer.Visible = false;
        _wallLayer.Visible = false;
        _objectLayer.Visible = false;
    }

    private static Vector2 ResolveTextureScale()
    {
        var scale = TileSize / SourceArtTileSize;
        return new Vector2(scale, scale);
    }

    private static Color ResolveTileColor(TileType tileType)
    {
        return tileType switch
        {
            TileType.Floor => new Color(0.18f, 0.18f, 0.2f, 1f),
            TileType.Wall => new Color(0.07f, 0.07f, 0.09f, 1f),
            TileType.Door => new Color(0.58f, 0.36f, 0.12f, 1f),
            TileType.StairsDown => new Color(0.2f, 0.42f, 0.75f, 1f),
            TileType.StairsUp => new Color(0.32f, 0.58f, 0.28f, 1f),
            TileType.Water => new Color(0.12f, 0.24f, 0.55f, 1f),
            TileType.Lava => new Color(0.7f, 0.24f, 0.08f, 1f),
            _ => new Color(0f, 0f, 0f, 1f),
        };
    }

    public string GetTileMarkerText(Position position)
    {
        return _tileMarkers.TryGetValue(position, out var marker) ? marker : string.Empty;
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
        ClearTileArt();
        for (var y = 0; y < _world.Height; y++)
        {
            for (var x = 0; x < _world.Width; x++)
            {
                RenderTile(new Position(x, y), _world.GetTile(new Position(x, y)));
            }
        }

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

    private void OnFovRecalculated()
    {
        SyncVisibilityFromWorld();
    }

    private void SyncVisibilityFromWorld()
    {
        if (_world is null)
        {
            return;
        }

        _visibleTiles.Clear();
        _exploredTiles.Clear();

        for (var y = 0; y < _world.Height; y++)
        {
            for (var x = 0; x < _world.Width; x++)
            {
                var position = new Position(x, y);
                if (_world.IsVisible(position))
                {
                    _visibleTiles.Add(position);
                }

                if (_world.IsExplored(position))
                {
                    _exploredTiles.Add(position);
                }
            }
        }

        UpdateFogLayer();
        _entityRenderer.UpdateVisibility(_visibleTiles);

        if (_world.Player is not null)
        {
            _cameraController.CenterOn(_world.Player.Position, TileSize);
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
        if (defenderPosition != Roguelike.Core.Position.Invalid)
        {
            _animationController.SpawnDamagePopup(
                _entityLayer,
                ToCanvasPosition(defenderPosition),
                damage.FinalDamage,
                damage.IsCritical,
                isHeal: false,
                damage.IsMiss);
        }

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