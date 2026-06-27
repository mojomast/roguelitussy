using Godot;

namespace Godotussy;

public partial class PauseMenu : MenuBase
{
    private enum PauseAction
    {
        None,
        Resume,
        Save1,
        Save2,
        Save3,
        Character,
        Help,
        DevTools,
        Title,
        Quit,
    }

    private readonly System.Collections.Generic.List<PauseAction> _actions = new();
    private EventBus? _eventBus;

    public event System.Action? ResumeRequested;

    public event System.Action? CharacterSheetRequested;

    public event System.Action? HelpRequested;

    public event System.Action? DevToolsRequested;

    public event System.Action? MainMenuRequested;

    public PauseMenu()
    {
        Name = "PauseMenu";
        Title = "PAUSED";
        RebuildOptions();
        Visible = false;
    }

    public void Bind(EventBus? eventBus)
    {
        _eventBus = eventBus;
    }

    protected override string BuildBodyText()
    {
        return string.Join(
            "\n",
            "Expedition command is paused.",
            "Resume keeps the current turn state intact.",
            "Save slots preserve active and cached floors.",
            "Review, Help, and Workshop are safe tools.",
            "Return to Title or Quit ends this shell session.");
    }

    protected override string BuildFooterText()
    {
        return "Up/Down choose  Enter confirm  H help  T workshop  Esc resume";
    }

    protected override void ActivateSelected()
    {
        switch (SelectedIndex >= 0 && SelectedIndex < _actions.Count ? _actions[SelectedIndex] : PauseAction.None)
        {
            case PauseAction.Resume:
                Close();
                ResumeRequested?.Invoke();
                break;
            case PauseAction.Save1:
                _eventBus?.EmitSaveRequested(1);
                break;
            case PauseAction.Save2:
                _eventBus?.EmitSaveRequested(2);
                break;
            case PauseAction.Save3:
                _eventBus?.EmitSaveRequested(3);
                break;
            case PauseAction.Character:
                CharacterSheetRequested?.Invoke();
                break;
            case PauseAction.Help:
                HelpRequested?.Invoke();
                break;
            case PauseAction.DevTools:
                DevToolsRequested?.Invoke();
                break;
            case PauseAction.Title:
                Close();
                MainMenuRequested?.Invoke();
                break;
            case PauseAction.Quit:
                GetTree().Quit();
                break;
        }
    }

    private void RebuildOptions()
    {
        _actions.Clear();
        ConfigureOptions();
        AddSection("RUN");
        AddOption("Resume", PauseAction.Resume);
        AddSection("SAVE");
        AddOption("Save: Slot 1", PauseAction.Save1);
        AddOption("Save: Slot 2", PauseAction.Save2);
        AddOption("Save: Slot 3", PauseAction.Save3);
        AddSection("TOOLS");
        AddOption("Review Character", PauseAction.Character);
        AddOption("Open Help", PauseAction.Help);
        AddOption("Developer Workshop", PauseAction.DevTools);
        AddSection("SYSTEM");
        AddOption("Return to Title", PauseAction.Title);
        AddOption("Quit Game", PauseAction.Quit);
    }

    private void AddSection(string title)
    {
        _actions.Add(PauseAction.None);
        ConfigureSectionHeader(title);
    }

    private void AddOption(string label, PauseAction action)
    {
        _actions.Add(action);
        ConfigureOption(label);
    }

    protected override bool HandleCustomKey(Key key)
    {
        if (key == Key.H)
        {
            HelpRequested?.Invoke();
            return true;
        }

        if (key == Key.T)
        {
            DevToolsRequested?.Invoke();
            return true;
        }

        return false;
    }

    protected override void Cancel()
    {
        Close();
        ResumeRequested?.Invoke();
    }
}
