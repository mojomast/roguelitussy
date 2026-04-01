namespace Roguelike.Core;

public sealed class NpcComponent
{
    public string TemplateId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string DialogueId { get; set; } = string.Empty;
}