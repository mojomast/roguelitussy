namespace Roguelike.Core;

public sealed class KillStreakComponent
{
    public int CurrentStreak { get; set; }

    public int HighestStreak { get; set; }

    public int BonusXpAwarded { get; set; }

    /// <summary>
    /// Ends the current streak. Clears the awarded-milestone marker alongside the streak so
    /// milestone XP can be earned again on the next streak.
    /// </summary>
    public void Reset()
    {
        CurrentStreak = 0;
        BonusXpAwarded = 0;
    }
}
