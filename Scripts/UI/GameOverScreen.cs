using Godot;

namespace Godotussy;

public sealed record GameOverSummary(string PlayerName, int FloorReached, int EnemiesKilled, int TurnsTaken);

public partial class GameOverScreen : MenuBase
{
    private RunStats _stats = new("Player", 0, 0, 0, 0, 0, 0, 0, "Unknown", string.Empty, 0);

    public event System.Action? RetryRequested;

    public event System.Action? MainMenuRequested;

    public GameOverScreen()
    {
        Name = "GameOverScreen";
        Title = "YOU DIED";
        ConfigureOptions("New Run", "Main Menu");
        Visible = false;
    }

    public void Open(GameOverSummary summary)
    {
        _stats = new RunStats(
            summary.PlayerName,
            summary.FloorReached,
            summary.TurnsTaken,
            summary.EnemiesKilled,
            0,
            0,
            0,
            0,
            "Unknown",
            string.Empty,
            0);
        Title = $"✝ {_stats.CharacterName.ToUpperInvariant()} HAS FALLEN";
        base.Open();
    }

    public void Open(RunStats stats)
    {
        _stats = stats;
        Title = $"✝ {_stats.CharacterName.ToUpperInvariant()} HAS FALLEN";
        base.Open();
    }

    public string BuildBodyMarkup() => BuildBodyText();

    protected override string BuildBodyText()
    {
        var muted = UiStyle.ToHex(UiStyle.MutedText());
        var parchment = UiStyle.ToHex(UiStyle.Parchment());
        var gold = UiStyle.ToHex(UiStyle.BrightGold());
        var danger = UiStyle.ToHex(UiStyle.DangerRed());
        var bestFind = string.IsNullOrWhiteSpace(_stats.BestItemName)
            ? string.Empty
            : $"\n[color={muted}]Best find:[/color] [color={parchment}]{ItemRarityPresentation.EscapeBBCode(_stats.BestItemName)} ({_stats.BestItemValue}g)[/color]";

        return $"[color={danger}][b]✝ {ItemRarityPresentation.EscapeBBCode(_stats.CharacterName).ToUpperInvariant()} HAS FALLEN[/b][/color]   [color={muted}][lb]SEED:{_stats.Seed}[rb][/color]\n" +
            $"────────────────────────────────────────\n" +
            $"[color={muted}]Delver:[/color] [color={parchment}]{ItemRarityPresentation.EscapeBBCode(_stats.CharacterName)}[/color]\n" +
            $"[color={muted}]Slain by:[/color] [color={parchment}]{ItemRarityPresentation.EscapeBBCode(_stats.CauseOfDeath)}[/color]    [color={muted}]Floor {_stats.FloorReached} · Turn {_stats.TotalTurns:N0}[/color]" +
            bestFind +
            $"\n────────────────────────────────────────\n" +
            $"[color={muted}]⚔  Enemies slain[/color]      [color={gold}]{_stats.EnemiesKilled:N0}[/color]\n" +
            $"[color={muted}]🎒 Items found[/color]        [color={gold}]{_stats.ItemsFound:N0}[/color]\n" +
            $"[color={muted}]💰 Gold collected[/color]    [color={gold}]{_stats.GoldCollected:N0}[/color]\n" +
            $"[color={muted}]💔 Damage taken[/color]      [color={gold}]{_stats.DamageTaken:N0}[/color]\n" +
            $"────────────────────────────────────────\n" +
            $"[color={muted}][i]{ResolveFlavorLine(_stats)}[/i][/color]";
    }

    protected override string BuildFooterText()
    {
        return "[ENTER] New Run              [ESC] Main Menu";
    }

    protected override bool HandleCustomKey(Key key)
    {
        if (key is Key.KpEnter)
        {
            ActivateSelected();
            return true;
        }

        return false;
    }

    protected override void Cancel()
    {
        Close();
        MainMenuRequested?.Invoke();
    }

    protected override void ActivateSelected()
    {
        switch (SelectedIndex)
        {
            case 0:
                Close();
                RetryRequested?.Invoke();
                break;
            case 1:
                Close();
                MainMenuRequested?.Invoke();
                break;
        }
    }

    private static string ResolveFlavorLine(RunStats stats)
    {
        if (stats.DamageTaken == 0)
        {
            return "A flawless descent into darkness.";
        }

        if (stats.FloorReached >= 6)
        {
            return "The depths claimed another brave soul.";
        }

        if (stats.EnemiesKilled == 0)
        {
            return "Fell without drawing blood.";
        }

        if (stats.GoldCollected > 500)
        {
            return "At least the gold was good.";
        }

        return "The dungeon swallows another delver.";
    }
}
