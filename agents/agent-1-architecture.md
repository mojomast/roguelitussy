# Agent 1: Architecture Agent — Detailed Specification

## Mission
Set up the Godot 4.4 project skeleton, C# core library, directory structure, autoload singletons, shared contracts (interfaces/DTOs/signals), and integration wiring so all other agents can work in parallel against stable APIs.

---

## 1. Directory Structure (every folder)

```
godotussy/
├── project.godot
├── godotussy.csproj
├── godotussy.sln
├── .editorconfig
├── .gitignore
│
├── Core/                          # Pure C# simulation library (no Godot deps where possible)
│   ├── Interfaces/                # Shared contracts — ALL agents depend on these
│   │   ├── IWorldState.cs
│   │   ├── IAction.cs
│   │   ├── ITurnScheduler.cs
│   │   ├── ICombatResolver.cs
│   │   ├── IFOVProvider.cs
│   │   ├── IDungeonGenerator.cs
│   │   ├── IAIBrain.cs
│   │   ├── ISaveManager.cs
│   │   └── IContentDB.cs
│   ├── DTOs/                      # Shared data transfer objects
│   │   ├── Vec2I.cs               # Integer vector (grid position)
│   │   ├── EntityData.cs          # Entity snapshot for serialization
│   │   ├── TileData.cs            # Tile type + properties
│   │   ├── ItemData.cs            # Item definition
│   │   ├── EnemyData.cs           # Enemy definition
│   │   ├── StatusEffectData.cs    # Status effect definition
│   │   ├── ActionResult.cs        # Result of executing an action
│   │   ├── DamageEvent.cs         # Combat event payload
│   │   ├── LevelBlueprint.cs      # Generated level data
│   │   └── SavePayload.cs         # Top-level save structure
│   ├── Enums/
│   │   ├── TileType.cs            # Floor, Wall, StairsDown, StairsUp, Door, Water, Lava
│   │   ├── ActionType.cs          # Move, Attack, PickUp, UseItem, Wait, DropItem, UseStairs
│   │   ├── DamageType.cs          # Physical, Fire, Ice, Poison, Lightning
│   │   ├── StatusType.cs          # Poison, Burn, Freeze, Haste, Shield, Confusion, Blind
│   │   ├── AIState.cs             # Idle, Patrol, Chase, Attack, Flee
│   │   ├── Faction.cs             # Player, Enemy, Neutral
│   │   └── Direction.cs           # N, S, E, W, NE, NW, SE, SW, None
│   ├── Simulation/                # Agent 2 owns internals
│   │   └── (placeholder)
│   ├── Generation/                # Agent 4 owns internals
│   │   └── (placeholder)
│   ├── AI/                        # Agent 5 owns internals
│   │   └── (placeholder)
│   ├── Persistence/               # Agent 8 owns internals
│   │   └── (placeholder)
│   └── Content/                   # Agent 9 owns internals
│       └── (placeholder)
│
├── Scenes/
│   ├── Main.tscn                  # Root scene — entry point
│   ├── World/
│   │   ├── WorldView.tscn         # TileMap + entity rendering (Agent 3)
│   │   └── EntitySprite.tscn      # Reusable entity display
│   ├── UI/
│   │   ├── HUD.tscn               # Agent 6
│   │   ├── InventoryUI.tscn
│   │   ├── CombatLog.tscn
│   │   ├── CharacterSheet.tscn
│   │   ├── MainMenu.tscn
│   │   ├── PauseMenu.tscn
│   │   ├── GameOverScreen.tscn
│   │   └── Tooltip.tscn
│   └── Tools/
│       ├── MapEditor.tscn         # Agent 7
│       ├── ItemEditor.tscn
│       └── DebugConsole.tscn
│
├── Scripts/
│   ├── Autoloads/
│   │   ├── GameManager.cs         # Master game loop controller
│   │   ├── EventBus.cs            # Centralized signal bus
│   │   └── ContentDatabase.cs     # Loaded content registry
│   ├── World/
│   │   ├── WorldView.cs           # Agent 3
│   │   └── EntityRenderer.cs
│   ├── UI/
│   │   ├── HUD.cs                 # Agent 6
│   │   ├── InventoryUI.cs
│   │   ├── CombatLog.cs
│   │   └── CharacterSheet.cs
│   └── Tools/
│       ├── MapEditorPlugin.cs     # Agent 7
│       └── DebugConsole.cs
│
├── Content/
│   ├── Items/
│   │   └── items.json             # Agent 9
│   ├── Enemies/
│   │   └── enemies.json
│   ├── Rooms/
│   │   └── prefabs.json           # Room prefab definitions
│   ├── StatusEffects/
│   │   └── effects.json
│   └── LootTables/
│       └── loot.json
│
├── Assets/
│   ├── Tilesets/
│   │   └── dungeon_tileset.tres   # Placeholder 16x16 tileset
│   ├── Sprites/
│   │   ├── player.png
│   │   ├── enemies/
│   │   └── items/
│   ├── Fonts/
│   │   └── monospace.tres
│   └── Audio/
│       └── (placeholder)
│
├── Addons/
│   └── roguelike_tools/           # Agent 7 editor plugin
│       └── plugin.cfg
│
└── Tests/
    ├── SimulationTests/           # Agent 2
    ├── GenerationTests/           # Agent 4
    ├── AITests/                   # Agent 5
    ├── PersistenceTests/          # Agent 8
    └── ContentTests/              # Agent 9
```

---

## 2. project.godot Configuration

```ini
; Engine configuration file — DO NOT EDIT with the editor, only by code

config_version=5

[application]
config/name="Godotussy Roguelike"
config/description="Modular roguelike engine"
run/main_scene="res://Scenes/Main.tscn"
config/features=PackedStringArray("4.4", "C#", "Forward Plus")

[autoload]
GameManager="*res://Scripts/Autoloads/GameManager.cs"
EventBus="*res://Scripts/Autoloads/EventBus.cs"
ContentDatabase="*res://Scripts/Autoloads/ContentDatabase.cs"

[display]
window/size/viewport_width=1280
window/size/viewport_height=720
window/stretch/mode="canvas_items"
window/stretch/aspect="keep"

[dotnet]
project/assembly_name="godotussy"

[input]
move_up={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":87,"key_label":0,"unicode":119,"location":0,"echo":false,"script":null), Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194320,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null), Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194375,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
move_down={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":83,"key_label":0,"unicode":115,"location":0,"echo":false,"script":null), Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194322,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
move_left={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":65,"key_label":0,"unicode":97,"location":0,"echo":false,"script":null), Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194319,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
move_right={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":68,"key_label":0,"unicode":100,"location":0,"echo":false,"script":null), Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194321,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
move_up_left={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194377,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
move_up_right={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194379,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
move_down_left={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194373,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
move_down_right={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194371,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
wait_turn={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194370,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null), Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":46,"key_label":0,"unicode":46,"location":0,"echo":false,"script":null)]
}
pickup={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":71,"key_label":0,"unicode":103,"location":0,"echo":false,"script":null)]
}
inventory={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":73,"key_label":0,"unicode":105,"location":0,"echo":false,"script":null)]
}
use_stairs={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194325,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
pause_menu={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":4194305,"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)]
}
debug_console={
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"resource_name":"","device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":96,"key_label":0,"unicode":96,"location":0,"echo":false,"script":null)]
}

[rendering]
textures/canvas_textures/default_texture_filter=0
```

**Input map summary** (for reference by other agents):
| Action         | Keys                          |
|----------------|-------------------------------|
| move_up        | W / Up / Numpad 8             |
| move_down      | S / Down / Numpad 2           |
| move_left      | A / Left / Numpad 4           |
| move_right     | D / Right / Numpad 6          |
| move_up_left   | Numpad 7                      |
| move_up_right  | Numpad 9                      |
| move_down_left | Numpad 1                      |
| move_down_right| Numpad 3                      |
| wait_turn      | Numpad 5 / Period             |
| pickup         | G                             |
| inventory      | I                             |
| use_stairs     | Enter                         |
| pause_menu     | Escape                        |
| debug_console  | Backtick (`)                  |

**Rendering note**: `default_texture_filter=0` sets NEAREST for pixel art.

---

## 3. .csproj Setup

```xml
<Project Sdk="Godot.NET.Sdk/4.4.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <RootNamespace>Godotussy</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="8.0.*" />
  </ItemGroup>
</Project>
```

Key decisions:
- `net8.0` for latest C# features.
- `Nullable=enable` to catch null errors at compile time.
- `System.Text.Json` for content/save serialization (no Newtonsoft dependency).
- No separate class library — everything compiles as one Godot project assembly. Pure C# classes in `Core/` simply don't inherit from `GodotObject`.

---

## 4. Autoload Singletons

### 4.1 GameManager.cs
```csharp
using Godot;

namespace Godotussy;

/// <summary>
/// Master game loop. Autoloaded as "GameManager".
/// Coordinates turn processing between player input and AI.
/// </summary>
public partial class GameManager : Node
{
    public enum GameState { MainMenu, Playing, Paused, GameOver, Loading }

    public GameState CurrentState { get; private set; } = GameState.MainMenu;
    public int CurrentFloor { get; set; } = 1;
    public int Seed { get; private set; }

    // Core subsystem references (set during _Ready or via DI)
    public IWorldState? World { get; set; }
    public ITurnScheduler? Scheduler { get; set; }
    public ICombatResolver? Combat { get; set; }
    public IDungeonGenerator? Generator { get; set; }
    public ISaveManager? SaveMgr { get; set; }

    public override void _Ready()
    {
        // Subsystems register themselves here or are constructed
    }

    /// <summary>
    /// Called by input handler when player submits an action.
    /// Processes player turn, then all NPC turns, then signals render update.
    /// </summary>
    public void ProcessPlayerAction(IAction action)
    {
        // 1. Validate + execute player action
        // 2. Advance scheduler
        // 3. Process all NPC actions until player's turn again
        // 4. Emit TurnCompleted signal
    }

    public void StartNewGame(int seed)
    {
        Seed = seed;
        CurrentFloor = 1;
        CurrentState = GameState.Playing;
        // Generate floor 1, place player, signal ready
    }

    public void TransitionFloor(int newFloor)
    {
        CurrentFloor = newFloor;
        // Save current floor state, generate/load new floor, autosave
    }
}
```

### 4.2 EventBus.cs
```csharp
using Godot;

namespace Godotussy;

/// <summary>
/// Centralized signal bus. Autoloaded as "EventBus".
/// All cross-system communication goes through signals here.
/// Agents emit/connect signals but never call each other directly.
/// </summary>
public partial class EventBus : Node
{
    // === Turn signals ===
    [Signal] public delegate void TurnCompletedEventHandler();
    [Signal] public delegate void PlayerActionSubmittedEventHandler(int actionType, int targetX, int targetY, string itemId);

    // === Combat signals ===
    [Signal] public delegate void DamageDealtEventHandler(int attackerId, int defenderId, int amount, int damageType);
    [Signal] public delegate void EntityDiedEventHandler(int entityId);
    [Signal] public delegate void StatusEffectAppliedEventHandler(int entityId, int effectType, int duration);
    [Signal] public delegate void StatusEffectRemovedEventHandler(int entityId, int effectType);

    // === World signals ===
    [Signal] public delegate void FloorChangedEventHandler(int newFloor);
    [Signal] public delegate void EntityMovedEventHandler(int entityId, int fromX, int fromY, int toX, int toY);
    [Signal] public delegate void EntitySpawnedEventHandler(int entityId, int x, int y);
    [Signal] public delegate void EntityRemovedEventHandler(int entityId);
    [Signal] public delegate void ItemPickedUpEventHandler(int entityId, string itemId);
    [Signal] public delegate void ItemDroppedEventHandler(int entityId, string itemId, int x, int y);

    // === UI signals ===
    [Signal] public delegate void LogMessageEventHandler(string message, int colorType);
    [Signal] public delegate void InventoryChangedEventHandler(int entityId);
    [Signal] public delegate void HPChangedEventHandler(int entityId, int current, int max);

    // === Persistence signals ===
    [Signal] public delegate void SaveRequestedEventHandler(int slot);
    [Signal] public delegate void LoadRequestedEventHandler(int slot);
    [Signal] public delegate void SaveCompletedEventHandler(bool success);
    [Signal] public delegate void LoadCompletedEventHandler(bool success);

    // === FOV signals ===
    [Signal] public delegate void FOVRecalculatedEventHandler();
}
```

### 4.3 ContentDatabase.cs
```csharp
using Godot;
using System.Collections.Generic;

namespace Godotussy;

/// <summary>
/// Loaded content registry. Autoloaded as "ContentDatabase".
/// Loads all JSON content at startup. Other systems query by ID.
/// </summary>
public partial class ContentDatabase : Node
{
    private Dictionary<string, ItemData> _items = new();
    private Dictionary<string, EnemyData> _enemies = new();
    private Dictionary<string, StatusEffectData> _effects = new();

    public override void _Ready()
    {
        LoadItems("res://Content/Items/items.json");
        LoadEnemies("res://Content/Enemies/enemies.json");
        LoadEffects("res://Content/StatusEffects/effects.json");
    }

    public ItemData? GetItem(string id) => _items.GetValueOrDefault(id);
    public EnemyData? GetEnemy(string id) => _enemies.GetValueOrDefault(id);
    public StatusEffectData? GetEffect(string id) => _effects.GetValueOrDefault(id);
    public IReadOnlyDictionary<string, ItemData> AllItems => _items;
    public IReadOnlyDictionary<string, EnemyData> AllEnemies => _enemies;

    private void LoadItems(string path) { /* JSON deserialization */ }
    private void LoadEnemies(string path) { /* JSON deserialization */ }
    private void LoadEffects(string path) { /* JSON deserialization */ }
}
```

---

## 5. Shared Contracts (Interfaces & DTOs)

### 5.1 Core Interfaces

```csharp
// IWorldState.cs
namespace Godotussy;

public interface IWorldState
{
    int Width { get; }
    int Height { get; }
    TileType GetTile(Vec2I pos);
    void SetTile(Vec2I pos, TileType type);
    bool IsInBounds(Vec2I pos);
    bool IsWalkable(Vec2I pos);
    bool IsOpaque(Vec2I pos);

    // Entity management
    int SpawnEntity(EntityData data, Vec2I pos);
    void RemoveEntity(int entityId);
    EntityData? GetEntity(int entityId);
    void MoveEntity(int entityId, Vec2I newPos);
    EntityData? GetEntityAt(Vec2I pos);
    IEnumerable<EntityData> GetEntitiesInRadius(Vec2I center, int radius);
    IEnumerable<EntityData> GetAllEntities();

    // Items on ground
    void PlaceItem(string itemId, Vec2I pos);
    string? PickUpItem(Vec2I pos);
    IEnumerable<(string itemId, Vec2I pos)> GetGroundItems();

    // Floor data
    int CurrentFloor { get; }
    Vec2I StairsDownPos { get; }
    Vec2I StairsUpPos { get; }
}

// IAction.cs
namespace Godotussy;

public interface IAction
{
    ActionType Type { get; }
    int ActorId { get; }
    int EnergyCost { get; }
    bool Validate(IWorldState world);
    ActionResult Execute(IWorldState world, ICombatResolver combat);
}

// ITurnScheduler.cs
namespace Godotussy;

public interface ITurnScheduler
{
    void RegisterActor(int entityId, int speed);
    void UnregisterActor(int entityId);
    int GetNextActor();
    void ConsumeEnergy(int entityId, int cost);
    int GetEnergy(int entityId);
    void TickStatusEffects(int entityId, IWorldState world);
}

// ICombatResolver.cs
namespace Godotussy;

public interface ICombatResolver
{
    DamageEvent ResolveMeleeAttack(EntityData attacker, EntityData defender);
    DamageEvent ResolveRangedAttack(EntityData attacker, EntityData defender, int distance);
    bool RollHitChance(EntityData attacker, EntityData defender);
    int CalculateDamage(EntityData attacker, EntityData defender, DamageType type);
    int ApplyArmor(int rawDamage, int armor, DamageType type);
}

// IFOVProvider.cs
namespace Godotussy;

public interface IFOVProvider
{
    HashSet<Vec2I> ComputeFOV(Vec2I origin, int radius, Func<Vec2I, bool> isOpaque);
}

// IDungeonGenerator.cs
namespace Godotussy;

public interface IDungeonGenerator
{
    LevelBlueprint Generate(int floor, int seed, string biome);
}

// IAIBrain.cs
namespace Godotussy;

public interface IAIBrain
{
    IAction DecideAction(EntityData entity, IWorldState world, IFOVProvider fov);
}

// ISaveManager.cs
namespace Godotussy;

public interface ISaveManager
{
    bool Save(int slot, IWorldState world, ITurnScheduler scheduler, int floor, int seed);
    (IWorldState world, int floor, int seed)? Load(int slot);
    bool SlotExists(int slot);
    void DeleteSlot(int slot);
    SavePayload? ReadSlotMetadata(int slot);
}

// IContentDB.cs — implemented by ContentDatabase autoload
namespace Godotussy;

public interface IContentDB
{
    ItemData? GetItem(string id);
    EnemyData? GetEnemy(string id);
    StatusEffectData? GetEffect(string id);
    IReadOnlyDictionary<string, ItemData> AllItems { get; }
    IReadOnlyDictionary<string, EnemyData> AllEnemies { get; }
}
```

### 5.2 Core DTOs

```csharp
// Vec2I.cs
namespace Godotussy;

public readonly record struct Vec2I(int X, int Y)
{
    public static Vec2I operator +(Vec2I a, Vec2I b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2I operator -(Vec2I a, Vec2I b) => new(a.X - b.X, a.Y - b.Y);
    public int ManhattanDistance(Vec2I other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    public double EuclideanDistance(Vec2I other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
    public int ChebyshevDistance(Vec2I other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public static readonly Vec2I Zero = new(0, 0);
    public static readonly Vec2I Up = new(0, -1);
    public static readonly Vec2I Down = new(0, 1);
    public static readonly Vec2I Left = new(-1, 0);
    public static readonly Vec2I Right = new(1, 0);
    public static readonly Vec2I UpLeft = new(-1, -1);
    public static readonly Vec2I UpRight = new(1, -1);
    public static readonly Vec2I DownLeft = new(-1, 1);
    public static readonly Vec2I DownRight = new(1, 1);

    public static Vec2I FromDirection(Direction dir) => dir switch
    {
        Direction.N => Up, Direction.S => Down,
        Direction.E => Right, Direction.W => Left,
        Direction.NE => UpRight, Direction.NW => UpLeft,
        Direction.SE => DownRight, Direction.SW => DownLeft,
        _ => Zero
    };
}

// EntityData.cs
namespace Godotussy;

public class EntityData
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string SpriteId { get; set; } = "";
    public Vec2I Position { get; set; }
    public Faction Faction { get; set; }
    public bool IsPlayer { get; set; }

    // Stats
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; } = 100;     // 100 = normal speed
    public int ViewRadius { get; set; } = 8;

    // Inventory
    public List<string> Inventory { get; set; } = new();
    public string? EquippedWeapon { get; set; }
    public string? EquippedArmor { get; set; }
    public int MaxInventorySize { get; set; } = 20;

    // Status effects
    public List<ActiveStatusEffect> StatusEffects { get; set; } = new();

    // AI
    public string? AIProfileId { get; set; }  // null for player
    public string? EnemyTypeId { get; set; }

    // Energy (managed by scheduler, stored here for save/load)
    public int Energy { get; set; } = 0;
}

public class ActiveStatusEffect
{
    public StatusType Type { get; set; }
    public int RemainingTurns { get; set; }
    public int TickDamage { get; set; }       // damage per turn (for DoTs)
    public int Stacks { get; set; } = 1;
}

// TileData.cs
namespace Godotussy;

public readonly record struct TileData(TileType Type, bool Explored = false);

// ItemData.cs
namespace Godotussy;

public class ItemData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SpriteId { get; set; } = "";
    public string Category { get; set; } = "";  // weapon, armor, potion, scroll, food
    public int AttackBonus { get; set; }
    public int DefenseBonus { get; set; }
    public int HealAmount { get; set; }
    public string? AppliesEffect { get; set; }   // StatusType to apply on use
    public int EffectDuration { get; set; }
    public int EffectRadius { get; set; }         // for AoE items
    public DamageType DamageType { get; set; } = DamageType.Physical;
    public int Value { get; set; }                // gold value
    public bool Consumable { get; set; }
    public bool Equippable { get; set; }
    public int DropWeight { get; set; } = 100;    // loot table weight
    public int MinFloor { get; set; } = 1;        // earliest floor this can appear
}

// DamageEvent.cs
namespace Godotussy;

public record DamageEvent(
    int AttackerId,
    int DefenderId,
    int RawDamage,
    int FinalDamage,
    DamageType Type,
    bool Hit,
    bool Killed
);

// ActionResult.cs
namespace Godotussy;

public record ActionResult(
    bool Success,
    string Message,
    int EnergyCost,
    List<DamageEvent>? DamageEvents = null
);

// LevelBlueprint.cs
namespace Godotussy;

public class LevelBlueprint
{
    public int Width { get; set; }
    public int Height { get; set; }
    public TileType[,] Tiles { get; set; } = new TileType[0, 0];
    public Vec2I PlayerSpawn { get; set; }
    public Vec2I StairsDown { get; set; }
    public Vec2I StairsUp { get; set; }
    public List<(string enemyId, Vec2I pos)> EnemySpawns { get; set; } = new();
    public List<(string itemId, Vec2I pos)> ItemSpawns { get; set; } = new();
    public List<RoomData> Rooms { get; set; } = new();
    public string Biome { get; set; } = "dungeon";
    public int Floor { get; set; }
}

public record RoomData(int X, int Y, int Width, int Height);

// SavePayload.cs
namespace Godotussy;

public class SavePayload
{
    public int Version { get; set; } = 1;
    public DateTime Timestamp { get; set; }
    public int Seed { get; set; }
    public int CurrentFloor { get; set; }
    public int TurnNumber { get; set; }
    public EntityData Player { get; set; } = new();
    public List<EntityData> Entities { get; set; } = new();
    public TileType[,] Tiles { get; set; } = new TileType[0, 0];
    public bool[,] Explored { get; set; } = new bool[0, 0];
    public List<(string itemId, Vec2I pos)> GroundItems { get; set; } = new();
    public Dictionary<int, int> SchedulerEnergy { get; set; } = new();
}


// StatusEffectData.cs
namespace Godotussy;

public class StatusEffectData
{
    public string Id { get; set; } = "";
    public StatusType Type { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int DefaultDuration { get; set; }
    public int TickDamage { get; set; }
    public bool Stackable { get; set; }
    public int MaxStacks { get; set; } = 1;
    public string SpriteId { get; set; } = "";
}

// EnemyData.cs (content definition, not runtime)
namespace Godotussy;

public class EnemyData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SpriteId { get; set; } = "";
    public int HP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; } = 100;
    public int ViewRadius { get; set; } = 6;
    public string AIProfile { get; set; } = "aggressive";
    public int XPValue { get; set; }
    public string? LootTableId { get; set; }
    public int MinFloor { get; set; } = 1;
    public int MaxFloor { get; set; } = 99;
    public int SpawnWeight { get; set; } = 100;
    public Faction Faction { get; set; } = Faction.Enemy;
    public DamageType DamageType { get; set; } = DamageType.Physical;
    public string? OnDeathEffect { get; set; }  // e.g. "explode_poison"
}
```

### 5.3 Enums

```csharp
// TileType.cs
namespace Godotussy;
public enum TileType { Floor, Wall, StairsDown, StairsUp, DoorClosed, DoorOpen, Water, Lava }

// ActionType.cs
namespace Godotussy;
public enum ActionType { Move, Attack, PickUp, UseItem, Wait, DropItem, UseStairs, OpenDoor }

// DamageType.cs
namespace Godotussy;
public enum DamageType { Physical, Fire, Ice, Poison, Lightning }

// StatusType.cs
namespace Godotussy;
public enum StatusType { Poison, Burn, Freeze, Haste, Shield, Confusion, Blind, Regeneration }

// AIState.cs
namespace Godotussy;
public enum AIState { Idle, Patrol, Chase, Attack, Flee }

// Faction.cs
namespace Godotussy;
public enum Faction { Player, Enemy, Neutral }

// Direction.cs
namespace Godotussy;
public enum Direction { N, S, E, W, NE, NW, SE, SW, None }
```

---

## 6. C# ↔ GDScript Wiring

All game logic is in C#. No GDScript is used for logic. Godot nodes use C# scripts attached directly.

**Pattern**: C# node scripts inherit from Godot node types and access autoload singletons via `GetNode<T>`:

```csharp
// Any C# script accessing autoloads:
var gameManager = GetNode<GameManager>("/root/GameManager");
var eventBus = GetNode<EventBus>("/root/EventBus");
var contentDB = GetNode<ContentDatabase>("/root/ContentDatabase");
```

**Signal wiring pattern** (C# to C#):
```csharp
// Emitting:
var eventBus = GetNode<EventBus>("/root/EventBus");
eventBus.EmitSignal(EventBus.SignalName.EntityMoved, entityId, fromX, fromY, toX, toY);

// Connecting:
eventBus.EntityMoved += OnEntityMoved;

private void OnEntityMoved(int entityId, int fromX, int fromY, int toX, int toY)
{
    // React to movement
}
```

**If any GDScript is needed** (e.g., for quick editor tool scripts), call C# from GDScript:
```gdscript
# GDScript calling C# autoload:
var game_manager = get_node("/root/GameManager")
game_manager.ProcessPlayerAction(action)
```

**Rule**: All 9 agents write C# only. GDScript is only permitted for editor plugin `plugin.gd` boilerplate if Godot requires it.

---

## 7. Main.tscn Scene Tree

```
Main (Node2D) — Scripts/Autoloads/... are autoloads, not in tree
├── WorldView (Node2D) — Scripts/World/WorldView.cs
│   ├── TileMapLayer_Floor (TileMapLayer)
│   ├── TileMapLayer_Walls (TileMapLayer)
│   ├── TileMapLayer_Objects (TileMapLayer)
│   ├── TileMapLayer_Fog (TileMapLayer)
│   ├── EntityLayer (Node2D) — holds EntitySprite children
│   └── Camera2D — follows player
├── CanvasLayer (CanvasLayer)
│   ├── HUD (Control) — Scripts/UI/HUD.cs
│   ├── InventoryUI (Control) — Scripts/UI/InventoryUI.cs (hidden by default)
│   ├── CombatLog (Control) — Scripts/UI/CombatLog.cs
│   ├── CharacterSheet (Control) — hidden by default
│   ├── Tooltip (Control) — hidden by default
│   ├── PauseMenu (Control) — hidden by default
│   ├── GameOverScreen (Control) — hidden by default
│   └── DebugConsole (Control) — hidden by default
└── MainMenu (Control) — visible on startup, hides WorldView
```

---

## 8. Acceptance Tests

| # | Test | Pass Criteria |
|---|------|---------------|
| A1 | Project opens in Godot 4.4 | No errors in editor console |
| A2 | `dotnet build` succeeds | Exit code 0, zero warnings on contracts |
| A3 | Main.tscn runs | Window opens, no crash |
| A4 | GameManager autoload accessible | `GetNode("/root/GameManager")` returns non-null in any scene script |
| A5 | EventBus autoload accessible | `GetNode("/root/EventBus")` returns non-null |
| A6 | ContentDatabase autoload accessible | `GetNode("/root/ContentDatabase")` returns non-null |
| A7 | All interfaces compile | All files in `Core/Interfaces/` compile without error |
| A8 | All DTOs compile | All files in `Core/DTOs/` compile without error |
| A9 | All enums compile | All files in `Core/Enums/` compile without error |
| A10 | Vec2I operations work | `new Vec2I(1,2) + new Vec2I(3,4) == new Vec2I(4,6)` |
| A11 | Signal emits without crash | Emit `TurnCompleted` from test script, no error |
| A12 | Directory structure matches spec | Every folder in spec exists on disk |
| A13 | Input actions registered | `InputMap.HasAction("move_up")` returns true for all actions |
| A14 | Placeholder content loads | ContentDatabase._Ready completes without exceptions (with empty JSON arrays) |
| A15 | Rendering set to nearest filter | Pixel art renders crisp at any window size |

---

## Files to Create (Summary)

| File | Purpose | Lines (est.) |
|------|---------|-------------|
| `project.godot` | Engine config, autoloads, input map | ~200 |
| `godotussy.csproj` | C# build configuration | ~15 |
| `godotussy.sln` | Solution file | ~25 |
| `.gitignore` | Git ignores for Godot + C# | ~30 |
| `.editorconfig` | Code style | ~20 |
| `Core/Interfaces/*.cs` (9 files) | Shared API contracts | ~150 total |
| `Core/DTOs/*.cs` (10 files) | Shared data structures | ~250 total |
| `Core/Enums/*.cs` (7 files) | Shared enumerations | ~35 total |
| `Scripts/Autoloads/GameManager.cs` | Game loop singleton | ~80 |
| `Scripts/Autoloads/EventBus.cs` | Signal bus singleton | ~50 |
| `Scripts/Autoloads/ContentDatabase.cs` | Content registry singleton | ~60 |
| `Scenes/Main.tscn` | Root scene | ~100 |
| Placeholder dirs + files | Folder structure for all agents | ~20 |
