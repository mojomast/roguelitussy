using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class UIRoot : CanvasLayer
{
    private EventBus? _eventBus;
    private GameManager? _gameManager;
    private IContentDatabase? _content;
    private int _enemiesKilled;
    private bool _restoreMainMenuAfterDevTools;

    public UIRoot()
    {
        Name = "UiRoot";
    }

    public HUD HUD { get; } = new();

    public CombatLog CombatLog { get; } = new();

    public InventoryUI Inventory { get; } = new();

    public CharacterSheet CharacterSheet { get; } = new();

    public MainMenu MainMenu { get; } = new();

    public PauseMenu PauseMenu { get; } = new();

    public GameOverScreen GameOverScreen { get; } = new();

    public Minimap Minimap { get; } = new();

    public DevToolsWorkbench DevToolsWorkbench { get; } = new();

    public HelpOverlay HelpOverlay { get; } = new();

    public Tooltip Tooltip { get; } = new();

    public DebugConsole DebugConsole { get; } = new();

    public DebugOverlay DebugOverlay { get; } = new();

    public InputHandler InputHandler { get; } = new();

    public override void _Ready()
    {
        EnsureChildren();
        var contentAutoload = GetNodeOrNull<ContentDatabase>("/root/ContentDatabase");
        BindServices(GetNodeOrNull<GameManager>("/root/GameManager"), GetNodeOrNull<EventBus>("/root/EventBus"), contentAutoload?.Database);
    }

    public void BindServices(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.EntityDied -= OnEntityDied;
            _eventBus.LoadCompleted -= OnLoadCompleted;
            _eventBus.FloorChanged -= OnFloorChanged;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content ?? gameManager?.Content;

        HUD.Bind(_gameManager, _eventBus);
        CombatLog.Bind(_gameManager, _eventBus);
        Inventory.Bind(_gameManager, _eventBus, _content, Tooltip);
        CharacterSheet.Bind(_gameManager, _eventBus, _content);
        Minimap.Bind(_gameManager, _eventBus);
        DevToolsWorkbench.Bind(_gameManager, _eventBus, _content);
        MainMenu.Bind(_gameManager, _eventBus);
        PauseMenu.Bind(_eventBus);
        DebugConsole.Bind(_gameManager, _eventBus, _content);
        DebugOverlay.Bind(_gameManager, _eventBus);
        InputHandler.Bind(_gameManager, _eventBus);
        CombatLog.RefreshConsole();

        MainMenu.GameStarted -= OnGameStarted;
        MainMenu.GameStarted += OnGameStarted;
        MainMenu.HelpRequested -= ToggleHelp;
        MainMenu.HelpRequested += ToggleHelp;
        MainMenu.DevToolsRequested -= OpenDevTools;
        MainMenu.DevToolsRequested += OpenDevTools;

        PauseMenu.ResumeRequested -= OnResumeRequested;
        PauseMenu.ResumeRequested += OnResumeRequested;
        PauseMenu.CharacterSheetRequested -= OnCharacterSheetRequested;
        PauseMenu.CharacterSheetRequested += OnCharacterSheetRequested;
        PauseMenu.HelpRequested -= ToggleHelp;
        PauseMenu.HelpRequested += ToggleHelp;
        PauseMenu.DevToolsRequested -= OpenDevTools;
        PauseMenu.DevToolsRequested += OpenDevTools;
        PauseMenu.MainMenuRequested -= OpenMainMenu;
        PauseMenu.MainMenuRequested += OpenMainMenu;

        GameOverScreen.RetryRequested -= RetryCurrentSeed;
        GameOverScreen.RetryRequested += RetryCurrentSeed;
        GameOverScreen.MainMenuRequested -= OpenMainMenu;
        GameOverScreen.MainMenuRequested += OpenMainMenu;

        InputHandler.InventoryRequested -= ToggleInventory;
        InputHandler.InventoryRequested += ToggleInventory;
        InputHandler.CharacterSheetRequested -= ToggleCharacterSheet;
        InputHandler.CharacterSheetRequested += ToggleCharacterSheet;
        InputHandler.PauseRequested -= TogglePause;
        InputHandler.PauseRequested += TogglePause;
        InputHandler.MinimapToggleRequested -= ToggleMinimap;
        InputHandler.MinimapToggleRequested += ToggleMinimap;
        InputHandler.HelpRequested -= ToggleHelp;
        InputHandler.HelpRequested += ToggleHelp;
        InputHandler.ToolsRequested -= ToggleDevTools;
        InputHandler.ToolsRequested += ToggleDevTools;

        DevToolsWorkbench.DebugConsoleRequested -= OpenDebugConsoleFromWorkshop;
        DevToolsWorkbench.DebugConsoleRequested += OpenDebugConsoleFromWorkshop;
        DevToolsWorkbench.PlaytestStarted -= OnDevToolsPlaytestStarted;
        DevToolsWorkbench.PlaytestStarted += OnDevToolsPlaytestStarted;
        DevToolsWorkbench.RuntimeSessionActivated -= OnDevToolsRuntimeSessionActivated;
        DevToolsWorkbench.RuntimeSessionActivated += OnDevToolsRuntimeSessionActivated;
        DevToolsWorkbench.RuntimeContentReloaded -= OnRuntimeContentReloaded;
        DevToolsWorkbench.RuntimeContentReloaded += OnRuntimeContentReloaded;

        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted += OnTurnCompleted;
            _eventBus.EntityDied += OnEntityDied;
            _eventBus.LoadCompleted += OnLoadCompleted;
            _eventBus.FloorChanged += OnFloorChanged;
        }

        _enemiesKilled = 0;
        if (_gameManager?.CurrentState == GameManager.GameState.MainMenu)
        {
            MainMenu.Open();
        }
        else
        {
            MainMenu.Close();
        }

        RefreshInputGate();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed)
        {
            return;
        }

        var key = keyEvent.PhysicalKeycode != Key.None ? keyEvent.PhysicalKeycode : keyEvent.Keycode;
        if (RouteKey(key))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void EnsureChildren()
    {
        AddIfMissing(HUD);
        AddIfMissing(CombatLog);
        AddIfMissing(Inventory);
        AddIfMissing(CharacterSheet);
        AddIfMissing(Minimap);
        AddIfMissing(DevToolsWorkbench);
        AddIfMissing(MainMenu);
        AddIfMissing(PauseMenu);
        AddIfMissing(GameOverScreen);
        AddIfMissing(HelpOverlay);
        AddIfMissing(Tooltip);
        AddIfMissing(DebugConsole);
        AddIfMissing(DebugOverlay);
        AddIfMissing(InputHandler);
    }

    private void AddIfMissing(Node node)
    {
        if (node.GetParent() is null)
        {
            AddChild(node);
        }
    }

    private bool RouteKey(Key key)
    {
        if (DebugConsole.Visible)
        {
            var handledByConsole = DebugConsole.HandleKey(key);
            if (handledByConsole)
            {
                RefreshInputGate();
            }

            return handledByConsole;
        }

        if (DevToolsWorkbench.Visible)
        {
            var handledByWorkshop = DevToolsWorkbench.HandleKey(key);
            if (handledByWorkshop)
            {
                RestoreMainMenuAfterDevToolsIfNeeded();
                RefreshInputGate();
            }

            return handledByWorkshop;
        }

        if (key == Key.Quoteleft)
        {
            ToggleDebugConsole();
            return true;
        }

        if (key == Key.Q)
        {
            ToggleDebugOverlay();
            return true;
        }

        if (GameOverScreen.Visible)
        {
            var handled = GameOverScreen.HandleKey(key);
            if (handled)
            {
                RefreshInputGate();
            }

            return handled;
        }

        if (HelpOverlay.Visible)
        {
            var handled = HelpOverlay.HandleKey(key);
            if (handled)
            {
                RefreshInputGate();
            }

            return handled;
        }

        if (MainMenu.Visible)
        {
            var handled = MainMenu.HandleKey(key);
            if (handled)
            {
                RefreshInputGate();
            }

            return handled;
        }

        if (PauseMenu.Visible)
        {
            var handled = PauseMenu.HandleKey(key);
            if (handled)
            {
                RefreshInputGate();
            }

            return handled;
        }

        if (Inventory.Visible)
        {
            var handled = Inventory.HandleKey(key);
            if (handled)
            {
                RefreshInputGate();
            }

            return handled;
        }

        if (CharacterSheet.Visible)
        {
            var handled = CharacterSheet.HandleKey(key);
            if (handled)
            {
                RefreshInputGate();
            }

            return handled;
        }

        return InputHandler.HandleKey(key);
    }

    private void OnTurnCompleted()
    {
        if (IsPlayerDead())
        {
            OpenGameOver();
        }

        RefreshInputGate();
    }

    private void OnEntityDied(EntityId entityId)
    {
        if (_gameManager?.World?.Player is null)
        {
            return;
        }

        if (entityId == _gameManager.World.Player.Id)
        {
            OpenGameOver();
            return;
        }

        _enemiesKilled++;
    }

    private void OnLoadCompleted(bool success)
    {
        if (!success)
        {
            return;
        }

        _enemiesKilled = 0;
        MainMenu.Close();
        PauseMenu.Close();
        GameOverScreen.Close();
        HelpOverlay.Close();
        Inventory.Close();
        CharacterSheet.Close();
        Tooltip.Hide();
        CombatLog.RefreshConsole();
        RefreshInputGate();
    }

    private void OnFloorChanged(int floor)
    {
        GameOverScreen.Close();
        CombatLog.RefreshConsole();
        RefreshInputGate();
    }

    private void OnGameStarted()
    {
        _enemiesKilled = 0;
        PauseMenu.Close();
        Inventory.Close();
        CharacterSheet.Close();
        GameOverScreen.Close();
        HelpOverlay.Close();
        Tooltip.Hide();
        CombatLog.RefreshConsole();
        RefreshInputGate();
    }

    private void OnResumeRequested()
    {
        PauseMenu.Close();
        RefreshInputGate();
    }

    private void OnCharacterSheetRequested()
    {
        PauseMenu.Close();
        ToggleCharacterSheet();
    }

    private void ToggleInventory()
    {
        if (MainMenu.Visible || GameOverScreen.Visible)
        {
            return;
        }

        PauseMenu.Close();
        CharacterSheet.Close();
        Inventory.Toggle();
        if (!Inventory.Visible)
        {
            Tooltip.Hide();
        }

        RefreshInputGate();
    }

    private void ToggleCharacterSheet()
    {
        if (MainMenu.Visible || GameOverScreen.Visible)
        {
            return;
        }

        PauseMenu.Close();
        Inventory.Close();
        Tooltip.Hide();
        CharacterSheet.Toggle();
        RefreshInputGate();
    }

    private void TogglePause()
    {
        if (MainMenu.Visible || GameOverScreen.Visible)
        {
            return;
        }

        if (Inventory.Visible)
        {
            Inventory.Close();
            Tooltip.Hide();
        }
        else if (CharacterSheet.Visible)
        {
            CharacterSheet.Close();
        }
        else if (PauseMenu.Visible)
        {
            PauseMenu.Close();
        }
        else
        {
            PauseMenu.Open();
        }

        RefreshInputGate();
    }

    private void ToggleMinimap()
    {
        HUD.ToggleMinimap();
        Minimap.Toggle();
    }

    private void ToggleHelp()
    {
        HelpOverlay.ToggleForContext(MainMenu.Visible);
        RefreshInputGate();
    }

    private void ToggleDevTools()
    {
        if (GameOverScreen.Visible)
        {
            return;
        }

        if (DevToolsWorkbench.Visible)
        {
            DevToolsWorkbench.Close();
            RestoreMainMenuAfterDevToolsIfNeeded();
        }
        else
        {
            OpenDevTools();
            return;
        }

        RefreshInputGate();
    }

    private void OpenDevTools()
    {
        _restoreMainMenuAfterDevTools = MainMenu.Visible;
        HelpOverlay.Close();
        PauseMenu.Close();
        Inventory.Close();
        CharacterSheet.Close();
        Tooltip.Hide();
        MainMenu.Close();
        DevToolsWorkbench.Open();
        RefreshInputGate();
    }

    private void ToggleDebugConsole()
    {
        DebugConsole.Toggle();
        RefreshInputGate();
    }

    private void OpenDebugConsoleFromWorkshop()
    {
        DevToolsWorkbench.Close();
        DebugConsole.Open();
        RefreshInputGate();
    }

    private void OnDevToolsPlaytestStarted()
    {
        _restoreMainMenuAfterDevTools = false;
        MainMenu.Close();
        PauseMenu.Close();
        HelpOverlay.Close();
        Tooltip.Hide();
        RefreshInputGate();
    }

    private void OnDevToolsRuntimeSessionActivated()
    {
        _restoreMainMenuAfterDevTools = false;
        MainMenu.Close();
        PauseMenu.Close();
        HelpOverlay.Close();
        Tooltip.Hide();
        RefreshInputGate();
    }

    private void OnRuntimeContentReloaded(IContentDatabase content)
    {
        BindServices(_gameManager, _eventBus, content);
    }

    private void ToggleDebugOverlay()
    {
        DebugOverlay.Toggle();
    }

    private void OpenMainMenu()
    {
        _restoreMainMenuAfterDevTools = false;
        PauseMenu.Close();
        Inventory.Close();
        CharacterSheet.Close();
        GameOverScreen.Close();
        HelpOverlay.Close();
        DevToolsWorkbench.Close();
        Tooltip.Hide();
        MainMenu.Open();
        CombatLog.RefreshConsole();
        RefreshInputGate();
    }

    private void RestoreMainMenuAfterDevToolsIfNeeded()
    {
        if (!_restoreMainMenuAfterDevTools)
        {
            return;
        }

        _restoreMainMenuAfterDevTools = false;
        if (_gameManager?.CurrentState == GameManager.GameState.MainMenu)
        {
            MainMenu.Open();
        }
    }

    private void RetryCurrentSeed()
    {
        var seed = _gameManager?.Seed ?? MainMenu.PendingSeed;
        MainMenu.SetSeed(seed <= 0 ? 1337 : seed);
        MainMenu.Close();
        _gameManager?.StartNewGame(MainMenu.PendingSeed);
        OnGameStarted();
    }

    private void OpenGameOver()
    {
        if (GameOverScreen.Visible || _gameManager?.World?.Player is null)
        {
            return;
        }

        PauseMenu.Close();
        Inventory.Close();
        CharacterSheet.Close();
        HelpOverlay.Close();
        DevToolsWorkbench.Close();
        Tooltip.Hide();
        var world = _gameManager.World;
        GameOverScreen.Open(new GameOverSummary(world.Player.Name, world.Depth, _enemiesKilled, world.TurnNumber));
        CombatLog.RefreshConsole();
        RefreshInputGate();
    }

    private bool IsPlayerDead()
    {
        return _gameManager?.World?.Player?.Stats.HP <= 0;
    }

    private void RefreshInputGate()
    {
        RefreshGameplayChromeVisibility();
        InputHandler.SetInputEnabled(
            !MainMenu.Visible
            && !PauseMenu.Visible
            && !Inventory.Visible
            && !CharacterSheet.Visible
            && !GameOverScreen.Visible
            && !HelpOverlay.Visible
            && !DevToolsWorkbench.Visible
            && !DebugConsole.Visible);
    }

    private void RefreshGameplayChromeVisibility()
    {
        var suppressGameplayChrome = MainMenu.Visible
            || PauseMenu.Visible
            || Inventory.Visible
            || CharacterSheet.Visible
            || GameOverScreen.Visible
            || HelpOverlay.Visible
            || DevToolsWorkbench.Visible
            || DebugConsole.Visible;

        Minimap.SetSuppressed(suppressGameplayChrome);
        CombatLog.SetSuppressed(suppressGameplayChrome);
    }
}