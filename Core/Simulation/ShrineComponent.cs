namespace Roguelike.Core;

public sealed class ShrineComponent
{
    public string ShrineType { get; set; } = "perk";

    public int HPCost { get; set; } = 10;

    public bool IsUsed { get; set; }

    public bool RewardChoicePending { get; set; }

    public string PendingRewardType { get; set; } = string.Empty;

    public EntityId PendingActorId { get; set; }
}
