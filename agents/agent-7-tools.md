# Agent 7: Tools Agent — Detailed Specification

## Mission
Build developer tools for content creation and debugging: a map editor for painting tiles and saving room prefabs, an item/enemy editor, and a debug console with cheat commands. Implemented as Godot editor plugins and in-game overlays.

---

## 1. Files to Create

| File | Purpose |
|------|---------|
| `Addons/roguelike_tools/plugin.cfg` | Plugin registration |
| `Addons/roguelike_tools/RoguelikeToolsPlugin.cs` | Main plugin entry point |
| `Scripts/Tools/MapEditor.cs` | Map editor: tile painting, room prefab save/load |
| `Scripts/Tools/ItemEditor.cs` | Item/enemy JSON editor with form UI |
| `Scripts/Tools/DebugConsole.cs` | In-game command console |
| `Scenes/Tools/MapEditor.tscn` | Map editor scene |
| `Scenes/Tools/ItemEditor.tscn` | Item editor scene |
| `Scenes/Tools/DebugConsole.tscn` | Debug console scene |

---

## 2. Map Editor

### Purpose
Paint tiles to create room prefabs, save them to `Content/Rooms/prefabs.json`, and load existing prefabs for editing.

### UI Layout

```
┌─────────────────────────────────────────────────────────┐
│  MAP EDITOR                                [Save] [Load]│
├──────────┬──────────────────────────────────────────────┤
│ PALETTE  │                                              │
│          │                                              │
│ [Floor]  │         TILE CANVAS                          │
│ [Wall]   │         (click to paint)                     │
│ [Water]  │                                              │
│ [Lava]   │         20×20 grid default                   │
│ [Door]   │                                              │
│ [StairDn]│                                              │
│ [StairUp]│                                              │
│          │                                              │
│ TOOLS    │                                              │
│ [Pen]    │                                              │
│ [Rect]   │                                              │
│ [Fill]   │                                              │
│          │                                              │
│ SIZE     │                                              │
│ W: [20]  │                                              │
│ H: [20]  │                                              │
├──────────┴──────────────────────────────────────────────┤
│ ID: [room_id]  Tags: [dungeon, cave]  Floor: [1]-[99]  │
└─────────────────────────────────────────────────────────┘
```

### MapEditor.cs Implementation

```csharp
public partial class MapEditor : Control
{
    private TileType _selectedTile = TileType.Floor;
    private TileType[,] _canvas;
    private int _canvasWidth = 20;
    private int _canvasHeight = 20;
    private string _roomId = "new_room";
    private string _tags = "common";
    private int _minFloor = 1;
    private int _maxFloor = 99;

    private enum Tool { Pen, Rect, Fill }
    private Tool _currentTool = Tool.Pen;

    public override void _Ready()
    {
        _canvas = new TileType[_canvasWidth, _canvasHeight];
        // Initialize all as Wall
        for (int x = 0; x < _canvasWidth; x++)
            for (int y = 0; y < _canvasHeight; y++)
                _canvas[x, y] = TileType.Wall;

        RedrawCanvas();
    }

    // Input: click/drag to paint tiles
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var gridPos = ScreenToGrid(mb.Position);
            if (IsInCanvas(gridPos))
                PaintTile(gridPos);
        }
        if (@event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var gridPos = ScreenToGrid(mm.Position);
            if (IsInCanvas(gridPos))
                PaintTile(gridPos);
        }
    }

    private void PaintTile(Vec2I pos)
    {
        switch (_currentTool)
        {
            case Tool.Pen:
                _canvas[pos.X, pos.Y] = _selectedTile;
                break;
            case Tool.Fill:
                FloodFillCanvas(pos, _canvas[pos.X, pos.Y], _selectedTile);
                break;
            case Tool.Rect:
                // Handled via drag start/end
                break;
        }
        RedrawCanvas();
    }

    /// <summary>
    /// Save current canvas as a room prefab to prefabs.json
    /// </summary>
    public void SavePrefab()
    {
        // Convert canvas to string array format
        var tiles = new string[_canvasHeight];
        for (int y = 0; y < _canvasHeight; y++)
        {
            var row = new char[_canvasWidth];
            for (int x = 0; x < _canvasWidth; x++)
            {
                row[x] = TileToChar(_canvas[x, y]);
            }
            tiles[y] = new string(row);
        }

        var prefab = new
        {
            id = _roomId,
            width = _canvasWidth,
            height = _canvasHeight,
            tiles = tiles,
            legend = new Dictionary<string, string>
            {
                ["." ] = "Wall",
                ["f"] = "Floor",
                ["d"] = "DoorClosed",
                ["w"] = "Water",
                ["l"] = "Lava",
                ["<"] = "StairsUp",
                [">"] = "StairsDown"
            },
            tags = _tags.Split(',').Select(t => t.Trim()).ToArray(),
            minFloor = _minFloor,
            maxFloor = _maxFloor,
            spawnPoints = new List<object>() // TODO: spawn point placement
        };

        // Load existing prefabs.json, append, save
        var path = ProjectSettings.GlobalizePath("res://Content/Rooms/prefabs.json");
        // Read, deserialize, add, serialize, write
        // Use System.Text.Json
    }

    /// <summary>
    /// Load a prefab from prefabs.json by ID and populate the canvas
    /// </summary>
    public void LoadPrefab(string id)
    {
        var path = ProjectSettings.GlobalizePath("res://Content/Rooms/prefabs.json");
        // Read, find by id, populate _canvas
    }

    private static char TileToChar(TileType type) => type switch
    {
        TileType.Wall => '.',
        TileType.Floor => 'f',
        TileType.DoorClosed => 'd',
        TileType.DoorOpen => 'd',
        TileType.Water => 'w',
        TileType.Lava => 'l',
        TileType.StairsUp => '<',
        TileType.StairsDown => '>',
        _ => '.'
    };

    private void RedrawCanvas()
    {
        // Render canvas tiles as colored rectangles in a draw call or via TileMap
        QueueRedraw();
    }

    public override void _Draw()
    {
        const int cellSize = 24;
        for (int x = 0; x < _canvasWidth; x++)
        {
            for (int y = 0; y < _canvasHeight; y++)
            {
                var color = TileColor(_canvas[x, y]);
                var rect = new Rect2(x * cellSize + 200, y * cellSize + 40, cellSize - 1, cellSize - 1);
                DrawRect(rect, color);
            }
        }
    }

    private static Color TileColor(TileType type) => type switch
    {
        TileType.Floor => new Color(0.4f, 0.4f, 0.4f),
        TileType.Wall => new Color(0.15f, 0.15f, 0.15f),
        TileType.Water => new Color(0.2f, 0.3f, 0.8f),
        TileType.Lava => new Color(0.9f, 0.3f, 0.1f),
        TileType.DoorClosed => new Color(0.6f, 0.3f, 0.1f),
        TileType.StairsDown => new Color(0.8f, 0.8f, 0.2f),
        TileType.StairsUp => new Color(0.2f, 0.8f, 0.2f),
        _ => Colors.Black
    };
}
```

---

## 3. Item/Enemy Editor

### Purpose
Form-based editor for creating and editing items and enemies stored in JSON files. No code changes needed to add content.

### UI Layout

```
┌──────────────────────────────────────────────┐
│  ITEM EDITOR                                 │
├──────────────────────────────────────────────┤
│  Items List          │  Item Details         │
│  ┌────────────────┐  │                       │
│  │ health_potion  │  │  ID: [health_potion]  │
│  │ iron_sword     │  │  Name: [Health Potion] │
│  │ leather_mail   │  │  Category: [potion ▼]  │
│  │ fire_scroll    │  │  Description: [...]    │
│  │ (+ New Item)   │  │  Attack Bonus: [0]     │
│  └────────────────┘  │  Defense Bonus: [0]    │
│                      │  Heal Amount: [25]     │
│  [Save All]          │  Consumable: [✓]       │
│  [Validate]          │  Equippable: [ ]       │
│                      │  Drop Weight: [100]    │
│                      │  Min Floor: [1]        │
│                      │  Sprite: [potion ▼]    │
└──────────────────────┴───────────────────────┘
```

### Implementation Approach

```csharp
public partial class ItemEditor : Control
{
    private ItemList _itemList;
    private Dictionary<string, ItemData> _items = new();
    private string? _selectedItemId;

    public override void _Ready()
    {
        _itemList = GetNode<ItemList>("HSplit/ItemList");
        LoadItems();
        PopulateList();
    }

    private void LoadItems()
    {
        var path = ProjectSettings.GlobalizePath("res://Content/Items/items.json");
        var json = System.IO.File.ReadAllText(path);
        var items = JsonSerializer.Deserialize<List<ItemData>>(json) ?? new();
        _items = items.ToDictionary(i => i.Id);
    }

    private void SaveItems()
    {
        var path = ProjectSettings.GlobalizePath("res://Content/Items/items.json");
        var json = JsonSerializer.Serialize(_items.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(path, json);
    }

    private void OnItemSelected(int index)
    {
        _selectedItemId = _itemList.GetItemText(index);
        PopulateForm(_items[_selectedItemId]);
    }

    // Validation: check for duplicate IDs, missing names, invalid stat ranges
    private List<string> ValidateAll()
    {
        var errors = new List<string>();
        foreach (var item in _items.Values)
        {
            if (string.IsNullOrEmpty(item.Id)) errors.Add("Item has empty ID");
            if (string.IsNullOrEmpty(item.Name)) errors.Add($"Item {item.Id} has no name");
            if (item.HealAmount < 0) errors.Add($"Item {item.Id} has negative heal amount");
            if (item.AttackBonus < 0) errors.Add($"Item {item.Id} has negative attack bonus");
            if (item.MinFloor < 1) errors.Add($"Item {item.Id} has invalid min floor");
        }
        return errors;
    }
}
```

The Enemy Editor follows the same pattern, editing `Content/Enemies/enemies.json`.

---

## 4. Debug Console

### Activation
Toggle with backtick (`) key. Visible as a semi-transparent overlay at the bottom of the screen.

### UI

```
┌──────────────────────────────────────────────────┐
│ DEBUG CONSOLE                                     │
│ > teleport 10 5                                   │
│ Teleported player to (10, 5)                      │
│ > spawn skeleton 15 8                             │
│ Spawned skeleton at (15, 8)                       │
│ > god                                             │
│ God mode enabled                                  │
│ > _                                               │
└──────────────────────────────────────────────────┘
```

### Commands

| Command | Syntax | Effect |
|---------|--------|--------|
| `teleport` | `teleport <x> <y>` | Move player to position |
| `spawn` | `spawn <enemy_id> <x> <y>` | Spawn enemy at position |
| `give` | `give <item_id>` | Add item to player inventory |
| `god` | `god` | Toggle invincibility (HP never decreases) |
| `kill` | `kill` | Kill all visible enemies |
| `heal` | `heal [amount]` | Restore HP (default: full) |
| `reveal` | `reveal` | Reveal entire map (remove fog) |
| `floor` | `floor <n>` | Jump to floor N |
| `stats` | `stats` | Print player stats |
| `seed` | `seed` | Print current game seed |
| `fps` | `fps` | Toggle FPS counter |
| `help` | `help` | List all commands |

### DebugConsole.cs Implementation

```csharp
public partial class DebugConsole : Control
{
    private LineEdit _input;
    private RichTextLabel _output;
    private bool _godMode = false;
    private const int MaxOutputLines = 50;

    public bool GodMode => _godMode;

    public override void _Ready()
    {
        _input = GetNode<LineEdit>("Panel/Input");
        _output = GetNode<RichTextLabel>("Panel/Output");
        _input.TextSubmitted += OnCommandSubmitted;
        Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("debug_console"))
        {
            Visible = !Visible;
            if (Visible)
            {
                _input.GrabFocus();
                _input.Clear();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnCommandSubmitted(string command)
    {
        _input.Clear();
        PrintLine($"> {command}");

        var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var gm = GetNode<GameManager>("/root/GameManager");

        switch (parts[0].ToLower())
        {
            case "teleport":
                if (parts.Length == 3 && int.TryParse(parts[1], out int tx) && int.TryParse(parts[2], out int ty))
                {
                    ExecuteTeleport(gm, tx, ty);
                }
                else PrintLine("Usage: teleport <x> <y>");
                break;

            case "spawn":
                if (parts.Length == 4 && int.TryParse(parts[2], out int sx) && int.TryParse(parts[3], out int sy))
                {
                    ExecuteSpawn(gm, parts[1], sx, sy);
                }
                else PrintLine("Usage: spawn <enemy_id> <x> <y>");
                break;

            case "give":
                if (parts.Length == 2) ExecuteGive(gm, parts[1]);
                else PrintLine("Usage: give <item_id>");
                break;

            case "god":
                _godMode = !_godMode;
                PrintLine($"God mode {(_godMode ? "enabled" : "disabled")}");
                break;

            case "kill":
                ExecuteKillAll(gm);
                break;

            case "heal":
                int healAmt = parts.Length == 2 && int.TryParse(parts[1], out int h) ? h : -1;
                ExecuteHeal(gm, healAmt);
                break;

            case "reveal":
                // Emit signal to reveal all tiles
                PrintLine("Map revealed");
                break;

            case "floor":
                if (parts.Length == 2 && int.TryParse(parts[1], out int f))
                {
                    gm.TransitionFloor(f);
                    PrintLine($"Jumped to floor {f}");
                }
                else PrintLine("Usage: floor <n>");
                break;

            case "stats":
                ExecuteStats(gm);
                break;

            case "seed":
                PrintLine($"Seed: {gm.Seed}");
                break;

            case "help":
                PrintLine("Commands: teleport, spawn, give, god, kill, heal, reveal, floor, stats, seed, fps, help");
                break;

            default:
                PrintLine($"Unknown command: {parts[0]}");
                break;
        }
    }

    private void ExecuteTeleport(GameManager gm, int x, int y)
    {
        var pos = new Vec2I(x, y);
        if (gm.World == null) { PrintLine("No world loaded"); return; }
        if (!gm.World.IsInBounds(pos)) { PrintLine("Out of bounds"); return; }
        if (!gm.World.IsWalkable(pos)) { PrintLine("Not walkable"); return; }

        var player = gm.World.GetAllEntities().FirstOrDefault(e => e.IsPlayer);
        if (player == null) { PrintLine("No player"); return; }

        gm.World.MoveEntity(player.Id, pos);
        PrintLine($"Teleported to ({x}, {y})");
    }

    // ... similar for other commands

    private void PrintLine(string text)
    {
        _output.AppendText(text + "\n");
    }
}
```

### Security Note
Debug console is only available in debug builds. In release builds, the `debug_console` input action should be removed or the console script should check `OS.IsDebugBuild()`:

```csharp
public override void _UnhandledInput(InputEvent @event)
{
    if (!OS.IsDebugBuild()) return;
    // ...
}
```

---

## 5. Godot Editor Plugin Setup

### plugin.cfg

```ini
[plugin]
name="Roguelike Tools"
description="Map editor, item editor for roguelike content creation"
author="Godotussy"
version="1.0"
script="RoguelikeToolsPlugin.cs"
```

### RoguelikeToolsPlugin.cs

```csharp
#if TOOLS
using Godot;

namespace Godotussy;

[Tool]
public partial class RoguelikeToolsPlugin : EditorPlugin
{
    private Control? _mapEditorDock;
    private Control? _itemEditorDock;

    public override void _EnterTree()
    {
        // Add map editor as bottom panel
        var mapEditorScene = GD.Load<PackedScene>("res://Scenes/Tools/MapEditor.tscn");
        _mapEditorDock = mapEditorScene.Instantiate<Control>();
        AddControlToBottomPanel(_mapEditorDock, "Map Editor");

        var itemEditorScene = GD.Load<PackedScene>("res://Scenes/Tools/ItemEditor.tscn");
        _itemEditorDock = itemEditorScene.Instantiate<Control>();
        AddControlToBottomPanel(_itemEditorDock, "Item Editor");
    }

    public override void _ExitTree()
    {
        if (_mapEditorDock != null)
        {
            RemoveControlFromBottomPanel(_mapEditorDock);
            _mapEditorDock.QueueFree();
        }
        if (_itemEditorDock != null)
        {
            RemoveControlFromBottomPanel(_itemEditorDock);
            _itemEditorDock.QueueFree();
        }
    }
}
#endif
```

---

## 6. Test Scenarios (7)

| # | Test Scenario | Expected |
|---|---------------|----------|
| T1 | Map editor: paint floor tile | Canvas cell changes to Floor type and color |
| T2 | Map editor: save prefab | JSON file contains new prefab with correct dimensions and tile data |
| T3 | Map editor: load existing prefab | Canvas populated with prefab's tile layout |
| T4 | Item editor: edit item name | JSON file updated on save with new name |
| T5 | Item editor: validate catches missing ID | Validation returns error for empty ID |
| T6 | Debug console: teleport command | Player position changes to specified coordinates |
| T7 | Debug console: god mode toggle | GodMode property toggles, player takes no damage |

---

## 7. Dependencies

| Dependency | Provider | Notes |
|------------|----------|-------|
| `IWorldState` | Agent 2 | Debug console manipulates world state |
| `GameManager` | Agent 1 | Debug console accesses game state |
| Content JSON format | Agent 9 | Editors must match content schema |
| `EventBus` | Agent 1 | Debug commands emit rendering updates |
| Project structure | Agent 1 | Plugin paths must match project layout |
