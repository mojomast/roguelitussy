using Godot;

namespace Roguelike.Godot;

public partial class GameOverScreen : CanvasLayer
{
    private PanelContainer _panel = null!;
    private Label _titleLabel = null!;
    private Label _depthLabel = null!;
    private Label _turnsLabel = null!;
    private Button _mainMenuButton = null!;
    private Button _quitButton = null!;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("%GameOverPanel");
        _titleLabel = GetNode<Label>("%GameOverTitle");
        _depthLabel = GetNode<Label>("%FinalDepthLabel");
        _turnsLabel = GetNode<Label>("%TurnsSurvivedLabel");
        _mainMenuButton = GetNode<Button>("%MainMenuButton");
        _quitButton = GetNode<Button>("%QuitButton");

        _mainMenuButton.Pressed += OnMainMenuPressed;
        _quitButton.Pressed += OnQuitPressed;

        _panel.Visible = false;

        EventBus.Instance.GameOver += OnGameOver;
    }

    public override void _ExitTree()
    {
        _mainMenuButton.Pressed -= OnMainMenuPressed;
        _quitButton.Pressed -= OnQuitPressed;
        EventBus.Instance.GameOver -= OnGameOver;
    }

    private void OnGameOver(int finalDepth, int turnsSurvived)
    {
        _titleLabel.Text = "GAME OVER";
        _depthLabel.Text = $"Final Depth: {finalDepth}";
        _turnsLabel.Text = $"Turns Survived: {turnsSurvived}";
        _panel.Visible = true;
    }

    private void OnMainMenuPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
