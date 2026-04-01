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

    public LevelUpOverlay LevelUpOverlay { get; } = new();

    public DialogUI DialogUI { get; } = new();

    public ShopUI ShopUI { get; } = new();

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
        var contentAutoload = ResolveContentAutoload();
        BindServices(ResolveGameManager(), ResolveEventBus(), contentAutoload?.Database);
    }

    public void BindServices(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        if (_eventBus is not null)
        {
            _eventBus.TurnCompleted -= OnTurnCompleted;
            _eventBus.EntityDied -= OnEntityDied;
            _eventBus.LoadCompleted -= OnLoadCompleted;
            _eventBus.FloorChanged -= OnFloorChanged;
            _eventBus.ProgressionChanged -= OnProgressionChanged;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        _content = content ?? gameManager?.Content;

        HUD.Bind(_gameManager, _eventBus);
        CombatLog.Bind(_gameManager, _eventBus);
        Inventory.Bind(_gameManager, _eventBus, _content, Tooltip);
        CharacterSheet.Bind(_gameManager, _eventBus, _content);
        LevelUpOverlay.Bind(_gameManager);
        DialogUI.ShopRequested -= OpenShopFromDialog;
        DialogUI.ShopRequested += OpenShopFromDialog;
        ShopUI.Bind(_gameManager, _eventBus, _content);
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
        InputHandler.InteractRequested -= Interact;
        InputHandler.InteractRequested += Interact;
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
            _eventBus.ProgressionChanged += OnProgressionChanged;
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

        UpdateLevelUpOverlay();
        RefreshInputGate();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
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
        AddIfMissing(LevelUpOverlay);
        AddIfMissing(DialogUI);
        AddIfMissing(ShopUI);
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

    private GameManager? ResolveGameManager()
    {
        return GetNodeOrNull<GameManager>("/root/GameManager")
            ?? AutoloadResolver.Resolve<GameManager>(this, "GameManager");
    }

    private EventBus? ResolveEventBus()
    {
        return GetNodeOrNull<EventBus>("/root/EventBus")
            ?? AutoloadResolver.Resolve<EventBus>(this, "EventBus");
    }

    private ContentDatabase? ResolveContentAutoload()
    {
        return GetNodeOrNull<ContentDatabase>("/root/ContentDatabase")
            ?? AutoloadResolver.Resolve<ContentDatabase>(this, "ContentDatabase");
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

        if (LevelUpOverlay.Visible)
        {
            var handled = LevelUpOverlay.HandleKey(key);
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

        if (DialogUI.Visible)
        {
            var handled = DialogUI.HandleKey(key);
            if (handled)
            {
                RefreshInputGate();
            }

            return handled;
        }

        if (ShopUI.Visible)
        {
            var handled = ShopUI.HandleKey(key);
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
        LevelUpOverlay.Close();
        DialogUI.Close();
        ShopUI.Close();
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

    private void OnProgressionChanged(EntityId entityId)
    {
        if (_gameManager?.World?.Player?.Id != entityId)
        {
            return;
        }

        UpdateLevelUpOverlay();
        RefreshInputGate();
    }

    private void OnGameStarted()
    {
        _enemiesKilled = 0;
        PauseMenu.Close();
        Inventory.Close();
        CharacterSheet.Close();
        LevelUpOverlay.Close();
        DialogUI.Close();
        ShopUI.Close();
        GameOverScreen.Close();
        HelpOverlay.Close();
        Tooltip.Hide();
        CombatLog.RefreshConsole();
        UpdateLevelUpOverlay();
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
        LevelUpOverlay.Close();
        DialogUI.Close();
        ShopUI.Close();
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
        LevelUpOverlay.Close();
        DialogUI.Close();
        ShopUI.Close();
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
        else if (ShopUI.Visible)
        {
            ShopUI.Close();
        }
        else if (DialogUI.Visible)
        {
            DialogUI.Close();
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
        LevelUpOverlay.Close();
        DialogUI.Close();
        ShopUI.Close();
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
        LevelUpOverlay.Close();
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
        if (_gameManager?.CurrentState == GameManager.GameState.Playing)
        {
            OnGameStarted();
            return;
        }

        MainMenu.Open();
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
        LevelUpOverlay.Close();
        DialogUI.Close();
        ShopUI.Close();
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
            && !LevelUpOverlay.Visible
            && !Inventory.Visible
            && !CharacterSheet.Visible
            && !DialogUI.Visible
            && !ShopUI.Visible
            && !GameOverScreen.Visible
            && !HelpOverlay.Visible
            && !DevToolsWorkbench.Visible
            && !DebugConsole.Visible);
    }

    private void RefreshGameplayChromeVisibility()
    {
        var suppressGameplayChrome = MainMenu.Visible
            || PauseMenu.Visible
            || LevelUpOverlay.Visible
            || Inventory.Visible
            || CharacterSheet.Visible
            || DialogUI.Visible
            || ShopUI.Visible
            || GameOverScreen.Visible
            || HelpOverlay.Visible
            || DevToolsWorkbench.Visible
            || DebugConsole.Visible;

        Minimap.SetSuppressed(suppressGameplayChrome);
        CombatLog.SetSuppressed(suppressGameplayChrome);
    }

    private void Interact()
    {
        if (MainMenu.Visible || PauseMenu.Visible || GameOverScreen.Visible || HelpOverlay.Visible || DevToolsWorkbench.Visible || DebugConsole.Visible)
        {
            return;
        }

        if (DialogUI.Visible)
        {
            DialogUI.Close();
            RefreshInputGate();
            return;
        }

        if (ShopUI.Visible)
        {
            ShopUI.Close();
            RefreshInputGate();
            return;
        }

        Inventory.Close();
        CharacterSheet.Close();
        Tooltip.Hide();

        var context = _gameManager?.GetInteractionContext();
        if (context is null)
        {
            _eventBus?.EmitLogMessage("No one nearby wants to talk.");
            RefreshInputGate();
            return;
        }

        DialogUI.Open(context);
        RefreshInputGate();
    }

    private void OpenShopFromDialog(EntityId merchantId)
    {
        DialogUI.Close();
        ShopUI.Open(merchantId);
        RefreshInputGate();
    }

    private void UpdateLevelUpOverlay()
    {
        var progression = _gameManager?.World?.Player?.GetComponent<ProgressionComponent>();
        var choices = _gameManager?.GetAvailablePerkChoices();
        if (progression is null || progression.UnspentPerkChoices <= 0 || choices is null || choices.Count == 0)
        {
            LevelUpOverlay.Close();
            return;
        }

        PauseMenu.Close();
        Inventory.Close();
        CharacterSheet.Close();
        DialogUI.Close();
        ShopUI.Close();
        Tooltip.Hide();
        if (!LevelUpOverlay.Visible)
        {
            LevelUpOverlay.Open();
        }
        else
        {
            LevelUpOverlay.Refresh();
        }
    }
}