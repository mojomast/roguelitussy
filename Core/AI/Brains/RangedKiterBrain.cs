namespace Roguelike.Core;

public sealed class RangedKiterBrain : AIBrain
{
    public RangedKiterBrain()
        : base(AIProfiles.RangedKiter)
    {
    }

    public RangedKiterBrain(AIProfile profile)
        : base(profile)
    {
    }
}
