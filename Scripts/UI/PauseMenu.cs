using Godot;
using System.Globalization;

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
    private GameManager? _gameManager;

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
        Bind(null, eventBus);
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        _eventBus = eventBus;
        _gameManager = gameManager;
        RebuildMenuText();
    }

    protected override string BuildBodyText()
    {
        var stats = ResolveRunStats();
        return string.Join(
            "\n",
            "Expedition command is paused.",
            "Resume keeps the current turn state intact.",
            "Save slots preserve active and cached floors.",
            "Review, Help, and Workshop are safe tools.",
            "Return to Title or Quit ends this shell session.",
            string.Empty,
            BuildRunStatsText(stats));
    }

    public override void Open()
    {
        RebuildMenuText();
        base.Open();
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
        AddOption("Resume Run", PauseAction.Resume);
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

    private RunStats ResolveRunStats()
    {
        _gameManager ??= GetNodeOrNull<GameManager>("/root/GameManager")
            ?? AutoloadResolver.Resolve<GameManager>(this, "GameManager");
        return _gameManager?.CurrentRunStats ?? new RunStats("Rook", 0, 0, 0, 0, 0, 0, 0, "Unknown", string.Empty, 0);
    }

    private static string BuildRunStatsText(RunStats stats)
    {
        return string.Join(
            "\n",
            "─── CURRENT RUN ───────────────────",
            $"Floor    {FormatStat(stats.FloorReached),5}       Turns {FormatStat(stats.TotalTurns),8}",
            $"Enemies  {FormatStat(stats.EnemiesKilled),5}       Gold  {FormatStat(stats.GoldCollected),8}",
            $"Damage   {FormatStat(stats.DamageTaken),5}       Items {FormatStat(stats.ItemsFound),8}",
            $"Seed     {FormatStat(stats.Seed),5}",
            "───────────────────────────────────");
    }

    private static string FormatStat(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
