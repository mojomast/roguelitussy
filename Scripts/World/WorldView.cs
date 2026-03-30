using Godot;
using Roguelike.Core;

namespace Roguelike.Godot;

public partial class WorldView : Node2D
{
    private const int TileSize = 16;

    // Atlas source IDs — tiles will be set by atlas coords.
    // These use flat colored cells as placeholders until real tilesets are added.
    private const int SourceId = 0;

    [Export] public NodePath FloorLayerPath { get; set; } = "FloorLayer";
    [Export] public NodePath WallLayerPath { get; set; } = "WallLayer";
    [Export] public NodePath ObjectLayerPath { get; set; } = "ObjectLayer";
    [Export] public NodePath FogLayerPath { get; set; } = "FogLayer";
    [Export] public NodePath EntityLayerPath { get; set; } = "EntityLayer";
    [Export] public NodePath CameraPath { get; set; } = "Camera";

    private TileMapLayer _floorLayer = null!;
    private TileMapLayer _wallLayer = null!;
    private TileMapLayer _objectLayer = null!;
    private TileMapLayer _fogLayer = null!;
    private EntityRenderer _entityRenderer = null!;
    private CameraController _camera = null!;
    private AnimationController _animController = null!;
    private FOVCalculator _fov = null!;

    private WorldState? _world;

    public override void _Ready()
    {
        _floorLayer = GetNode<TileMapLayer>(FloorLayerPath);
        _wallLayer = GetNode<TileMapLayer>(WallLayerPath);
        _objectLayer = GetNode<TileMapLayer>(ObjectLayerPath);
        _fogLayer = GetNode<TileMapLayer>(FogLayerPath);
        _entityRenderer = GetNode<EntityRenderer>(EntityLayerPath);
        _camera = GetNode<CameraController>(CameraPath);

        _animController = new AnimationController();
        AddChild(_animController);

        _fov = new FOVCalculator();

        SubscribeSignals();
    }

    /// <summary>
    /// Called by GameManager or Main to inject the world reference.
    /// </summary>
    public void SetWorld(WorldState world)
    {
        _world = world;
        _entityRenderer.Initialize(world, _animController);
    }

    private void SubscribeSignals()
    {
        var bus = EventBus.Instance;
        bus.LevelGenerated += OnLevelGenerated;
        bus.FOVUpdated += OnFOVUpdated;
        bus.EntityMoved += OnEntityMoved;
        bus.EntityAttacked += OnEntityAttacked;
        bus.EntityDied += OnEntityDied;
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        if (bus == null) return;
        bus.LevelGenerated -= OnLevelGenerated;
        bus.FOVUpdated -= OnFOVUpdated;
        bus.EntityMoved -= OnEntityMoved;
        bus.EntityAttacked -= OnEntityAttacked;
        bus.EntityDied -= OnEntityDied;
    }

    // ═══ SIGNAL HANDLERS ═══

    private void OnLevelGenerated(int depth, int width, int height)
    {
        if (_world == null) return;

        _camera.SetMapBounds(width, height);
        RebuildTilemap();
        _entityRenderer.BuildSprites();
        ComputeFOV();

        _camera.SetTarget(_world.Player.Position);
        _camera.SnapToTarget();
    }

    private void OnFOVUpdated()
    {
        UpdateFog();
        _entityRenderer.UpdateVisibility();
    }

    private void OnEntityMoved(string entityId, int fromX, int fromY, int toX, int toY)
    {
        _entityRenderer.OnEntityMoved(entityId, fromX, fromY, toX, toY);

        // If the player moved, recompute FOV and update camera
        if (_world != null && entityId == _world.Player.Id.ToString())
        {
            ComputeFOV();
            _camera.SetTarget(new Position(toX, toY));
        }
    }

    private void OnEntityAttacked(string attackerId, string defenderId, int damage, bool isCrit, bool isMiss)
    {
        _entityRenderer.OnEntityAttacked(attackerId, defenderId, damage, isCrit, isMiss);
    }

    private void OnEntityDied(string entityId, string killerEntityId)
    {
        _entityRenderer.OnEntityDied(entityId, killerEntityId);
    }

    // ═══ TILEMAP BUILDING ═══

    private void RebuildTilemap()
    {
        if (_world == null) return;

        _floorLayer.Clear();
        _wallLayer.Clear();
        _objectLayer.Clear();
        _fogLayer.Clear();

        for (int y = 0; y < _world.Height; y++)
        {
            for (int x = 0; x < _world.Width; x++)
            {
                var pos = new Position(x, y);
                var tile = _world.GetTile(pos);
                var cellCoords = new Vector2I(x, y);

                switch (tile)
                {
                    case TileType.Floor:
                        _floorLayer.SetCell(cellCoords, SourceId, GetTileAtlas(tile));
                        break;
                    case TileType.Wall:
                        _wallLayer.SetCell(cellCoords, SourceId, GetTileAtlas(tile));
                        break;
                    case TileType.Door:
                    case TileType.StairsDown:
                    case TileType.StairsUp:
                        _floorLayer.SetCell(cellCoords, SourceId, GetTileAtlas(TileType.Floor));
                        _objectLayer.SetCell(cellCoords, SourceId, GetTileAtlas(tile));
                        break;
                    case TileType.Water:
                        _floorLayer.SetCell(cellCoords, SourceId, GetTileAtlas(tile));
                        break;
                    case TileType.Void:
                    default:
                        break;
                }

                // Start all tiles fogged
                _fogLayer.SetCell(cellCoords, SourceId, FogAtlas_Unseen);
            }
        }
    }

    // ═══ FOV ═══

    private void ComputeFOV()
    {
        if (_world == null) return;

        _world.ClearVisibility();
        _fov.Compute(
            _world.Player.Position,
            _world.Player.Stats.ViewRadius,
            pos => _world.BlocksSight(pos),
            pos => { if (_world.InBounds(pos)) _world.SetVisible(pos, true); }
        );

        EventBus.Instance.EmitSignal(EventBus.SignalName.FOVUpdated);
    }

    // ═══ FOG ═══

    private void UpdateFog()
    {
        if (_world == null) return;

        for (int y = 0; y < _world.Height; y++)
        {
            for (int x = 0; x < _world.Width; x++)
            {
                var pos = new Position(x, y);
                var cellCoords = new Vector2I(x, y);

                if (_world.IsVisible(pos))
                {
                    _fogLayer.SetCell(cellCoords, -1); // Clear fog
                }
                else if (_world.IsExplored(pos))
                {
                    _fogLayer.SetCell(cellCoords, SourceId, FogAtlas_Explored);
                }
                else
                {
                    _fogLayer.SetCell(cellCoords, SourceId, FogAtlas_Unseen);
                }
            }
        }
    }

    // ═══ TILE ATLAS COORDINATES ═══
    // Placeholder atlas coords — row 0 of the tileset source
    // Replace with real atlas coords once a tileset is imported.

    private static readonly Vector2I FogAtlas_Unseen = new(0, 0);
    private static readonly Vector2I FogAtlas_Explored = new(1, 0);

    private static Vector2I GetTileAtlas(TileType tile)
    {
        return tile switch
        {
            TileType.Floor => new Vector2I(0, 0),
            TileType.Wall => new Vector2I(1, 0),
            TileType.Door => new Vector2I(2, 0),
            TileType.StairsDown => new Vector2I(3, 0),
            TileType.StairsUp => new Vector2I(4, 0),
            TileType.Water => new Vector2I(5, 0),
            _ => new Vector2I(0, 0),
        };
    }
}
