namespace Roguelike.Core;

public static class BrainFactory
{
    public static IBrain Create(string brainType)
    {
        var profile = AIProfiles.Get(brainType);
        return brainType switch
        {
            "melee_rusher" => new MeleeRusherBrain(profile),
            "ranged_kiter" => new RangedKiterBrain(profile),
            "patrol_guard" => new PatrolGuardBrain(profile),
            "fleeing" => new FleeingBrain(profile),
            "ambush" => new AmbushBrain(profile),
            "support" => new SupportBrain(profile),
            _ => new MeleeRusherBrain(profile),
        };
    }

    public static IBrain Create(EnemyTemplate template)
    {
        var profile = AIProfiles.ForTemplate(template);
        return template.BrainType switch
        {
            "melee_rusher" => new MeleeRusherBrain(profile),
            "ranged_kiter" => new RangedKiterBrain(profile),
            "patrol_guard" => new PatrolGuardBrain(profile),
            "fleeing" => new FleeingBrain(profile),
            "ambush" => new AmbushBrain(profile),
            "support" => new SupportBrain(profile),
            _ => new MeleeRusherBrain(profile),
        };
    }
}
