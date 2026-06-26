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
    int MinRange,
    int AggroRange,
    int SupportRange,
    int GroupAggroRange,
    bool CanFlee,
    bool PatrolsWhenIdle,
    bool PhaseThroughWalls);

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
        1,
        0,
        6,
        0,
        CanFlee: false,
        PatrolsWhenIdle: true,
        PhaseThroughWalls: false);

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
        1,
        0,
        6,
        0,
        CanFlee: true,
        PatrolsWhenIdle: true,
        PhaseThroughWalls: false);

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
        1,
        0,
        6,
        0,
        CanFlee: false,
        PatrolsWhenIdle: true,
        PhaseThroughWalls: false);

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
        1,
        0,
        6,
        0,
        CanFlee: true,
        PatrolsWhenIdle: true,
        PhaseThroughWalls: false);

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
        1,
        0,
        6,
        0,
        CanFlee: false,
        PatrolsWhenIdle: false,
        PhaseThroughWalls: false);

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
        1,
        0,
        6,
        0,
        CanFlee: true,
        PatrolsWhenIdle: true,
        PhaseThroughWalls: false);

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

    public static AIProfile ForTemplate(EnemyTemplate template)
    {
        var baseProfile = Get(template.BrainType);
        var parameters = template.AIParameters;

        var fleeThreshold = parameters.FleeHpPct.HasValue
            ? parameters.FleeHpPct.Value / 100f
            : baseProfile.FleeThreshold;

        var canFlee = parameters.FleeHpPct.HasValue
            ? parameters.FleeHpPct.Value > 0f
            : baseProfile.CanFlee;

        return baseProfile with
        {
            FleeThreshold = fleeThreshold,
            CanFlee = canFlee,
            AggroRange = parameters.AggroRange is > 0 ? parameters.AggroRange.Value : baseProfile.AggroRange,
            PatrolsWhenIdle = parameters.WanderWhenIdle ?? baseProfile.PatrolsWhenIdle,
            PreferredRange = parameters.PreferredRange is > 0 ? parameters.PreferredRange.Value : baseProfile.PreferredRange,
            MinRange = parameters.MinRange is > 0 ? parameters.MinRange.Value : baseProfile.MinRange,
            PatrolRadius = parameters.PatrolRadius is > 0 ? parameters.PatrolRadius.Value : baseProfile.PatrolRadius,
            SupportRange = parameters.SupportRange is > 0 ? parameters.SupportRange.Value : baseProfile.SupportRange,
            GroupAggroRange = parameters.GroupAggroRange is > 0 ? parameters.GroupAggroRange.Value : baseProfile.GroupAggroRange,
            PhaseThroughWalls = parameters.PhaseThroughWalls ?? baseProfile.PhaseThroughWalls,
        };
    }
}
