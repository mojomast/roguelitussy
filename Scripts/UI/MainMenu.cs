using Godot;

namespace Roguelike.Godot;

public partial class MainMenu : CanvasLayer
{
    [Signal]
    public delegate void NewGameRequestedEventHandler();

    private Button _newGameButton = null!;
    private Button _loadGameButton = null!;
    private Button _quitButton = null!;

    public override void _Ready()
    {
        _newGameButton = GetNode<Button>("%NewGameButton");
        _loadGameButton = GetNode<Button>("%LoadGameButton");
        _quitButton = GetNode<Button>("%QuitButton");

        _newGameButton.Pressed += OnNewGamePressed;
        _loadGameButton.Pressed += OnLoadGamePressed;
        _quitButton.Pressed += OnQuitPressed;
    }

    public override void _ExitTree()
    {
        _newGameButton.Pressed -= OnNewGamePressed;
        _loadGameButton.Pressed -= OnLoadGamePressed;
        _quitButton.Pressed -= OnQuitPressed;
    }

    private void OnNewGamePressed()
    {
        EmitSignal(SignalName.NewGameRequested);
    }

    private void OnLoadGamePressed()
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.LoadRequested, 0);
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
