namespace Roguelike.Core;

public sealed class ProgressionComponent
{
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public int ExperienceToNextLevel { get; set; } = 50;
    public int UnspentStatPoints { get; set; }
    public int Kills { get; set; }

    public static int CalculateXpThreshold(int level)
    {
        return 50 * level * (level + 1) / 2;
    }

    public bool CanLevelUp => Experience >= ExperienceToNextLevel;
}
