# Agent 3: Rendering Agent — Detailed Specification

## Mission
Render the dungeon using Godot TileMapLayers, implement symmetric shadowcasting FOV, render entity sprites, animate movement/attacks with tweens, and make the camera follow the player. All visual representation of the simulation state.

---

## 1. Files to Create

| File | Purpose |
|------|---------|
| `Scripts/World/WorldView.cs` | Master rendering controller, attached to WorldView node |
| `Scripts/World/FOVCalculator.cs` | Symmetric shadowcasting implementation |
| `Scripts/World/EntityRenderer.cs` | Manages entity sprite children |
| `Scripts/World/AnimationController.cs` | Tween-based movement/attack animations |
| `Scenes/World/WorldView.tscn` | Scene with TileMapLayers + EntityLayer + Camera |
| `Scenes/World/EntitySprite.tscn` | Reusable entity sprite scene |
| `Assets/Tilesets/dungeon_tileset.tres` | TileSet resource for dungeon tiles |
| `Tests/RenderingTests/FOVTests.cs` | FOV algorithm unit tests |

---

## 2. TileMap Layer Setup

Four `TileMapLayer` nodes, each using the same `TileSet` resource but different atlas source IDs or tile IDs.

### Layer Stack (bottom to top)

| Layer | Node Name | Z-Index | Purpose |
|-------|-----------|---------|---------|
| 0 | `TileMapLayer_Floor` | 0 | Floor tiles, stairs, water, lava |
| 1 | `TileMapLayer_Walls` | 1 | Wall tiles, doors |
| 2 | `TileMapLayer_Objects` | 2 | Ground items, decorations |
| 3 | `TileMapLayer_Fog` | 10 | Fog of war overlay |

### Tile Size
- **16×16 pixels** per tile.
- Grid cell size in TileSet: 16×16.

### TileSet Configuration (`dungeon_tileset.tres`)

Source: Single texture atlas (or autotile). Minimum tiles needed:

| Atlas ID | Tile | Visual |
|----------|------|--------|
| 0 | Floor | Gray stone |
| 1 | Wall | Dark brick |
| 2 | StairsDown | Floor + down arrow |
| 3 | StairsUp | Floor + up arrow |
| 4 | DoorClosed | Brown rectangle |
| 5 | DoorOpen | Brown open rectangle |
| 6 | Water | Blue |
| 7 | Lava | Orange/red |
| 8 | FogHidden | Pure black (fully opaque) |
| 9 | FogSeen | Semi-transparent black (50% alpha) |

**Placeholder art**: Generate solid-color 16×16 rectangles with 1px borders. Pixel art not required for MVP.

### Rendering Flow

```csharp
public partial class WorldView : Node2D
{
    private TileMapLayer _floorLayer;
    private TileMapLayer _wallLayer;
    private TileMapLayer _objectLayer;
    private TileMapLayer _fogLayer;
    private Node2D _entityLayer;
    private Camera2D _camera;

    private IWorldState _world;
    private HashSet<Vec2I> _visibleTiles = new();
    private HashSet<Vec2I> _exploredTiles = new();
    private FOVCalculator _fov = new();

    public override void _Ready()
    {
        _floorLayer = GetNode<TileMapLayer>("TileMapLayer_Floor");
        _wallLayer = GetNode<TileMapLayer>("TileMapLayer_Walls");
        _objectLayer = GetNode<TileMapLayer>("TileMapLayer_Objects");
        _fogLayer = GetNode<TileMapLayer>("TileMapLayer_Fog");
        _entityLayer = GetNode<Node2D>("EntityLayer");
        _camera = GetNode<Camera2D>("Camera2D");

        var eventBus = GetNode<EventBus>("/root/EventBus");
        eventBus.TurnCompleted += OnTurnCompleted;
        eventBus.FloorChanged += OnFloorChanged;
    }

    public void RenderFullMap(IWorldState world)
    {
        _world = world;
        // Clear all layers
        _floorLayer.Clear();
        _wallLayer.Clear();
        _objectLayer.Clear();
        _fogLayer.Clear();

        // Render terrain
        for (int x = 0; x < world.Width; x++)
        {
            for (int y = 0; y < world.Height; y++)
            {
                var pos = new Vec2I(x, y);
                var tile = world.GetTile(pos);
                RenderTile(pos, tile);
            }
        }

        // Initial FOV
        RecalculateFOV();
        UpdateFogLayer();
    }

    private void RenderTile(Vec2I pos, TileType tile)
    {
        var gpos = new Vector2I(pos.X, pos.Y);
        switch (tile)
        {
            case TileType.Floor:
            case TileType.StairsDown:
            case TileType.StairsUp:
            case TileType.Water:
            case TileType.Lava:
                _floorLayer.SetCell(gpos, sourceId: 0, atlasCoords: TileAtlas(tile));
                break;
            case TileType.Wall:
            case TileType.DoorClosed:
            case TileType.DoorOpen:
                _wallLayer.SetCell(gpos, sourceId: 0, atlasCoords: TileAtlas(tile));
                break;
        }
    }

    private Vector2I TileAtlas(TileType type) => type switch
    {
        TileType.Floor => new Vector2I(0, 0),
        TileType.Wall => new Vector2I(1, 0),
        TileType.StairsDown => new Vector2I(2, 0),
        TileType.StairsUp => new Vector2I(3, 0),
        TileType.DoorClosed => new Vector2I(4, 0),
        TileType.DoorOpen => new Vector2I(5, 0),
        TileType.Water => new Vector2I(6, 0),
        TileType.Lava => new Vector2I(7, 0),
        _ => new Vector2I(0, 0)
    };
}
```

---

## 3. Symmetric Shadowcasting FOV

### Algorithm Reference
Use **Symmetric Shadowcasting** as described by Albert Ford:
https://www.albertford.com/shadowcasting/

This algorithm guarantees: if tile A can see tile B, then tile B can see tile A (symmetry).

### Implementation

```csharp
public class FOVCalculator : IFOVProvider
{
    /// <summary>
    /// Compute all visible tiles from origin within radius using symmetric shadowcasting.
    /// </summary>
    public HashSet<Vec2I> ComputeFOV(Vec2I origin, int radius, Func<Vec2I, bool> isOpaque)
    {
        var visible = new HashSet<Vec2I> { origin };

        for (int i = 0; i < 4; i++)
        {
            var quadrant = new Quadrant(i, origin);
            RevealRecursive(quadrant, 1, new Slope(0, 1), new Slope(1, 1), radius, isOpaque, visible);
        }

        return visible;
    }

    private void RevealRecursive(Quadrant q, int row, Slope startSlope, Slope endSlope,
                                  int radius, Func<Vec2I, bool> isOpaque, HashSet<Vec2I> visible)
    {
        if (row > radius) return;
        if (startSlope.GreaterOrEqual(endSlope)) return;

        bool prevWasOpaque = false;
        var newStart = startSlope;

        int minCol = RoundTiesUp(row * startSlope.Num, startSlope.Den);
        int maxCol = RoundTiesDown(row * endSlope.Num, endSlope.Den);

        for (int col = minCol; col <= maxCol; col++)
        {
            var pos = q.Transform(row, col);
            bool inRadius = row * row + col * col <= radius * radius; // Euclidean

            if (inRadius)
            {
                visible.Add(pos);
            }

            bool currentOpaque = !inRadius || isOpaque(pos);

            if (currentOpaque && !prevWasOpaque)
            {
                // Start of opaque span
                newStart = new Slope(2 * col - 1, 2 * row);
            }

            if (!currentOpaque && prevWasOpaque)
            {
                // End of opaque span — recurse with narrowed arc
                RevealRecursive(q, row + 1, newStart, new Slope(2 * col - 1, 2 * row), radius, isOpaque, visible);
            }

            prevWasOpaque = currentOpaque;
        }

        if (!prevWasOpaque)
        {
            RevealRecursive(q, row + 1, newStart, endSlope, radius, isOpaque, visible);
        }
    }

    // Helper types
    private record struct Slope(int Num, int Den)
    {
        public bool GreaterOrEqual(Slope other) => Num * other.Den >= other.Num * Den;
    }

    private record struct Quadrant(int Index, Vec2I Origin)
    {
        public Vec2I Transform(int row, int col) => Index switch
        {
            0 => new Vec2I(Origin.X + col, Origin.Y - row), // North
            1 => new Vec2I(Origin.X + row, Origin.Y + col), // East
            2 => new Vec2I(Origin.X - col, Origin.Y + row), // South
            3 => new Vec2I(Origin.X - row, Origin.Y - col), // West
            _ => Origin
        };
    }

    private static int RoundTiesUp(int num, int den) => (num + den - 1) / den;
    private static int RoundTiesDown(int num, int den) => num / den;
}
```

### Tile Visibility States

| State | Meaning | Visual |
|-------|---------|--------|
| **Visible** | In current FOV | Full color, entities shown |
| **Seen** (explored) | Was visible before, not now | Desaturated / 50% dark overlay, no entities |
| **Hidden** | Never seen | Pure black / not rendered |

### FOV Update Flow

```
1. Player acts (movement or any action)
2. EventBus.TurnCompleted fires
3. WorldView.RecalculateFOV():
   a. Get player entity position and view radius
   b. Call FOVCalculator.ComputeFOV(playerPos, viewRadius, world.IsOpaque)
   c. Store result as _visibleTiles
   d. Union _visibleTiles into _exploredTiles
4. WorldView.UpdateFogLayer():
   a. For each tile in map:
      - If in _visibleTiles: clear fog tile
      - If in _exploredTiles but not _visibleTiles: set FogSeen tile (semi-transparent)
      - Else: set FogHidden tile (fully opaque)
5. WorldView.UpdateEntityVisibility():
   a. For each entity sprite:
      - Visible = entity.Position is in _visibleTiles
```

---

## 4. Entity Sprite Rendering

### EntitySprite.tscn Scene

```
EntitySprite (Node2D)
└── Sprite2D — displays the entity texture
```

### EntityRenderer.cs

```csharp
public partial class EntityRenderer : Node2D
{
    private readonly Dictionary<int, Node2D> _sprites = new();
    private IWorldState _world;
    private HashSet<Vec2I> _visibleTiles;

    private const int TileSize = 16;

    public void OnEntitySpawned(int entityId, int x, int y)
    {
        var scene = GD.Load<PackedScene>("res://Scenes/World/EntitySprite.tscn");
        var sprite = scene.Instantiate<Node2D>();

        var entity = _world.GetEntity(entityId);
        // Load sprite texture based on entity.SpriteId
        var tex = GD.Load<Texture2D>($"res://Assets/Sprites/{entity.SpriteId}.png");
        sprite.GetNode<Sprite2D>("Sprite2D").Texture = tex;

        sprite.Position = new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2);
        AddChild(sprite);
        _sprites[entityId] = sprite;
    }

    public void OnEntityMoved(int entityId, int fromX, int fromY, int toX, int toY)
    {
        if (!_sprites.TryGetValue(entityId, out var sprite)) return;
        // Animate via AnimationController
        var targetPos = new Vector2(toX * TileSize + TileSize / 2, toY * TileSize + TileSize / 2);
        AnimationController.AnimateMove(sprite, targetPos);
    }

    public void OnEntityRemoved(int entityId)
    {
        if (_sprites.TryGetValue(entityId, out var sprite))
        {
            sprite.QueueFree();
            _sprites.Remove(entityId);
        }
    }

    public void UpdateVisibility(HashSet<Vec2I> visibleTiles)
    {
        _visibleTiles = visibleTiles;
        foreach (var (id, sprite) in _sprites)
        {
            var entity = _world.GetEntity(id);
            if (entity != null)
                sprite.Visible = visibleTiles.Contains(entity.Position);
        }
    }
}
```

---

## 5. Animation System (Tween-Based)

### Movement Animation

```csharp
public static class AnimationController
{
    private const float MoveDuration = 0.1f;   // seconds
    private const float AttackDuration = 0.15f;

    public static void AnimateMove(Node2D sprite, Vector2 targetPos)
    {
        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "position", targetPos, MoveDuration)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);
    }

    /// <summary>
    /// Bump animation: sprite moves partway toward target then back.
    /// Used for melee attacks.
    /// </summary>
    public static void AnimateAttack(Node2D attacker, Vector2 targetPos)
    {
        var originalPos = attacker.Position;
        var bumpPos = originalPos + (targetPos - originalPos) * 0.3f;

        var tween = attacker.CreateTween();
        tween.TweenProperty(attacker, "position", bumpPos, AttackDuration * 0.4f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(attacker, "position", originalPos, AttackDuration * 0.6f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.In);
    }

    /// <summary>
    /// Flash red on damage taken.
    /// </summary>
    public static void AnimateDamage(Node2D sprite)
    {
        var spriteNode = sprite.GetNode<Sprite2D>("Sprite2D");
        var tween = sprite.CreateTween();
        spriteNode.Modulate = Colors.Red;
        tween.TweenProperty(spriteNode, "modulate", Colors.White, 0.2f);
    }

    /// <summary>
    /// Fade out and free on death.
    /// </summary>
    public static void AnimateDeath(Node2D sprite)
    {
        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "modulate:a", 0.0f, 0.3f);
        tween.TweenCallback(Callable.From(() => sprite.QueueFree()));
    }
}
```

### Animation Sequencing

Animations play between turns. The game loop waits for animations to complete before processing the next turn:

```csharp
// In GameManager or WorldView:
// 1. Execute all actions for this turn
// 2. Emit signals (EntityMoved, DamageDealt, etc.)
// 3. WorldView queues animations from signal handlers
// 4. await ToSignal(animationTween, "finished") or use a timer
// 5. Then allow next input / next NPC turn

// Simple approach: use a flag
public bool IsAnimating { get; private set; }

// In _Process, block input while IsAnimating is true
```

---

## 6. Camera Following Player

```csharp
// In WorldView._Ready():
_camera = GetNode<Camera2D>("Camera2D");
_camera.Enabled = true;
_camera.PositionSmoothingEnabled = true;
_camera.PositionSmoothingSpeed = 10.0f;

// After player moves:
public void CenterOnPlayer(Vec2I playerPos)
{
    _camera.Position = new Vector2(
        playerPos.X * TileSize + TileSize / 2,
        playerPos.Y * TileSize + TileSize / 2
    );
}

// Camera zoom: 2x-4x for 16px tiles on 1280x720 viewport
_camera.Zoom = new Vector2(3, 3); // 3x zoom as default
```

### Camera Constraints
- Smoothly follow player position.
- No clamping to map edges (dungeon is surrounded by walls, camera overshooting into black is fine).
- Zoom adjustable with mouse wheel (optional, nice-to-have).

---

## 7. Test Scenarios (12)

| # | Test Scenario | Expected |
|---|---------------|----------|
| R1 | FOV from open room center, radius 8 | All tiles within 8 tiles visible, walls block behind |
| R2 | FOV symmetry: if A sees B, B sees A | For every visible pair, symmetry holds |
| R3 | FOV in 1-wide corridor | Only corridor tiles visible, not through walls |
| R4 | FOV at map edge | No out-of-bounds errors, edge tiles correctly visible |
| R5 | FOV with radius 0 | Only origin tile visible |
| R6 | Explored tiles persist after moving away | Previously visible tiles remain in _exploredTiles |
| R7 | Entity sprite created on spawn | Sprite exists as child of EntityLayer |
| R8 | Entity sprite removed on death | Sprite freed after death animation |
| R9 | Entity hidden when outside FOV | sprite.Visible == false for entities in fog |
| R10 | Move animation reaches target position | After tween completes, sprite.Position matches tile |
| R11 | Full map renders without error | RenderFullMap(80×50 world) completes in < 100ms |
| R12 | Camera centers on player after move | Camera.Position matches player tile center |

---

## 8. Dependencies

| Dependency | Provider | Notes |
|------------|----------|-------|
| `IWorldState` | Agent 2 | Read tile/entity data for rendering |
| `IFOVProvider` | Self (FOVCalculator) | Implements the interface from Agent 1 |
| `EventBus` | Agent 1 | Connect to signals for updates |
| `LevelBlueprint` | Agent 4 | Initial render triggered by `FloorChanged` |
| Tileset art | Placeholder | Create colored rectangles |
| Entity sprites | Placeholder | Create colored squares with letters |

### Placeholder Art Generation

Create minimal 16×16 PNG files programmatically or as solid-color placeholders:
- `player.png`: Green square with "@" or "P"
- `enemies/rat.png`: Brown square with "r"
- `enemies/skeleton.png`: White square with "s"
- `items/potion.png`: Red square with "!"
- Floor tile: Gray (#666666)
- Wall tile: Dark gray (#333333)

These can be 1-color PNGs or generated via `Image.Create()` in C# at startup.
