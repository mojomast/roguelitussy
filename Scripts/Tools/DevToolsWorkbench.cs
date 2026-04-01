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
    private const float OuterMargin = 24f;
    private const int MinSaveSlot = 1;
    private const int MaxSaveSlot = 3;

    private readonly MapEditor _mapEditor = new();
    private readonly ItemEditor _itemEditor = new();
    private Panel? _panel;
    private ColorRect? _backdrop;
    private ColorRect? _headerBand;
    private ColorRect? _bodyCard;
    private ColorRect? _optionsCard;
    private ColorRect? _statusCard;
    private Label? _label;
    private Label? _titleLabel;
    private Label? _modeLabel;
    private Label? _optionsLabel;
    private Label? _statusLabel;
    private Label? _controlsLabel;
    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IContentDatabase? _content;
    private string? _contentDirectory;
    private string _statusText = "Workshop ready.";
    private int _selectedIndex;
    private int _roomBrowseIndex;
    private int _cursorX;
    private int _cursorY;
    private int _pendingSeed = 1337;
    private int _selectedSaveSlot = MinSaveSlot;
    private int _pendingFloorDepth;
    private int _pendingTeleportX;
    private int _pendingTeleportY;
    private IReadOnlyList<string> _roomIds = Array.Empty<string>();
    private string _bodyText = string.Empty;
    private string _optionsText = string.Empty;

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

    public event Action? RuntimeSessionActivated;

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
            case Key.KpEnter:
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

    public void SetPendingSeed(int seed)
    {
        _pendingSeed = Math.Max(1, seed);
        Refresh();
    }

    public void SetSelectedSaveSlot(int slot)
    {
        _selectedSaveSlot = Math.Clamp(slot, MinSaveSlot, MaxSaveSlot);
        Refresh();
    }

    public void SetPendingFloorDepth(int depth)
    {
        _pendingFloorDepth = Math.Max(0, depth);
        Refresh();
    }

    public void SetPendingTeleportTarget(int x, int y)
    {
        _pendingTeleportX = Math.Max(0, x);
        _pendingTeleportY = Math.Max(0, y);
        ClampPendingTeleportTarget();
        Refresh();
    }

    public bool StartNewExpeditionFromSeed()
    {
        if (_gameManager is null)
        {
            _statusText = "Game manager is not available.";
            Refresh();
            return false;
        }

        _gameManager.StartNewGame(_pendingSeed);
        if (_gameManager.CurrentState != GameManager.GameState.Playing)
        {
            _statusText = $"Failed to start a run with seed {_pendingSeed}.";
            Refresh();
            return false;
        }

        _statusText = $"Started a new expedition with seed {_pendingSeed}.";
        Close();
        RuntimeSessionActivated?.Invoke();
        return true;
    }

    public bool SaveCurrentRunToSlot()
    {
        if (_gameManager?.SaveToSlot(_selectedSaveSlot) != true)
        {
            _statusText = $"Save failed for slot {_selectedSaveSlot}.";
            Refresh();
            return false;
        }

        _statusText = $"Saved the current run to slot {_selectedSaveSlot}.";
        Refresh();
        return true;
    }

    public bool LoadRunFromSlot()
    {
        if (_gameManager?.LoadFromSlot(_selectedSaveSlot) != true)
        {
            _statusText = $"Load failed for slot {_selectedSaveSlot}.";
            Refresh();
            return false;
        }

        _pendingSeed = Math.Max(1, _gameManager.Seed);
        _statusText = $"Loaded slot {_selectedSaveSlot}.";
        Close();
        RuntimeSessionActivated?.Invoke();
        return true;
    }

    public bool HealPlayerToFull()
    {
        var player = _gameManager?.World?.Player;
        if (player is null)
        {
            _statusText = "No active player is available to heal.";
            Refresh();
            return false;
        }

        player.Stats.HP = player.Stats.MaxHP;
        _eventBus?.EmitHPChanged(player.Id, player.Stats.HP, player.Stats.MaxHP);
        _statusText = $"Healed {player.Name} to full HP.";
        Refresh();
        return true;
    }

    public bool RevealEntireMap()
    {
        if (_gameManager?.SetMapReveal(true) != true)
        {
            _statusText = "No active world is available to reveal.";
            Refresh();
            return false;
        }

        _statusText = "Revealed the current map.";
        Refresh();
        return true;
    }

    public bool RestorePlayerFog()
    {
        if (_gameManager?.SetMapReveal(false) != true)
        {
            _statusText = "No active world is available to refresh.";
            Refresh();
            return false;
        }

        _statusText = "Restored player visibility and fog.";
        Refresh();
        return true;
    }

    public bool TravelToPendingFloor()
    {
        if (_gameManager?.TravelToFloor(_pendingFloorDepth) != true)
        {
            _statusText = $"Could not travel to floor {_pendingFloorDepth}.";
            Refresh();
            return false;
        }

        SyncRuntimeTargets();
        _statusText = $"Travelled to floor {_pendingFloorDepth}.";
        Refresh();
        return true;
    }

    public bool TeleportPlayerToPendingTarget()
    {
        var target = new Roguelike.Core.Position(_pendingTeleportX, _pendingTeleportY);
        if (_gameManager?.TeleportPlayer(target) != true)
        {
            _statusText = $"Could not teleport to {_pendingTeleportX},{_pendingTeleportY}.";
            Refresh();
            return false;
        }

        SyncRuntimeTargets();
        _statusText = $"Teleported the player to {_pendingTeleportX},{_pendingTeleportY}.";
        Refresh();
        return true;
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
        if (_gameManager is not null && _gameManager.Seed > 0)
        {
            _pendingSeed = _gameManager.Seed;
        }

        SyncRuntimeTargets();

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
            _ => 18,
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
            case WorkshopMode.Commands:
                AdjustCommand(delta);
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
                StartNewExpeditionFromSeed();
                return;
            case 1:
                SaveCurrentRunToSlot();
                return;
            case 2:
                LoadRunFromSlot();
                return;
            case 3:
                HealPlayerToFull();
                return;
            case 4:
                RevealEntireMap();
                return;
            case 5:
                RestorePlayerFog();
                return;
            case 6:
                TravelToPendingFloor();
                return;
            case 7:
                TeleportPlayerToPendingTarget();
                return;
            case 8:
                ReloadData();
                _statusText = "Tool data reloaded from Content/.";
                break;
            case 9:
                ReloadRuntimeContent();
                return;
            case 10:
                var errors = _itemEditor.ValidateAll();
                var roomErrors = _mapEditor.ValidateDraft();
                var totalErrors = errors.Count + roomErrors.Count;
                _statusText = totalErrors == 0 ? "All visible tooling validations passed." : $"Validation reported {totalErrors} issue(s).";
                break;
            case 11:
                DebugConsoleRequested?.Invoke();
                _statusText = "Debug console opened.";
                break;
            case 12:
                Close();
                break;
        }
    }

    private void AdjustCommand(int delta)
    {
        switch (_selectedIndex)
        {
            case 13:
                _pendingSeed = Math.Max(1, _pendingSeed + delta);
                _statusText = $"Pending seed set to {_pendingSeed}.";
                break;
            case 14:
                _selectedSaveSlot = WrapSaveSlot(_selectedSaveSlot + delta);
                _statusText = $"Selected save slot {_selectedSaveSlot}.";
                break;
            case 15:
                _pendingFloorDepth = Math.Max(0, _pendingFloorDepth + delta);
                _statusText = $"Pending floor set to {_pendingFloorDepth}.";
                break;
            case 16:
                _pendingTeleportX = Math.Max(0, _pendingTeleportX + delta);
                ClampPendingTeleportTarget();
                _statusText = $"Teleport X set to {_pendingTeleportX}.";
                break;
            case 17:
                _pendingTeleportY = Math.Max(0, _pendingTeleportY + delta);
                ClampPendingTeleportTarget();
                _statusText = $"Teleport Y set to {_pendingTeleportY}.";
                break;
        }
    }

    private void EnsureVisuals()
    {
        if (_panel is not null && _label is not null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);

        Size = viewportSize;
        ZIndex = 110;
        _panel = new Panel
        {
            Name = "Panel",
            Size = panelSize,
        };
        _backdrop = new ColorRect
        {
            Name = "Backdrop",
            Color = new Color(0.04f, 0.06f, 0.08f, 0.98f),
        };
        _headerBand = new ColorRect
        {
            Name = "HeaderBand",
            Color = new Color(0.22f, 0.11f, 0.05f, 0.98f),
        };
        _bodyCard = new ColorRect
        {
            Name = "BodyCard",
            Color = new Color(0.11f, 0.14f, 0.18f, 0.98f),
        };
        _optionsCard = new ColorRect
        {
            Name = "OptionsCard",
            Color = new Color(0.08f, 0.09f, 0.12f, 0.99f),
        };
        _statusCard = new ColorRect
        {
            Name = "StatusCard",
            Color = new Color(0.09f, 0.11f, 0.14f, 0.99f),
        };
        _label = new Label
        {
            Name = "Label",
            Modulate = new Color(0.95f, 0.97f, 0.99f, 1f),
        };
        _titleLabel = new Label
        {
            Name = "TitleLabel",
            Modulate = new Color(1f, 0.93f, 0.82f, 1f),
        };
        _modeLabel = new Label
        {
            Name = "ModeLabel",
            Modulate = new Color(0.99f, 0.86f, 0.69f, 1f),
        };
        _optionsLabel = new Label
        {
            Name = "OptionsLabel",
            Modulate = new Color(0.98f, 0.92f, 0.85f, 1f),
        };
        _statusLabel = new Label
        {
            Name = "StatusLabel",
            Modulate = new Color(0.93f, 0.95f, 0.98f, 1f),
        };
        _controlsLabel = new Label
        {
            Name = "ControlsLabel",
            Modulate = new Color(0.73f, 0.78f, 0.84f, 1f),
        };

        _bodyCard.AddChild(_label);
        _optionsCard.AddChild(_optionsLabel);
        _statusCard.AddChild(_statusLabel);
        _statusCard.AddChild(_controlsLabel);
        _panel.AddChild(_backdrop);
        _panel.AddChild(_headerBand);
        _panel.AddChild(_bodyCard);
        _panel.AddChild(_optionsCard);
        _panel.AddChild(_statusCard);
        _panel.AddChild(_titleLabel);
        _panel.AddChild(_modeLabel);
        AddChild(_panel);
    }

    private void Refresh()
    {
        EnsureVisuals();

        _bodyText = BuildBodyText();
        var options = BuildOptions();
        _optionsText = BuildOptionsText(options);

        var builder = new StringBuilder();
        builder.AppendLine("DEVELOPER WORKSHOP");
        builder.AppendLine(BuildModeHeader());
        builder.AppendLine();
        builder.AppendLine(_bodyText);
        builder.AppendLine();

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

    private string BuildOptionsText(IReadOnlyList<string> options)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < options.Count; index++)
        {
            builder.Append(index == _selectedIndex ? "> " : "  ");
            builder.AppendLine(options[index]);
        }

        return builder.ToString().TrimEnd();
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
                "Start new expedition",
                "Save current run to selected slot",
                "Load selected slot",
                "Heal player to full",
                "Reveal entire map",
                "Restore player fog",
                "Travel to selected floor",
                "Teleport player to target",
                "Reload tool data",
                "Reload runtime content from disk",
                "Validate room + content documents",
                "Open debug console",
                "Close workshop",
                $"Seed: {_pendingSeed}",
                $"Save slot: {_selectedSaveSlot}",
                $"Floor target: {_pendingFloorDepth}",
                $"Teleport X: {_pendingTeleportX}",
                $"Teleport Y: {_pendingTeleportY}",
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
        var world = _gameManager?.World;
        var player = world?.Player;
        var slotMetadata = _gameManager?.SaveManager?.GetSaveMetadata(_selectedSaveSlot);
        return string.Join(
            "\n",
            $"State: {_gameManager?.CurrentState}",
            player is null
                ? "Run: no active expedition"
                : $"Run: {player.Name} on floor {world!.Depth}, turn {world.TurnNumber}, HP {player.Stats.HP}/{player.Stats.MaxHP}, Pos {player.Position.X},{player.Position.Y}",
            slotMetadata is null
                ? $"Slot {_selectedSaveSlot}: empty"
                : $"Slot {_selectedSaveSlot}: {slotMetadata.PlayerName}, floor {slotMetadata.Depth}, turn {slotMetadata.TurnNumber}",
            $"Targets: floor {_pendingFloorDepth}, teleport {_pendingTeleportX},{_pendingTeleportY}",
            "Use this tab to manage runs, saves, travel, visibility, and deeper debug handoffs.",
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
        if (_panel is null
            || _label is null
            || _backdrop is null
            || _headerBand is null
            || _bodyCard is null
            || _optionsCard is null
            || _statusCard is null
            || _titleLabel is null
            || _modeLabel is null
            || _optionsLabel is null
            || _statusLabel is null
            || _controlsLabel is null)
        {
            return;
        }

        var viewportSize = ResolveViewportSize();
        var panelSize = ResolvePanelSize(viewportSize);

        Size = viewportSize;
        _panel.Size = panelSize;
        _panel.Position = OverlayLayoutHelper.CenterInViewport(viewportSize, panelSize);

        _backdrop.Position = Vector2.Zero;
        _backdrop.Size = panelSize;
        _headerBand.Position = Vector2.Zero;
        _headerBand.Size = new Vector2(panelSize.X, 56f);

        _titleLabel.Position = new Vector2(PanelPadding, 12f);
        _titleLabel.Size = new Vector2(Math.Max(0f, panelSize.X - (PanelPadding * 2f)), 24f);
        _titleLabel.Text = "DEVELOPER WORKSHOP";

        _modeLabel.Position = new Vector2(PanelPadding, 32f);
        _modeLabel.Size = new Vector2(Math.Max(0f, panelSize.X - (PanelPadding * 2f)), 20f);
        _modeLabel.Text = BuildModeHeader();

        var contentTop = 68f;
        var statusHeight = 72f;
        var compactLayout = panelSize.X < 720f || panelSize.Y < 420f;
        var statusTop = panelSize.Y - PanelPadding - statusHeight;
        var contentHeight = Math.Max(0f, statusTop - contentTop - 12f);

        if (compactLayout)
        {
            var bodyHeight = Math.Max(80f, (contentHeight * 0.54f));
            var optionsTop = contentTop + bodyHeight + 12f;

            _bodyCard.Position = new Vector2(PanelPadding, contentTop);
            _bodyCard.Size = new Vector2(Math.Max(0f, panelSize.X - (PanelPadding * 2f)), Math.Max(0f, bodyHeight));

            _optionsCard.Position = new Vector2(PanelPadding, optionsTop);
            _optionsCard.Size = new Vector2(
                Math.Max(0f, panelSize.X - (PanelPadding * 2f)),
                Math.Max(0f, statusTop - optionsTop - 8f));
        }
        else
        {
            var bodyWidth = Math.Max(320f, (panelSize.X - (PanelPadding * 3f)) * 0.60f);
            var optionsWidth = Math.Max(180f, panelSize.X - bodyWidth - (PanelPadding * 3f));

            _bodyCard.Position = new Vector2(PanelPadding, contentTop);
            _bodyCard.Size = new Vector2(bodyWidth, contentHeight);

            _optionsCard.Position = new Vector2(_bodyCard.Position.X + _bodyCard.Size.X + PanelPadding, contentTop);
            _optionsCard.Size = new Vector2(optionsWidth, contentHeight);
        }

        _label.Position = new Vector2(14f, 12f);
        _label.Size = new Vector2(
            Math.Max(0f, _bodyCard.Size.X - 28f),
            Math.Max(0f, _bodyCard.Size.Y - 24f));
        _label.Text = $"MODE SUMMARY\n\n{_bodyText}";

        _optionsLabel.Position = new Vector2(14f, 12f);
        _optionsLabel.Size = new Vector2(
            Math.Max(0f, _optionsCard.Size.X - 28f),
            Math.Max(0f, _optionsCard.Size.Y - 24f));
        _optionsLabel.Text = string.IsNullOrWhiteSpace(_optionsText)
            ? string.Empty
            : $"ACTIONS\n\n{_optionsText}";

        _statusCard.Position = new Vector2(PanelPadding, statusTop);
        _statusCard.Size = new Vector2(Math.Max(0f, panelSize.X - (PanelPadding * 2f)), statusHeight);

        _statusLabel.Position = new Vector2(14f, 10f);
        _statusLabel.Size = new Vector2(Math.Max(0f, _statusCard.Size.X - 28f), 22f);
        _statusLabel.Text = $"Status  {_statusText}";

        _controlsLabel.Position = new Vector2(14f, 34f);
        _controlsLabel.Size = new Vector2(Math.Max(0f, _statusCard.Size.X - 28f), 28f);
        _controlsLabel.Text = "Tab mode  Up/Down select  Left/Right or +/- adjust  Enter apply  Esc/T close";

        _panel.Visible = Visible;
        _backdrop.Visible = Visible;
        _headerBand.Visible = Visible;
        _bodyCard.Visible = Visible;
        _optionsCard.Visible = Visible;
        _statusCard.Visible = Visible;
        _label.Visible = Visible;
        _titleLabel.Visible = Visible;
        _modeLabel.Visible = Visible;
        _optionsLabel.Visible = Visible;
        _statusLabel.Visible = Visible;
        _controlsLabel.Visible = Visible;
    }

    private Vector2 ResolvePanelSize(Vector2 viewportSize)
    {
        return OverlayLayoutHelper.FitPanelSize(viewportSize, new Vector2(PanelWidth, PanelHeight), OuterMargin);
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

    private void SyncRuntimeTargets()
    {
        var world = _gameManager?.World;
        var player = world?.Player;
        if (world is null || player is null)
        {
            return;
        }

        _pendingFloorDepth = Math.Max(0, world.Depth);
        _pendingTeleportX = Math.Max(0, player.Position.X);
        _pendingTeleportY = Math.Max(0, player.Position.Y);
        ClampPendingTeleportTarget();
    }

    private void ClampPendingTeleportTarget()
    {
        var world = _gameManager?.World;
        if (world is null)
        {
            return;
        }

        _pendingTeleportX = Math.Clamp(_pendingTeleportX, 0, Math.Max(0, world.Width - 1));
        _pendingTeleportY = Math.Clamp(_pendingTeleportY, 0, Math.Max(0, world.Height - 1));
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
            "ambush" => "ambush",
            "patrol" => "patrol_guard",
            "support" => "support",
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

    private static int WrapSaveSlot(int slot)
    {
        var slotCount = MaxSaveSlot - MinSaveSlot + 1;
        return ((slot - MinSaveSlot) % slotCount + slotCount) % slotCount + MinSaveSlot;
    }
}