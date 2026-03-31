namespace Roguelike.Core;

public sealed class IdentityComponent
{
    public string RaceId { get; set; } = "human";

    public string GenderId { get; set; } = "neutral";

    public string AppearanceId { get; set; } = "default";

    public string SpriteVariantId { get; set; } = "default";
}
