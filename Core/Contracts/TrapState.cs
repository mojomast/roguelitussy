namespace Roguelike.Core;

internal sealed class LegacyTrapState
{
    public Position Position { get; set; }

    public string TrapId { get; set; } = string.Empty;

    public int Damage { get; set; }

    public string StatusEffect { get; set; } = string.Empty;

    public bool Disarmed { get; set; }

    public bool Triggered { get; set; }
}
