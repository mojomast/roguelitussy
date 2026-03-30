using Godot;

namespace Godotussy;

public partial class MainMenu : MenuBase
{
    private EventBus? _eventBus;
    private GameManager? _gameManager;

    public int PendingSeed { get; private set; } = 1337;

    public event System.Action? GameStarted;

    public MainMenu()
    {
        Name = "MainMenu";
        Title = "GODOTUSSY ROGUELIKE";
        ConfigureOptions("New Game", "Load Slot 1", "Load Slot 2", "Load Slot 3", "Quit");
        Visible = true;
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.LoadCompleted -= OnLoadCompleted;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.LoadCompleted += OnLoadCompleted;
        }

        if (_gameManager?.Seed > 0)
        {
            PendingSeed = _gameManager.Seed;
        }

        RebuildMenuText();
    }

    public void SetSeed(int seed)
    {
        PendingSeed = seed;
        RebuildMenuText();
    }

    protected override string BuildBodyText()
    {
        return $"Seed: {PendingSeed}\nUse Left/Right or +/- to adjust seed.";
    }

    protected override bool HandleCustomKey(Key key)
    {
        switch (key)
        {
            case Key.Left:
            case Key.Minus:
                PendingSeed = PendingSeed <= 1 ? 1 : PendingSeed - 1;
                RebuildMenuText();
                return true;
            case Key.Right:
            case Key.Plus:
                PendingSeed++;
                RebuildMenuText();
                return true;
            default:
                return false;
        }
    }

    protected override void ActivateSelected()
    {
        switch (SelectedIndex)
        {
            case 0:
                _gameManager?.StartNewGame(PendingSeed);
                _eventBus?.EmitLogMessage($"Starting new game with seed {PendingSeed}.");
                Close();
                GameStarted?.Invoke();
                break;
            case 1:
                _eventBus?.EmitLoadRequested(1);
                break;
            case 2:
                _eventBus?.EmitLoadRequested(2);
                break;
            case 3:
                _eventBus?.EmitLoadRequested(3);
                break;
            case 4:
                GetTree().Quit();
                break;
        }
    }

    protected override void Cancel()
    {
    }

    private void OnLoadCompleted(bool success)
    {
        if (success)
        {
            Close();
        }
    }
}