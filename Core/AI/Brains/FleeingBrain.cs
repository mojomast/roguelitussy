namespace Roguelike.Core;

public sealed class FleeingBrain : AIBrain
{
    public FleeingBrain()
        : base(AIProfiles.Fleeing)
    {
    }

    public FleeingBrain(AIProfile profile)
        : base(profile)
    {
    }
}
