namespace Roguelike.Core;

public sealed class AmbushBrain : AIBrain
{
    public AmbushBrain()
        : base(AIProfiles.Ambush)
    {
    }

    public AmbushBrain(AIProfile profile)
        : base(profile)
    {
    }
}
