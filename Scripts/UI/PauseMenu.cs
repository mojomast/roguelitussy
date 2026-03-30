using Godot;

namespace Godotussy;

public partial class PauseMenu : MenuBase
{
    private EventBus? _eventBus;

    public event System.Action? ResumeRequested;

    public event System.Action? CharacterSheetRequested;

    public event System.Action? MainMenuRequested;

    public PauseMenu()
    {
        Name = "PauseMenu";
        Title = "PAUSED";
        ConfigureOptions("Resume", "Save Slot 1", "Save Slot 2", "Save Slot 3", "Character Sheet", "Main Menu", "Quit");
        Visible = false;
    }

    public void Bind(EventBus? eventBus)
    {
        _eventBus = eventBus;
    }

    protected override string BuildBodyText()
    {
        return "Game input is paused while this overlay is open.";
    }

    protected override void ActivateSelected()
    {
        switch (SelectedIndex)
        {
            case 0:
                Close();
                ResumeRequested?.Invoke();
                break;
            case 1:
                _eventBus?.EmitSaveRequested(1);
                break;
            case 2:
                _eventBus?.EmitSaveRequested(2);
                break;
            case 3:
                _eventBus?.EmitSaveRequested(3);
                break;
            case 4:
                CharacterSheetRequested?.Invoke();
                break;
            case 5:
                Close();
                MainMenuRequested?.Invoke();
                break;
            case 6:
                GetTree().Quit();
                break;
        }
    }

    protected override void Cancel()
    {
        Close();
        ResumeRequested?.Invoke();
    }
}