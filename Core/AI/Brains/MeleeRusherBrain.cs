namespace Roguelike.Core;

public sealed class MeleeRusherBrain : AIBrain
{
    public MeleeRusherBrain()
        : base(AIProfiles.MeleeRusher)
    {
    }

    public MeleeRusherBrain(AIProfile profile)
        : base(profile)
    {
    }
}
