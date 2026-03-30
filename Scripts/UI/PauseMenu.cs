using Godot;

namespace Roguelike.Godot;

public partial class PauseMenu : CanvasLayer
{
    private PanelContainer _panel = null!;
    private Button _resumeButton = null!;
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Button _quitToMenuButton = null!;
    private ColorRect _dimBackground = null!;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("%PausePanel");
        _resumeButton = GetNode<Button>("%ResumeButton");
        _saveButton = GetNode<Button>("%SaveButton");
        _loadButton = GetNode<Button>("%LoadButton");
        _quitToMenuButton = GetNode<Button>("%QuitToMenuButton");
        _dimBackground = GetNode<ColorRect>("%DimBackground");

        _resumeButton.Pressed += OnResumePressed;
        _saveButton.Pressed += OnSavePressed;
        _loadButton.Pressed += OnLoadPressed;
        _quitToMenuButton.Pressed += OnQuitToMenuPressed;

        SetPaused(false);
    }

    public override void _ExitTree()
    {
        _resumeButton.Pressed -= OnResumePressed;
        _saveButton.Pressed -= OnSavePressed;
        _loadButton.Pressed -= OnLoadPressed;
        _quitToMenuButton.Pressed -= OnQuitToMenuPressed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") ||
            (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape))
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }

    private void TogglePause()
    {
        SetPaused(!_panel.Visible);
    }

    private void SetPaused(bool paused)
    {
        _panel.Visible = paused;
        _dimBackground.Visible = paused;
        GetTree().Paused = paused;
    }

    private void OnResumePressed()
    {
        SetPaused(false);
    }

    private void OnSavePressed()
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.SaveRequested, 0);
    }

    private void OnLoadPressed()
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.LoadRequested, 0);
    }

    private void OnQuitToMenuPressed()
    {
        SetPaused(false);
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }
}
