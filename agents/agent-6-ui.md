# Agent 6: UI Agent — Detailed Specification

## Mission
Build the complete game UI using Godot Control nodes: HUD, inventory, combat log, character sheet, tooltips, menus, and full keyboard navigation. All UI reacts to EventBus signals — no direct simulation coupling.

---

## 1. Files to Create

| File | Purpose |
|------|---------|
| `Scripts/UI/HUD.cs` | HP bar, energy display, floor number, minimap |
| `Scripts/UI/InventoryUI.cs` | Grid-based inventory with keyboard/mouse interaction |
| `Scripts/UI/CombatLog.cs` | Scrolling message log with color coding |
| `Scripts/UI/CharacterSheet.cs` | Stats display panel |
| `Scripts/UI/Tooltip.cs` | Context-sensitive tooltip popups |
| `Scripts/UI/MainMenu.cs` | Title screen with New Game / Load / Quit |
| `Scripts/UI/PauseMenu.cs` | In-game pause overlay |
| `Scripts/UI/GameOverScreen.cs` | Death/victory screen |
| `Scripts/UI/InputHandler.cs` | Translates Godot input events into game actions |
| `Scenes/UI/HUD.tscn` | HUD scene |
| `Scenes/UI/InventoryUI.tscn` | Inventory scene |
| `Scenes/UI/CombatLog.tscn` | Combat log scene |
| `Scenes/UI/CharacterSheet.tscn` | Character sheet scene |
| `Scenes/UI/Tooltip.tscn` | Tooltip scene |
| `Scenes/UI/MainMenu.tscn` | Main menu scene |
| `Scenes/UI/PauseMenu.tscn` | Pause menu scene |
| `Scenes/UI/GameOverScreen.tscn` | Game over scene |

---

## 2. HUD Layout

### Visual Layout (1280×720 viewport)

```
┌──────────────────────────────────────────────────────────────┐
│ [HP: ████████░░ 75/100]  [Energy: 1000]  [Floor: 3]  [MAP] │  ← Top bar
│                                                              │
│                                                              │
│                      GAME VIEWPORT                           │
│                       (WorldView)                            │
│                                                              │
│                                                              │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│ Combat Log:                                                  │  ← Bottom panel
│ > You hit the Skeleton for 5 damage.                        │
│ > The Rat bites you for 2 damage.                           │
│ > You pick up a Health Potion.                              │
└──────────────────────────────────────────────────────────────┘
```

### HUD.cs Implementation

```csharp
public partial class HUD : Control
{
    private ProgressBar _hpBar;
    private Label _hpLabel;
    private Label _energyLabel;
    private Label _floorLabel;
    private SubViewport _minimapViewport; // optional minimap

    public override void _Ready()
    {
        _hpBar = GetNode<ProgressBar>("TopBar/HPBar");
        _hpLabel = GetNode<Label>("TopBar/HPLabel");
        _energyLabel = GetNode<Label>("TopBar/EnergyLabel");
        _floorLabel = GetNode<Label>("TopBar/FloorLabel");

        var eventBus = GetNode<EventBus>("/root/EventBus");
        eventBus.HPChanged += OnHPChanged;
        eventBus.FloorChanged += OnFloorChanged;
        eventBus.TurnCompleted += OnTurnCompleted;
    }

    private void OnHPChanged(int entityId, int current, int max)
    {
        // Only update for player (entityId == 1 by convention, or check IsPlayer)
        _hpBar.MaxValue = max;
        _hpBar.Value = current;
        _hpLabel.Text = $"HP: {current}/{max}";

        // Color coding
        float ratio = (float)current / max;
        _hpBar.Modulate = ratio switch
        {
            > 0.6f => Colors.Green,
            > 0.3f => Colors.Yellow,
            _ => Colors.Red
        };
    }

    private void OnFloorChanged(int newFloor)
    {
        _floorLabel.Text = $"Floor: {newFloor}";
    }

    private void OnTurnCompleted()
    {
        // Update energy display from scheduler
        var gm = GetNode<GameManager>("/root/GameManager");
        if (gm.Scheduler != null && gm.World != null)
        {
            var player = gm.World.GetAllEntities().FirstOrDefault(e => e.IsPlayer);
            if (player != null)
                _energyLabel.Text = $"Energy: {gm.Scheduler.GetEnergy(player.Id)}";
        }
    }
}
```

### HP Bar Scene Structure (HUD.tscn)

```
HUD (Control) — anchor: full rect
├── TopBar (HBoxContainer) — anchor: top, height: 32px
│   ├── HPBar (ProgressBar) — min_size: (200, 24)
│   ├── HPLabel (Label) — "HP: 100/100"
│   ├── EnergyLabel (Label) — "Energy: 1000"
│   ├── FloorLabel (Label) — "Floor: 1"
│   └── MinimapButton (TextureButton) — optional minimap toggle
└── StatusEffects (HBoxContainer) — anchor: top-right
    └── (dynamically added effect icons)
```

### Minimap (Optional MVP Feature)
- Small viewport (150×100) in top-right corner.
- Renders full map at 1px per tile.
- Player = green dot, enemies = red dots (only visible ones), stairs = yellow.
- Toggle with Tab key.

---

## 3. Inventory System

### Layout

```
┌─────────────────────────────────────┐
│         INVENTORY (I to close)      │
├─────────────────────────────────────┤
│ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│ │ ! │ │ ] │ │ / │ │   │ │   │    │  Row 1
│ └───┘ └───┘ └───┘ └───┘ └───┘    │
│ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│ │   │ │   │ │   │ │   │ │   │    │  Row 2
│ └───┘ └───┘ └───┘ └───┘ └───┘    │
│ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│ │   │ │   │ │   │ │   │ │   │    │  Row 3
│ └───┘ └───┘ └───┘ └───┘ └───┘    │
│ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐    │
│ │   │ │   │ │   │ │   │ │   │    │  Row 4
│ └───┘ └───┘ └───┘ └───┘ └───┘    │
├─────────────────────────────────────┤
│ [U]se  [D]rop  [E]quip  [Esc]Close│
└─────────────────────────────────────┘
```

### Grid: 5 columns × 4 rows = 20 slots (matching `MaxInventorySize`).

### InventoryUI.cs Implementation

```csharp
public partial class InventoryUI : Control
{
    private GridContainer _grid;
    private Label _itemDescription;
    private int _selectedIndex = 0;
    private List<string> _items = new();

    private const int Columns = 5;
    private const int Rows = 4;

    public override void _Ready()
    {
        _grid = GetNode<GridContainer>("Panel/Grid");
        _itemDescription = GetNode<Label>("Panel/Description");
        Visible = false;

        var eventBus = GetNode<EventBus>("/root/EventBus");
        eventBus.InventoryChanged += OnInventoryChanged;

        // Create slot nodes
        for (int i = 0; i < Columns * Rows; i++)
        {
            var slot = new Panel();
            slot.CustomMinimumSize = new Vector2(48, 48);
            var icon = new TextureRect { Name = "Icon", StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            var highlight = new ColorRect { Name = "Highlight", Color = new Color(1, 1, 0, 0.3f), Visible = false };
            highlight.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            slot.AddChild(highlight);
            slot.AddChild(icon);
            _grid.AddChild(slot);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;

        if (@event.IsActionPressed("inventory"))
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Keyboard navigation
        if (@event is InputEventKey key && key.Pressed)
        {
            switch (key.PhysicalKeycode)
            {
                case Key.Up:    MoveSelection(0, -1); break;
                case Key.Down:  MoveSelection(0, 1); break;
                case Key.Left:  MoveSelection(-1, 0); break;
                case Key.Right: MoveSelection(1, 0); break;
                case Key.U:     UseSelected(); break;
                case Key.D:     DropSelected(); break;
                case Key.E:     EquipSelected(); break;
                case Key.Escape: Close(); break;
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private void MoveSelection(int dx, int dy)
    {
        int col = _selectedIndex % Columns;
        int row = _selectedIndex / Columns;
        col = Math.Clamp(col + dx, 0, Columns - 1);
        row = Math.Clamp(row + dy, 0, Rows - 1);
        _selectedIndex = row * Columns + col;
        UpdateHighlight();
        UpdateDescription();
    }

    public void Open(List<string> items)
    {
        _items = items;
        _selectedIndex = 0;
        Visible = true;
        RefreshGrid();
        UpdateHighlight();
        // Pause game input while inventory is open
    }

    public void Close()
    {
        Visible = false;
    }

    private void UseSelected()
    {
        if (_selectedIndex >= _items.Count) return;
        var itemId = _items[_selectedIndex];
        var eventBus = GetNode<EventBus>("/root/EventBus");
        eventBus.EmitSignal(EventBus.SignalName.PlayerActionSubmitted,
            (int)ActionType.UseItem, 0, 0, itemId);
        Close();
    }

    private void DropSelected()
    {
        if (_selectedIndex >= _items.Count) return;
        var itemId = _items[_selectedIndex];
        var eventBus = GetNode<EventBus>("/root/EventBus");
        eventBus.EmitSignal(EventBus.SignalName.PlayerActionSubmitted,
            (int)ActionType.DropItem, 0, 0, itemId);
        Close();
    }
}
```

---

## 4. Combat Log

### Visual: 4-line scrolling text area at bottom of screen.

```csharp
public partial class CombatLog : Control
{
    private RichTextLabel _logText;
    private const int MaxMessages = 100;
    private readonly Queue<string> _messages = new();

    public override void _Ready()
    {
        _logText = GetNode<RichTextLabel>("Panel/LogText");
        _logText.BbcodeEnabled = true;
        _logText.ScrollFollowing = true;

        var eventBus = GetNode<EventBus>("/root/EventBus");
        eventBus.LogMessage += OnLogMessage;
        eventBus.DamageDealt += OnDamageDealt;
        eventBus.EntityDied += OnEntityDied;
        eventBus.ItemPickedUp += OnItemPickedUp;
        eventBus.StatusEffectApplied += OnStatusEffectApplied;
    }

    private void OnLogMessage(string message, int colorType)
    {
        string color = colorType switch
        {
            0 => "white",       // normal
            1 => "yellow",      // player action
            2 => "red",         // damage taken
            3 => "green",       // healing / positive
            4 => "gray",        // system/info
            5 => "orange",      // warning
            6 => "cyan",        // item
            _ => "white"
        };

        AddMessage($"[color={color}]{EscapeBBCode(message)}[/color]");
    }

    private void OnDamageDealt(int attackerId, int defenderId, int amount, int damageType)
    {
        // Formatting handled by whoever emits LogMessage
        // This is a fallback if LogMessage wasn't emitted
    }

    private void AddMessage(string bbcodeMessage)
    {
        _messages.Enqueue(bbcodeMessage);
        while (_messages.Count > MaxMessages)
            _messages.Dequeue();

        _logText.Clear();
        foreach (var msg in _messages)
        {
            _logText.AppendText(msg + "\n");
        }
    }

    private static string EscapeBBCode(string text)
    {
        return text.Replace("[", "[lb]"); // prevent BBCode injection
    }
}
```

### Color Coding Scheme

| Color | Used For |
|-------|----------|
| White | General messages |
| Yellow | Player actions ("You move north") |
| Red | Damage taken, enemy attacks |
| Green | Healing, positive effects |
| Gray | System messages, floor transitions |
| Orange | Warnings (low HP, status effects) |
| Cyan | Item interactions |

---

## 5. Character Sheet

```
┌─────────────────────────────────────┐
│        CHARACTER SHEET (C)          │
├─────────────────────────────────────┤
│  Name: Player                       │
│  Floor: 3                           │
│                                     │
│  HP:      75 / 100                  │
│  Attack:  12 (+3 weapon)            │
│  Defense: 8  (+2 armor)             │
│  Speed:   100                       │
│                                     │
│  Weapon: Iron Sword (+3 ATK)        │
│  Armor:  Leather Mail (+2 DEF)      │
│                                     │
│  Status Effects:                    │
│  - Poison (3 turns remaining)       │
│  - Shield x2 (+6 DEF)              │
│                                     │
│  Press Esc to close                 │
└─────────────────────────────────────┘
```

### CharacterSheet.cs

```csharp
public partial class CharacterSheet : Control
{
    public void Open(EntityData player, IContentDB contentDB)
    {
        Visible = true;
        // Populate labels from player data
        GetNode<Label>("Name").Text = $"Name: {player.Name}";
        GetNode<Label>("HP").Text = $"HP: {player.HP} / {player.MaxHP}";

        int weaponBonus = 0;
        if (player.EquippedWeapon != null)
        {
            var weapon = contentDB.GetItem(player.EquippedWeapon);
            weaponBonus = weapon?.AttackBonus ?? 0;
        }

        int armorBonus = 0;
        if (player.EquippedArmor != null)
        {
            var armor = contentDB.GetItem(player.EquippedArmor);
            armorBonus = armor?.DefenseBonus ?? 0;
        }

        GetNode<Label>("Attack").Text = $"Attack: {player.Attack} (+{weaponBonus} weapon)";
        GetNode<Label>("Defense").Text = $"Defense: {player.Defense} (+{armorBonus} armor)";
        GetNode<Label>("Speed").Text = $"Speed: {player.Speed}";

        // Status effects
        var effectsText = "";
        foreach (var effect in player.StatusEffects)
        {
            effectsText += $"- {effect.Type} ({effect.RemainingTurns} turns)";
            if (effect.Stacks > 1) effectsText += $" x{effect.Stacks}";
            effectsText += "\n";
        }
        GetNode<Label>("StatusEffects").Text = effectsText;
    }
}
```

---

## 6. Tooltips

Appear when hovering over:
- Inventory items (show name, description, stats)
- Enemies in view (show name, HP, status effects)
- UI elements (show keyboard shortcut)

```csharp
public partial class Tooltip : Control
{
    private Label _title;
    private Label _body;

    public void ShowItemTooltip(ItemData item, Vector2 screenPos)
    {
        _title.Text = item.Name;
        var lines = new List<string> { item.Description };
        if (item.AttackBonus > 0) lines.Add($"+{item.AttackBonus} Attack");
        if (item.DefenseBonus > 0) lines.Add($"+{item.DefenseBonus} Defense");
        if (item.HealAmount > 0) lines.Add($"Heals {item.HealAmount} HP");
        if (item.Consumable) lines.Add("(Consumable)");
        _body.Text = string.Join("\n", lines);
        Position = ClampToScreen(screenPos);
        Visible = true;
    }

    public void ShowEnemyTooltip(EntityData enemy, Vector2 screenPos)
    {
        _title.Text = enemy.Name;
        _body.Text = $"HP: {enemy.HP}/{enemy.MaxHP}\nATK: {enemy.Attack} DEF: {enemy.Defense}";
        Position = ClampToScreen(screenPos);
        Visible = true;
    }

    public void Hide() { Visible = false; }

    private Vector2 ClampToScreen(Vector2 pos)
    {
        var viewport = GetViewportRect().Size;
        var size = Size;
        pos.X = Math.Min(pos.X, viewport.X - size.X);
        pos.Y = Math.Min(pos.Y, viewport.Y - size.Y);
        return pos;
    }
}
```

---

## 7. Menu System

### 7.1 Main Menu

```
┌─────────────────────────┐
│                         │
│    GODOTUSSY ROGUELIKE  │
│                         │
│    ► New Game           │
│      Load Game          │
│      Quit               │
│                         │
└─────────────────────────┘
```

- Arrow keys / W/S to navigate
- Enter to select
- New Game: prompt for seed (or random)
- Load Game: show save slot picker (3 slots)
- Quit: exit application

### 7.2 Pause Menu

```
┌─────────────────────────┐
│      PAUSED             │
│                         │
│    ► Resume             │
│      Save Game          │
│      Character Sheet    │
│      Main Menu          │
│      Quit               │
└─────────────────────────┘
```

- Triggered by Escape key
- Semi-transparent overlay over game
- Blocks game input while visible

### 7.3 Game Over Screen

```
┌─────────────────────────┐
│     YOU DIED            │
│                         │
│  Floor Reached: 5       │
│  Enemies Killed: 23     │
│  Turns Taken: 412       │
│                         │
│    ► Try Again          │
│      Main Menu          │
└─────────────────────────┘
```

### Menu Implementation Pattern

```csharp
// All menus follow the same keyboard navigation pattern:
public abstract partial class MenuBase : Control
{
    protected int _selectedIndex = 0;
    protected int _itemCount;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;

        if (@event.IsActionPressed("move_up") || @event.IsActionPressed("ui_up"))
        {
            _selectedIndex = (_selectedIndex - 1 + _itemCount) % _itemCount;
            UpdateSelection();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("move_down") || @event.IsActionPressed("ui_down"))
        {
            _selectedIndex = (_selectedIndex + 1) % _itemCount;
            UpdateSelection();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed("ui_accept"))
        {
            OnItemSelected(_selectedIndex);
            GetViewport().SetInputAsHandled();
        }
    }

    protected abstract void UpdateSelection();
    protected abstract void OnItemSelected(int index);
}
```

---

## 8. Input Handler

Translates Godot input actions into game actions. Lives as a separate script that emits signals on the EventBus.

```csharp
public partial class InputHandler : Node
{
    private bool _inputEnabled = true;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_inputEnabled) return;
        var gm = GetNode<GameManager>("/root/GameManager");
        if (gm.CurrentState != GameManager.GameState.Playing) return;

        // Movement
        Direction? dir = null;
        if (@event.IsActionPressed("move_up")) dir = Direction.N;
        else if (@event.IsActionPressed("move_down")) dir = Direction.S;
        else if (@event.IsActionPressed("move_left")) dir = Direction.W;
        else if (@event.IsActionPressed("move_right")) dir = Direction.E;
        else if (@event.IsActionPressed("move_up_left")) dir = Direction.NW;
        else if (@event.IsActionPressed("move_up_right")) dir = Direction.NE;
        else if (@event.IsActionPressed("move_down_left")) dir = Direction.SW;
        else if (@event.IsActionPressed("move_down_right")) dir = Direction.SE;

        if (dir.HasValue)
        {
            var eventBus = GetNode<EventBus>("/root/EventBus");
            var delta = Vec2I.FromDirection(dir.Value);
            eventBus.EmitSignal(EventBus.SignalName.PlayerActionSubmitted,
                (int)ActionType.Move, delta.X, delta.Y, "");
            GetViewport().SetInputAsHandled();
            return;
        }

        // Wait
        if (@event.IsActionPressed("wait_turn"))
        {
            var eventBus = GetNode<EventBus>("/root/EventBus");
            eventBus.EmitSignal(EventBus.SignalName.PlayerActionSubmitted,
                (int)ActionType.Wait, 0, 0, "");
            GetViewport().SetInputAsHandled();
        }

        // Pickup
        if (@event.IsActionPressed("pickup"))
        {
            var eventBus = GetNode<EventBus>("/root/EventBus");
            eventBus.EmitSignal(EventBus.SignalName.PlayerActionSubmitted,
                (int)ActionType.PickUp, 0, 0, "");
            GetViewport().SetInputAsHandled();
        }

        // Inventory toggle
        if (@event.IsActionPressed("inventory"))
        {
            // Toggle inventory UI
            GetViewport().SetInputAsHandled();
        }

        // Stairs
        if (@event.IsActionPressed("use_stairs"))
        {
            var eventBus = GetNode<EventBus>("/root/EventBus");
            eventBus.EmitSignal(EventBus.SignalName.PlayerActionSubmitted,
                (int)ActionType.UseStairs, 0, 0, "");
            GetViewport().SetInputAsHandled();
        }

        // Pause
        if (@event.IsActionPressed("pause_menu"))
        {
            // Toggle pause menu
            GetViewport().SetInputAsHandled();
        }
    }

    public void SetInputEnabled(bool enabled) { _inputEnabled = enabled; }
}
```

---

## 9. Keyboard Navigation Summary

| Context | Key | Action |
|---------|-----|--------|
| Gameplay | WASD / Arrows / Numpad | Move player |
| Gameplay | Numpad 5 / Period | Wait |
| Gameplay | G | Pick up item |
| Gameplay | I | Open inventory |
| Gameplay | Enter | Use stairs |
| Gameplay | Escape | Pause menu |
| Gameplay | C | Character sheet |
| Gameplay | Backtick | Debug console |
| Inventory | Arrows | Navigate grid |
| Inventory | U | Use selected item |
| Inventory | D | Drop selected item |
| Inventory | E | Equip selected item |
| Inventory | Escape / I | Close inventory |
| Menus | Up/Down | Navigate options |
| Menus | Enter | Select option |
| Menus | Escape | Back / Close |

---

## 10. Test Scenarios (12)

| # | Test Scenario | Expected |
|---|---------------|----------|
| U1 | HP bar updates on HPChanged signal | Bar value and label match emitted values |
| U2 | HP bar color changes at thresholds | Green > 60%, Yellow 30-60%, Red < 30% |
| U3 | Floor label updates on FloorChanged | Label shows new floor number |
| U4 | Combat log adds message with color | BBCode text appears in log with correct color |
| U5 | Combat log scrolls with max 100 messages | Old messages removed, newest at bottom |
| U6 | BBCode injection in log message | Square brackets escaped, no formatting exploit |
| U7 | Inventory opens with I key | InventoryUI becomes visible |
| U8 | Inventory keyboard navigation wraps | Moving past last column stays in bounds |
| U9 | Use item from inventory | PlayerActionSubmitted signal emitted with item ID |
| U10 | Pause menu opens with Escape | Pause overlay visible, game state set to Paused |
| U11 | Main menu New Game starts game | Transitions to Playing state |
| U12 | Tooltip shows on inventory item hover | Tooltip visible with correct item stats |

---

## 11. Dependencies

| Dependency | Provider | Notes |
|------------|----------|-------|
| `EventBus` signals | Agent 1 | All UI updates via signals |
| `EntityData` | Agent 1 | Player stats for HUD/character sheet |
| `ItemData` | Agent 1/9 | Item info for inventory/tooltips |
| `IContentDB` | Agent 1 | Resolve item/enemy names |
| `GameManager.CurrentState` | Agent 1 | UI state gating |
| Input actions | Agent 1 (project.godot) | Input map must be set up |
