using System;

namespace Roguelike.Core.Content;

public sealed class DifficultyScaler
{
    public Stats ScaleStats(Stats baseStats, int depth)
    {
        var scaled = baseStats.Clone();
        scaled.HP = baseStats.HP + (int)(baseStats.HP * 0.10 * depth);
        scaled.MaxHP = baseStats.MaxHP + (int)(baseStats.MaxHP * 0.10 * depth);
        scaled.Attack = baseStats.Attack + depth / 2;
        scaled.Defense = baseStats.Defense + depth / 2;
        return scaled;
    }
}
