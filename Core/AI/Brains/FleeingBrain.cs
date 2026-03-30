namespace Roguelike.Core;

public sealed class FleeingBrain : AIBrain
{
    public FleeingBrain()
        : base(AIProfiles.Fleeing)
    {
    }
}