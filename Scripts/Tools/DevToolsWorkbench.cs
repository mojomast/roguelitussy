using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class DevToolsWorkbench : Control
{
    public enum WorkshopMode
    {
        Rooms,
        Items,
        Enemies,
        Commands,
    }

    private const float PanelWidth = 980f;
    private const float PanelHeight = 660f;
    private const float PanelPadding = 18f;

    private readonly MapEditor _mapEditor = new();
    private readonly ItemEditor _itemEditor = new();
    private Panel? _panel;
    private Label? _label;
    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;
    private string? _contentDirectory;
    private string _statusText = "Workshop ready.";
    private int _selectedIndex;
    private int _roomBrowseIndex;
    private int _cursorX;
    private int _cursorY;
    private IReadOnlyList<string> _roomIds = Array.Empty<string>();

    public DevToolsWorkbench()
    {
        Name = "DevToolsWorkbench";
        Visible = false;
        ActiveMode = WorkshopMode.Rooms;
    }

    public WorkshopMode ActiveMode { get; private set; }

    public string SummaryText { get; private set; } = string.Empty;

    public event Action? DebugConsoleRequested;

    public event Action? PlaytestStarted;

    public event Action<IContentDatabase>? RuntimeContentReloaded;

    public override void _Ready()
    {
        EnsureVisuals();
        Refresh();
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content, string? contentDirectory = null)
    {
        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content;
        _contentDirectory = contentDirectory;
        ReloadData();
    }

    public void Open()
    {
        Visible = true;
        ReloadData();
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        RefreshVisualState();
    }

    public void Toggle()
    {
        if (Visible)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public bool HandleKey(Key key)
    {
        if (!Visible)
        {
            return false;
        }

        switch (key)
        {
            case Key.Escape:
            case Key.T:
                Close();
                return true;
            case Key.Tab:
                CycleMode(1);
                return true;
            case Key.Up:
            case Key.W:
                MoveSelection(-1);
                return true;
            case Key.Down:
            case Key.S:
                MoveSelection(1);
                return true;
            case Key.Left:
            case Key.A:
            case Key.Minus:
                AdjustSelected(-1);
                return true;
            case Key.Right:
            case Key.D:
            case Key.Plus:
                AdjustSelected(1);
                return true;
            case Key.Enter:
                ActivateSelected();
                return true;
            default:
                return false;
        }
    }

    public void CreateRoomDraft()
    {
        var roomId = NextStableId("custom_room", _roomIds);
        _mapEditor.CreateDraft(roomId);
        _roomIds = _mapEditor.GetRoomIds(_contentDirectory);
        _statusText = _mapEditor.StatusText;
        ClampCursor();
        Refresh();
    }

    public void SaveCurrentRoomDraft()
    {
        _mapEditor.SavePrefab(_contentDirectory);
        _roomIds = _mapEditor.GetRoomIds(_contentDirectory);
        _roomBrowseIndex = Math.Max(0, Array.IndexOf(_roomIds.ToArray(), _mapEditor.RoomId));
        _statusText = _mapEditor.StatusText;
        Refresh();
    }

    public void CreateItemDraft()
    {
        _itemEditor.CreateNextItem();
        _statusText = _itemEditor.StatusText;
        Refresh();
    }

    public void SaveItemsDocument()
    {
        _itemEditor.SaveItems(_contentDirectory);
        _statusText = _itemEditor.StatusText;
        Refresh();
    }

    public void CreateEnemyDraft()
    {
        _itemEditor.CreateNextEnemy();
        _statusText = _itemEditor.StatusText;
        Refresh();
    }

    public void SaveEnemiesDocument()
    {
        _itemEditor.SaveEnemies(_contentDirectory);
        _statusText = _itemEditor.StatusText;
        Refresh();
    }

    public IReadOnlyList<string> ReloadRuntimeContent()
    {
        try
        {
            var resolvedDirectory = ToolPaths.ResolveContentDirectory(_contentDirectory);
            var loader = ContentLoader.LoadFromDirectory(resolvedDirectory, throwOnValidationErrors: false);
            var errors = loader.ValidationErrors;

            _gameManager?.SetRuntimeContent(loader);
            _eventBus?.EmitLogMessage($"Reloaded runtime content from {loader.ContentDirectory}.");
            _content = loader;
            RuntimeContentReloaded?.Invoke(loader);
            ReloadData();
            _statusText = errors.Count == 0
                ? "Runtime content reloaded from disk."
                : $"Runtime content reloaded with {errors.Count} validation warning(s): {string.Join(" | ", errors.Take(2))}";
            Refresh();
            return errors;
        }
        catch (Exception ex)
        {
            var errors = new[] { ex.Message };
            _statusText = ex.Message;
            Refresh();
            return errors;
        }
    }

    public bool PlaytestCurrentRoomDraft()
    {
        try
        {
            var world = BuildPlaytestWorld();
            _gameManager?.LoadToolWorld(world, $"Loaded playtest room '{_mapEditor.RoomId}'.");
            _statusText = $"Playtesting '{_mapEditor.RoomId}'.";
            Close();
            PlaytestStarted?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _statusText = ex.Message;
            Refresh();
            return false;
        }
    }

    public bool DropSelectedItemAtPlayer()
    {
        EnsureItemSelection();
        var world = _gameManager?.World;
        var player = world?.Player;
        var item = _itemEditor.SelectedItem;
        if (world is null || player is null)
        {
            _statusText = "No active run to drop an item into.";
            Refresh();
            return false;
        }

        if (item is null)
        {
            _statusText = "No item is selected.";
            Refresh();
            return false;
        }

        if (_gameManager?.Content is null || !_gameManager.Content.TryGetItemTemplate(item.Id, out var template))
        {
            _statusText = $"Item '{item.Id}' is not in the runtime database. Save and reload content first.";
            Refresh();
            return false;
        }

        var instance = new ItemInstance
        {
            TemplateId = template.TemplateId,
            CurrentCharges = template.MaxCharges > 0 ? template.MaxCharges : 0,
            StackCount = Math.Max(1, template.MaxStack > 1 ? template.MaxStack : 1),
            IsIdentified = true,
        };
        world.DropItem(player.Position, instance);
        _eventBus?.EmitItemDropped(player.Id, instance, player.Position);
        _statusText = $"Dropped {template.DisplayName} at the player position.";
        Refresh();
        return true;
    }

    public bool SpawnSelectedEnemyNearPlayer()
    {
        EnsureEnemySelection();
        var world = _gameManager?.World;
        var player = world?.Player;
        var enemy = _itemEditor.SelectedEnemy;
        if (world is null || player is null)
        {
            _statusText = "No active run to spawn an enemy into.";
            Refresh();
            return false;
        }

        if (enemy is null)
        {
            _statusText = "No enemy is selected.";
            Refresh();
            return false;
        }

        var spawnPosition = FindNearbyOpenTile(world, player.Position);
        if (spawnPosition == Roguelike.Core.Position.Invalid)
        {
            _statusText = "Could not find a free tile near the player.";
            Refresh();
            return false;
        }

        var entity = CreateEnemyEntity(enemy, spawnPosition);
        world.AddEntity(entity);
        _eventBus?.EmitEntitySpawned(entity);
        _eventBus?.EmitHPChanged(entity.Id, entity.Stats.HP, entity.Stats.MaxHP);
        _statusText = $"Spawned {enemy.Name} at {spawnPosition.X},{spawnPosition.Y}.";
        Refresh();
        return true;
    }

    private void ReloadData()
    {
        try
        {
            _itemEditor.Load(_contentDirectory);
        }
        catch (Exception ex)
        {
            _statusText = $"Content load failed: {ex.Message}";
        }

        try
        {
            _roomIds = _mapEditor.GetRoomIds(_contentDirectory);
            if (_roomIds.Count > 0)
            {
                _roomBrowseIndex = Math.Clamp(_roomBrowseIndex, 0, _roomIds.Count - 1);
            }
            else
            {
                _roomBrowseIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _roomIds = Array.Empty<string>();
            _statusText = $"Room load failed: {ex.Message}";
        }

        ClampCursor();
        Refresh();
    }

    private void CycleMode(int delta)
    {
        var modes = Enum.GetValues<WorkshopMode>();
        ActiveMode = modes[(Array.IndexOf(modes, ActiveMode) + delta + modes.Length) % modes.Length];
        _selectedIndex = 0;
        Refresh();
    }

    private void MoveSelection(int delta)
    {
        var optionCount = GetOptionCount();
        _selectedIndex = (_selectedIndex + delta % optionCount + optionCount) % optionCount;
        Refresh();
    }

    private int GetOptionCount()
    {
        return ActiveMode switch
        {
            WorkshopMode.Rooms => 13,
            WorkshopMode.Items => 13,
            WorkshopMode.Enemies => 15,
            _ => 5,
        };
    }

    private void AdjustSelected(int delta)
    {
        switch (ActiveMode)
        {
            case WorkshopMode.Rooms:
                AdjustRoom(delta);
                break;
            case WorkshopMode.Items:
                AdjustItem(delta);
                break;
            case WorkshopMode.Enemies:
                AdjustEnemy(delta);
                break;
        }

        Refresh();
    }

    private void ActivateSelected()
    {
        switch (ActiveMode)
        {
            case WorkshopMode.Rooms:
                ActivateRoom();
                break;
            case WorkshopMode.Items:
                ActivateItem();
                break;
            case WorkshopMode.Enemies:
                ActivateEnemy();
                break;
            case WorkshopMode.Commands:
                ActivateCommand();
                break;
        }

        Refresh();
    }

    private void AdjustRoom(int delta)
    {
        switch (_selectedIndex)
        {
            case 0:
                if (_roomIds.Count > 0)
                {
                    _roomBrowseIndex = (_roomBrowseIndex + delta % _roomIds.Count + _roomIds.Count) % _roomIds.Count;
                    _statusText = $"Browse room '{_roomIds[_roomBrowseIndex]}'.";
                }

                break;
            case 2:
                _mapEditor.ResizeCanvas(Math.Max(4, _mapEditor.CanvasWidth + delta), _mapEditor.CanvasHeight);
                ClampCursor();
                _statusText = _mapEditor.StatusText;
                break;
            case 3:
                _mapEditor.ResizeCanvas(_mapEditor.CanvasWidth, Math.Max(4, _mapEditor.CanvasHeight + delta));
                ClampCursor();
                _statusText = _mapEditor.StatusText;
                break;
            case 4:
                CycleBrush(delta);
                break;
            case 5:
                CycleTool(delta);
                break;
            case 6:
                _cursorX = Math.Clamp(_cursorX + delta, 0, _mapEditor.CanvasWidth - 1);
                break;
            case 7:
                _cursorY = Math.Clamp(_cursorY + delta, 0, _mapEditor.CanvasHeight - 1);
                break;
        }
    }

    private void ActivateRoom()
    {
        switch (_selectedIndex)
        {
            case 0:
                if (_roomIds.Count > 0 && _mapEditor.LoadPrefab(_roomIds[_roomBrowseIndex], _contentDirectory))
                {
                    ClampCursor();
                    _statusText = _mapEditor.StatusText;
                }

                break;
            case 1:
                CreateRoomDraft();
                break;
            case 8:
                _mapEditor.ApplyCurrentToolAt(_cursorX, _cursorY);
                _statusText = _mapEditor.StatusText;
                break;
            case 9:
                PlaytestCurrentRoomDraft();
                break;
            case 10:
                SaveCurrentRoomDraft();
                break;
            case 11:
                _mapEditor.ResetCanvas('#');
                _statusText = _mapEditor.StatusText;
                break;
            case 12:
                var errors = _mapEditor.ValidateDraft();
                _statusText = errors.Count == 0 ? _mapEditor.StatusText : string.Join(" | ", errors);
                break;
        }
    }

    private void AdjustItem(int delta)
    {
        EnsureItemSelection();
        switch (_selectedIndex)
        {
            case 0:
                _itemEditor.CycleSelectedItem(delta);
                break;
            case 2:
                _itemEditor.CycleSelectedItemType(delta);
                break;
            case 3:
                _itemEditor.CycleSelectedItemSlot(delta);
                break;
            case 4:
                _itemEditor.CycleSelectedItemRarity(delta);
                break;
            case 5:
                _itemEditor.AdjustSelectedItemStat("attack", delta);
                break;
            case 6:
                _itemEditor.AdjustSelectedItemStat("defense", delta);
                break;
            case 7:
                _itemEditor.AdjustSelectedItemValue(delta * 5);
                break;
            case 8:
                _itemEditor.AdjustSelectedItemMaxStack(delta);
                break;
            case 9:
                _itemEditor.ToggleSelectedItemStackable();
                break;
            case 10:
                break;
        }

        _statusText = _itemEditor.StatusText;
    }

    private void ActivateItem()
    {
        switch (_selectedIndex)
        {
            case 1:
                CreateItemDraft();
                break;
            case 9:
                _itemEditor.ToggleSelectedItemStackable();
                break;
            case 10:
                DropSelectedItemAtPlayer();
                return;
            case 11:
                SaveItemsDocument();
                break;
            case 12:
                var errors = _itemEditor.ValidateAll();
                _statusText = errors.Count == 0 ? "Content validation passed." : string.Join(" | ", errors.Take(3));
                return;
        }

        _statusText = _itemEditor.StatusText;
    }

    private void AdjustEnemy(int delta)
    {
        EnsureEnemySelection();
        switch (_selectedIndex)
        {
            case 0:
                _itemEditor.CycleSelectedEnemy(delta);
                break;
            case 2:
                _itemEditor.CycleSelectedEnemyAiType(delta);
                break;
            case 3:
                _itemEditor.CycleSelectedEnemyFaction(delta);
                break;
            case 4:
                _itemEditor.AdjustSelectedEnemyStat("hp", delta * 2);
                break;
            case 5:
                _itemEditor.AdjustSelectedEnemyStat("attack", delta);
                break;
            case 6:
                _itemEditor.AdjustSelectedEnemyStat("defense", delta);
                break;
            case 7:
                _itemEditor.AdjustSelectedEnemyStat("speed", delta);
                break;
            case 8:
                _itemEditor.AdjustSelectedEnemyStat("fov", delta);
                break;
            case 9:
                _itemEditor.AdjustSelectedEnemyDepth(false, delta);
                break;
            case 10:
                _itemEditor.AdjustSelectedEnemyDepth(true, delta);
                break;
            case 11:
                _itemEditor.AdjustSelectedEnemySpawnWeight(delta);
                break;
            case 12:
                break;
        }

        _statusText = _itemEditor.StatusText;
    }

    private void ActivateEnemy()
    {
        switch (_selectedIndex)
        {
            case 1:
                CreateEnemyDraft();
                break;
            case 12:
                SpawnSelectedEnemyNearPlayer();
                return;
            case 13:
                SaveEnemiesDocument();
                break;
            case 14:
                var errors = _itemEditor.ValidateAll();
                _statusText = errors.Count == 0 ? "Content validation passed." : string.Join(" | ", errors.Take(3));
                return;
        }

        _statusText = _itemEditor.StatusText;
    }

    private void ActivateCommand()
    {
        switch (_selectedIndex)
        {
            case 0:
                ReloadData();
                _statusText = "Tool data reloaded from Content/.";
                break;
            case 1:
                ReloadRuntimeContent();
                return;
            case 2:
                var errors = _itemEditor.ValidateAll();
                var roomErrors = _mapEditor.ValidateDraft();
                var totalErrors = errors.Count + roomErrors.Count;
                _statusText = totalErrors == 0 ? "All visible tooling validations passed." : $"Validation reported {totalErrors} issue(s).";
                break;
            case 3:
                DebugConsoleRequested?.Invoke();
                _statusText = "Debug console opened.";
                break;
            case 4:
                Close();
                break;
        }
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _label is not null)
        {
            return;
        }

        Size = ResolveViewportSize();
        ZIndex = 110;
        _panel = new Panel
        {
            Name = "Panel",
            Size = new Vector2(PanelWidth, PanelHeight),
        };
        _label = new Label
        {
            Name = "Label",
            Position = new Vector2(PanelPadding, PanelPadding),
            Size = new Vector2(PanelWidth - (PanelPadding * 2f), PanelHeight - (PanelPadding * 2f)),
        };
        _panel.AddChild(_label);
        AddChild(_panel);
    }

    private void Refresh()
    {
        EnsureVisuals();

        var builder = new StringBuilder();
        builder.AppendLine("DEVELOPER WORKSHOP");
        builder.AppendLine(BuildModeHeader());
        builder.AppendLine();
        builder.AppendLine(BuildBodyText());
        builder.AppendLine();

        var options = BuildOptions();
        for (var index = 0; index < options.Count; index++)
        {
            builder.Append(index == _selectedIndex ? "> " : "  ");
            builder.AppendLine(options[index]);
        }

        builder.AppendLine();
        builder.AppendLine($"Status: {_statusText}");
        builder.Append("Controls: Tab mode  Up/Down select  Left/Right or +/- adjust  Enter apply  Esc/T close");
        SummaryText = builder.ToString().TrimEnd();
        RefreshVisualState();
    }

    private string BuildModeHeader()
    {
        return string.Join(
            " | ",
            Enum.GetValues<WorkshopMode>().Select(mode => mode == ActiveMode ? $"[{mode}]" : mode.ToString()));
    }

    private string BuildBodyText()
    {
        return ActiveMode switch
        {
            WorkshopMode.Rooms => BuildRoomBody(),
            WorkshopMode.Items => BuildItemBody(),
            WorkshopMode.Enemies => BuildEnemyBody(),
            _ => BuildCommandsBody(),
        };
    }

    private IReadOnlyList<string> BuildOptions()
    {
        return ActiveMode switch
        {
            WorkshopMode.Rooms => BuildRoomOptions(),
            WorkshopMode.Items => BuildItemOptions(),
            WorkshopMode.Enemies => BuildEnemyOptions(),
            _ => new[]
            {
                "Reload tool data",
                "Reload runtime content from disk",
                "Validate room + content documents",
                "Open debug console",
                "Close workshop",
            },
        };
    }

    private IReadOnlyList<string> BuildRoomOptions()
    {
        var browse = _roomIds.Count == 0 ? "none" : _roomIds[_roomBrowseIndex];
        return new[]
        {
            $"Browse existing room: {browse}",
            "Create new room draft",
            $"Width: {_mapEditor.CanvasWidth}",
            $"Height: {_mapEditor.CanvasHeight}",
            $"Brush: {_mapEditor.SelectedBrush} ({_mapEditor.BrushLegend[_mapEditor.SelectedBrush]})",
            $"Tool: {_mapEditor.CurrentTool}",
            $"Cursor X: {_cursorX}",
            $"Cursor Y: {_cursorY}",
            "Apply tool at cursor",
            "Playtest current room draft",
            $"Save draft: {_mapEditor.RoomId}",
            "Reset canvas",
            "Validate room draft",
        };
    }

    private IReadOnlyList<string> BuildItemOptions()
    {
        var item = _itemEditor.SelectedItem;
        return new[]
        {
            $"Selected item: {item?.Id ?? "none"}",
            "Create new item draft",
            $"Type: {item?.Type ?? "n/a"}",
            $"Slot: {item?.Slot ?? "n/a"}",
            $"Rarity: {item?.Rarity ?? "n/a"}",
            $"Attack stat: {ResolveItemStat(item, "attack")}",
            $"Defense stat: {ResolveItemStat(item, "defense")}",
            $"Value: {item?.Value ?? 0}",
            $"Max stack: {item?.MaxStack ?? 1}",
            $"Stackable: {item?.Stackable ?? false}",
            "Drop selected item at player",
            "Save items.json",
            "Validate content",
        };
    }

    private IReadOnlyList<string> BuildEnemyOptions()
    {
        var enemy = _itemEditor.SelectedEnemy;
        return new[]
        {
            $"Selected enemy: {enemy?.Id ?? "none"}",
            "Create new enemy draft",
            $"AI type: {enemy?.AiType ?? "n/a"}",
            $"Faction: {enemy?.Faction ?? "n/a"}",
            $"HP: {enemy?.Stats.HP ?? 0}",
            $"Attack: {enemy?.Stats.Attack ?? 0}",
            $"Defense: {enemy?.Stats.Defense ?? 0}",
            $"Speed: {enemy?.Stats.Speed ?? 0}",
            $"FOV: {enemy?.Stats.FovRange ?? 0}",
            $"Min depth: {enemy?.MinDepth ?? 0}",
            $"Max depth: {enemy?.MaxDepth ?? 0}",
            $"Spawn weight: {enemy?.SpawnWeight ?? 0}",
            "Spawn selected enemy near player",
            "Save enemies.json",
            "Validate content",
        };
    }

    private string BuildRoomBody()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Draft: {_mapEditor.RoomId}  Name: {_mapEditor.RoomName}");
        builder.AppendLine($"Depths: {_mapEditor.MinDepth}-{_mapEditor.MaxDepth}  Tags: {_mapEditor.TagsText}");
        builder.AppendLine("Layout:");
        builder.Append(_mapEditor.CanvasText);
        return builder.ToString();
    }

    private string BuildItemBody()
    {
        EnsureItemSelection();
        var item = _itemEditor.SelectedItem;
        if (item is null)
        {
            return "No item loaded. Create a draft to begin.";
        }

        return string.Join(
            "\n",
            $"Item: {item.Id} - {item.Name}",
            item.Description,
            $"Sprite: {item.SpritePath}",
            $"Type {item.Type} / Slot {item.Slot} / Rarity {item.Rarity}",
            $"Stats: atk {ResolveItemStat(item, "attack")}, def {ResolveItemStat(item, "defense")}, value {item.Value}",
            $"Stacking: {(item.Stackable ? $"on (max {item.MaxStack ?? 1})" : "off")}");
    }

    private string BuildEnemyBody()
    {
        EnsureEnemySelection();
        var enemy = _itemEditor.SelectedEnemy;
        if (enemy is null)
        {
            return "No enemy loaded. Create a draft to begin.";
        }

        return string.Join(
            "\n",
            $"Enemy: {enemy.Id} - {enemy.Name}",
            enemy.Description,
            $"Sprite: {enemy.SpritePath}",
            $"AI {enemy.AiType} / Faction {enemy.Faction}",
            $"Stats: hp {enemy.Stats.HP}, atk {enemy.Stats.Attack}, def {enemy.Stats.Defense}, speed {enemy.Stats.Speed}, fov {enemy.Stats.FovRange}",
            $"Depths: {enemy.MinDepth}-{enemy.MaxDepth}  Weight: {enemy.SpawnWeight}");
    }

    private string BuildCommandsBody()
    {
        var commands = new DebugCommandProcessor().GetCommands();
        return string.Join(
            "\n",
            "Use this tab to reload tool data, validate documents, or jump into the debug console.",
            "Console commands:",
            string.Join(", ", commands));
    }

    private WorldState BuildPlaytestWorld()
    {
        var layout = _mapEditor.GetLayoutRows();
        if (layout.Count == 0)
        {
            throw new InvalidOperationException("Room draft has no layout to playtest.");
        }

        var width = _mapEditor.CanvasWidth;
        var height = _mapEditor.CanvasHeight;
        var world = new WorldState();
        world.InitGrid(width, height);
        world.Depth = 1;
        world.Seed = _gameManager?.Seed ?? 1;

        var playerSpawn = Roguelike.Core.Position.Invalid;
        var enemySpawns = new List<Roguelike.Core.Position>();
        var itemSpawns = new List<Roguelike.Core.Position>();

        for (var y = 0; y < height; y++)
        {
            var row = y < layout.Count ? layout[y] : string.Empty;
            for (var x = 0; x < width; x++)
            {
                var token = x < row.Length ? row[x] : '#';
                var position = new Roguelike.Core.Position(x, y);
                world.SetTile(position, ResolveTileType(token));
                if (token == 'P')
                {
                    playerSpawn = position;
                }
                else if (token == 'S')
                {
                    enemySpawns.Add(position);
                }
                else if (token is 'I' or 'C')
                {
                    itemSpawns.Add(position);
                }
            }
        }

        if (playerSpawn == Roguelike.Core.Position.Invalid)
        {
            playerSpawn = FindFirstWalkable(world);
        }

        var player = CreatePlaytestPlayer(playerSpawn);
        world.Player = player;
        world.AddEntity(player);

        foreach (var spawn in enemySpawns)
        {
            world.AddEntity(CreateEnemyEntity(_itemEditor.SelectedEnemy ?? CreateFallbackEnemyDefinition(), spawn));
        }

        var itemTemplateId = ResolvePlaytestItemTemplateId();
        if (!string.IsNullOrWhiteSpace(itemTemplateId))
        {
            foreach (var spawn in itemSpawns)
            {
                world.DropItem(spawn, new ItemInstance
                {
                    TemplateId = itemTemplateId,
                    StackCount = 1,
                    IsIdentified = true,
                });
            }
        }

        return world;
    }

    private void RefreshVisualState()
    {
        EnsureVisuals();
        if (_panel is null || _label is null)
        {
            return;
        }

        Size = ResolveViewportSize();
        _panel.Position = new Vector2((Size.X - _panel.Size.X) * 0.5f, (Size.Y - _panel.Size.Y) * 0.5f);
        _panel.Visible = Visible;
        _label.Visible = Visible;
        _label.Text = SummaryText;
    }

    private void CycleBrush(int delta)
    {
        var brushes = _mapEditor.BrushLegend.Keys.OrderBy(key => key).ToArray();
        var index = Array.IndexOf(brushes, _mapEditor.SelectedBrush);
        index = (index + delta + brushes.Length) % brushes.Length;
        _mapEditor.SetSelectedBrush(brushes[index]);
        _statusText = _mapEditor.StatusText;
    }

    private void CycleTool(int delta)
    {
        var tools = Enum.GetValues<MapEditor.ToolMode>();
        var index = Array.IndexOf(tools, _mapEditor.CurrentTool);
        _mapEditor.SetTool(tools[(index + delta + tools.Length) % tools.Length]);
        _statusText = _mapEditor.StatusText;
    }

    private void EnsureItemSelection()
    {
        if (_itemEditor.SelectedItem is null && _itemEditor.ItemIds.Count > 0)
        {
            _itemEditor.SelectItem(_itemEditor.ItemIds[0]);
        }
    }

    private void EnsureEnemySelection()
    {
        if (_itemEditor.SelectedEnemy is null && _itemEditor.EnemyIds.Count > 0)
        {
            _itemEditor.SelectEnemy(_itemEditor.EnemyIds[0]);
        }
    }

    private Entity CreatePlaytestPlayer(Position spawn)
    {
        var player = new Entity(
            "Builder",
            spawn,
            new Stats
            {
                HP = 40,
                MaxHP = 40,
                Attack = 8,
                Defense = 4,
                Accuracy = 80,
                Evasion = 8,
                Speed = 100,
                ViewRadius = 8,
            },
            Faction.Player);
        var inventory = new InventoryComponent(20);
        inventory.Add(new ItemInstance { TemplateId = "potion_health", IsIdentified = true });
        player.SetComponent(inventory);
        return player;
    }

    private static Entity CreateEnemyEntity(EnemyDefinition definition, Position position)
    {
        var faction = ParseFaction(definition.Faction);
        var entity = new Entity(
            definition.Name,
            position,
            new Stats
            {
                HP = definition.Stats.HP,
                MaxHP = definition.Stats.HP,
                Attack = definition.Stats.Attack,
                Defense = definition.Stats.Defense,
                Accuracy = definition.Stats.Accuracy,
                Evasion = definition.Stats.Evasion,
                Speed = definition.Stats.Speed,
                ViewRadius = definition.Stats.FovRange,
            },
            faction);
        entity.SetComponent<IBrain>(BrainFactory.Create(MapAiType(definition.AiType)));
        return entity;
    }

    private string ResolvePlaytestItemTemplateId()
    {
        EnsureItemSelection();
        var selected = _itemEditor.SelectedItem?.Id;
        if (!string.IsNullOrWhiteSpace(selected) && _gameManager?.Content?.TryGetItemTemplate(selected, out _) == true)
        {
            return selected;
        }

        return _gameManager?.Content?.ItemTemplates.Keys.OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault() ?? string.Empty;
    }

    private static Roguelike.Core.Position FindFirstWalkable(IWorldState world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Roguelike.Core.Position(x, y);
                if (world.IsWalkable(position))
                {
                    return position;
                }
            }
        }

        throw new InvalidOperationException("Room draft does not contain a walkable tile for the player.");
    }

    private static Roguelike.Core.Position FindNearbyOpenTile(WorldState world, Roguelike.Core.Position origin)
    {
        foreach (var delta in Roguelike.Core.Position.AllDirections)
        {
            var candidate = origin + delta;
            if (world.IsWalkable(candidate) && world.GetEntityAt(candidate) is null)
            {
                return candidate;
            }
        }

        return Roguelike.Core.Position.Invalid;
    }

    private static TileType ResolveTileType(char token)
    {
        return token switch
        {
            '#' or ' ' => TileType.Wall,
            '+' => TileType.Door,
            '>' => TileType.StairsDown,
            '<' => TileType.StairsUp,
            '~' => TileType.Water,
            '^' => TileType.Lava,
            _ => TileType.Floor,
        };
    }

    private static string MapAiType(string aiType)
    {
        return aiType switch
        {
            "melee_rush" => "melee_rusher",
            "ranged_kite" => "ranged_kiter",
            "ambush" => "fleeing",
            "patrol" => "patrol_guard",
            "support" => "fleeing",
            _ => "melee_rusher",
        };
    }

    private static Faction ParseFaction(string faction)
    {
        return faction.Equals("neutral", StringComparison.OrdinalIgnoreCase) ? Faction.Neutral : Faction.Enemy;
    }

    private static EnemyDefinition CreateFallbackEnemyDefinition()
    {
        return new EnemyDefinition
        {
            Id = "workbench_dummy",
            Name = "Workbench Dummy",
            Description = "Fallback enemy for room playtests.",
            AiType = "melee_rush",
            Faction = "Enemy",
            MinDepth = 1,
            MaxDepth = 1,
            SpawnWeight = 1,
            Stats = new EnemyStatsDefinition
            {
                HP = 10,
                Attack = 3,
                Defense = 1,
                Accuracy = 70,
                Evasion = 5,
                Speed = 100,
                FovRange = 7,
                XpValue = 5,
            },
        };
    }

    private void ClampCursor()
    {
        _cursorX = Math.Clamp(_cursorX, 0, Math.Max(0, _mapEditor.CanvasWidth - 1));
        _cursorY = Math.Clamp(_cursorY, 0, Math.Max(0, _mapEditor.CanvasHeight - 1));
    }

    private Vector2 ResolveViewportSize()
    {
        return GetParent() is not null && GetTree() is not null ? GetViewportRect().Size : new Vector2(1280f, 720f);
    }

    private static int ResolveItemStat(ItemDefinition? item, string stat)
    {
        if (item is null)
        {
            return 0;
        }

        return item.Stats.TryGetValue(stat, out var value) ? value : 0;
    }

    private static string NextStableId(string prefix, IReadOnlyList<string> existing)
    {
        var used = new HashSet<string>(existing, StringComparer.Ordinal);
        var suffix = 1;
        while (used.Contains($"{prefix}_{suffix}"))
        {
            suffix++;
        }

        return $"{prefix}_{suffix}";
    }
}