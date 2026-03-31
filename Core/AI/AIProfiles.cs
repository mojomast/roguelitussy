namespace Roguelike.Core;

public sealed record AIProfile(
    string Name,
    float AggressionWeight,
    float ChaseWeight,
    float PatrolWeight,
    float FleeWeight,
    float WaitWeight,
    float FleeThreshold,
    int IdleTurnsBeforePatrol,
    int PatrolRadius,
    int MaxPatrolSteps,
    int PreferredRange,
    bool CanFlee,
    bool PatrolsWhenIdle);

public static class AIProfiles
{
    public static AIProfile MeleeRusher { get; } = new(
        "melee_rusher",
        1.00f,
        0.95f,
        0.35f,
        0.10f,
        0.55f,
        0.25f,
        3,
        8,
        8,
        1,
        CanFlee: false,
        PatrolsWhenIdle: true);

    public static AIProfile RangedKiter { get; } = new(
        "ranged_kiter",
        0.75f,
        0.70f,
        0.40f,
        1.10f,
        0.30f,
        0.35f,
        3,
        10,
        8,
        3,
        CanFlee: true,
        PatrolsWhenIdle: true);

    public static AIProfile PatrolGuard { get; } = new(
        "patrol_guard",
        0.85f,
        0.80f,
        0.65f,
        0.15f,
        0.20f,
        0.25f,
        3,
        10,
        10,
        1,
        CanFlee: false,
        PatrolsWhenIdle: true);

    public static AIProfile Fleeing { get; } = new(
        "fleeing",
        0.55f,
        0.55f,
        0.45f,
        1.25f,
        0.15f,
        0.50f,
        3,
        10,
        8,
        2,
        CanFlee: true,
        PatrolsWhenIdle: true);

    public static AIProfile Ambush { get; } = new(
        "ambush",
        1.20f,
        0.60f,
        0.50f,
        0.30f,
        0.90f,
        0.20f,
        5,
        6,
        6,
        1,
        CanFlee: false,
        PatrolsWhenIdle: false);

    public static AIProfile Support { get; } = new(
        "support",
        0.30f,
        0.40f,
        0.30f,
        0.80f,
        0.40f,
        0.40f,
        3,
        8,
        8,
        4,
        CanFlee: true,
        PatrolsWhenIdle: true);

    public static AIProfile Get(string brainType)
    {
        return brainType switch
        {
            "melee_rusher" => MeleeRusher,
            "ranged_kiter" => RangedKiter,
            "patrol_guard" => PatrolGuard,
            "fleeing" => Fleeing,
            "ambush" => Ambush,
            "support" => Support,
            _ => MeleeRusher,
        };
    }
}