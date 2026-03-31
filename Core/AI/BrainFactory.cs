namespace Roguelike.Core;

public static class BrainFactory
{
    public static IBrain Create(string brainType)
    {
        return brainType switch
        {
            "melee_rusher" => new MeleeRusherBrain(),
            "ranged_kiter" => new RangedKiterBrain(),
            "patrol_guard" => new PatrolGuardBrain(),
            "fleeing" => new FleeingBrain(),
            "ambush" => new AmbushBrain(),
            "support" => new SupportBrain(),
            _ => new MeleeRusherBrain(),
        };
    }

    public static IBrain Create(EnemyTemplate template) => Create(template.BrainType);
}