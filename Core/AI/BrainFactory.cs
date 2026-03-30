using System;
using System.Collections.Generic;
using Roguelike.Core.AI.Brains;

namespace Roguelike.Core.AI;

public sealed class BrainFactory
{
    private readonly CombatResolver _resolver;

    public BrainFactory(CombatResolver resolver)
    {
        _resolver = resolver;
    }

    public IBrain CreateBrain(string brainType)
    {
        return brainType switch
        {
            "melee_rusher" => new MeleeRusherBrain(_resolver),
            "ranged_kiter" => new RangedKiterBrain(),
            "patrol_guard" => new PatrolGuardBrain(_resolver),
            "fleeing" => new FleeingBrain(),
            _ => new MeleeRusherBrain(_resolver),
        };
    }
}
