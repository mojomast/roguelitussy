namespace Roguelike.Core;

public sealed class Stats
{
    public int HP { get; set; }

    public int MaxHP { get; set; }

    public int Attack { get; set; }

    public int Accuracy { get; set; }

    public int Defense { get; set; }

    public int Evasion { get; set; }

    public int Speed { get; set; } = 100;

    public int ViewRadius { get; set; } = 8;

    public int Energy { get; set; }

    public bool IsAlive => HP > 0;

    public Stats Clone() => (Stats)MemberwiseClone();
}
