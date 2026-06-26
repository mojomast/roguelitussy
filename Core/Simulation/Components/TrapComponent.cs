namespace Roguelike.Core;

public sealed class TrapComponent
{
    public string TemplateId { get; set; } = string.Empty;

    public bool IsArmed { get; set; } = true;

    public bool IsRevealed { get; set; }

    public int TriggerCount { get; set; }
}
