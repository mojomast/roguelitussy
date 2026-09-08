using System;

namespace Roguelike.Core;

public sealed class WaitAction : IAction
{
    public WaitAction(EntityId actorId)
    {
        ActorId = actorId;
    }

    public EntityId ActorId { get; }

    public ActionType Type => ActionType.Wait;

    public ActionResult Validate(IWorldState world) => world.GetEntity(ActorId) is null ? ActionResult.Invalid : ActionResult.Success;

    public ActionOutcome Execute(WorldState world)
    {
        if (Validate(world) != ActionResult.Success)
        {
            return ActionOutcome.Fail(ActionResult.Invalid);
        }

        var actor = world.GetEntity(ActorId)!;
        var outcome = new ActionOutcome { Result = ActionResult.Success };
        if (actor.IsAlive
            && actor.Stats.HP < actor.Stats.MaxHP
            && !StatusEffectProcessor.HasHarmfulTickingEffect(actor, world.ContentDatabase))
        {
            actor.Stats.HP = Math.Min(actor.Stats.MaxHP, actor.Stats.HP + 1);
            outcome.LogMessages.Add($"{actor.Name} recovers 1 HP while waiting.");
        }
        else
        {
            outcome.LogMessages.Add("Waiting...");
        }

        return outcome;
    }

    public int GetEnergyCost() => 1000;
}
