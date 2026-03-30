using Godot;

namespace Godotussy;

public sealed record GameOverSummary(string PlayerName, int FloorReached, int EnemiesKilled, int TurnsTaken);

public partial class GameOverScreen : MenuBase
{
    private GameOverSummary _summary = new("Player", 0, 0, 0);

    public event System.Action? RetryRequested;

    public event System.Action? MainMenuRequested;

    public GameOverScreen()
    {
        Name = "GameOverScreen";
        Title = "YOU DIED";
        ConfigureOptions("Try Again", "Main Menu", "Quit");
        Visible = false;
    }

    public void Open(GameOverSummary summary)
    {
        _summary = summary;
        base.Open();
    }

    protected override string BuildBodyText()
    {
        return $"{_summary.PlayerName}\nFloor Reached: {_summary.FloorReached}\nEnemies Killed: {_summary.EnemiesKilled}\nTurns Taken: {_summary.TurnsTaken}";
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
            case 2:
                GetTree().Quit();
                break;
        }
    }
}