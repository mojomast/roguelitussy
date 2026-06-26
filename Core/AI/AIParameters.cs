namespace Roguelike.Core;

public sealed record AIParameters
{
    public float? FleeHpPct { get; init; }

    public int? AggroRange { get; init; }

    public bool? WanderWhenIdle { get; init; }

    public int? PreferredRange { get; init; }

    public int? MinRange { get; init; }

    public int? PatrolRadius { get; init; }

    public int? SupportRange { get; init; }

    public int? GroupAggroRange { get; init; }

    public bool? PhaseThroughWalls { get; init; }

    public static AIParameters Empty { get; } = new();
}
