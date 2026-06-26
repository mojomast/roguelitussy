namespace Roguelike.Core;

public sealed class SupportBrain : AIBrain
{
    public SupportBrain()
        : base(AIProfiles.Support)
    {
    }

    public SupportBrain(AIProfile profile)
        : base(profile)
    {
    }
}
