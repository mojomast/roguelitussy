namespace Roguelike.Core;

public sealed class PatrolGuardBrain : AIBrain
{
    public PatrolGuardBrain()
        : base(AIProfiles.PatrolGuard)
    {
    }
}