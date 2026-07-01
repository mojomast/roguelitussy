using System;

namespace Roguelike.Core;

public sealed class InteractShrineAction : IAction
{
    public InteractShrineAction(EntityId actorId, EntityId shrineId)
    {
        ActorId = actorId;
        ShrineId = shrineId;
    }

    public EntityId ActorId { get; }

    public EntityId ShrineId { get; }

    public ActionType Type => ActionType.UseItem;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var shrine = world.GetEntity(ShrineId);
        var shrineComponent = shrine?.GetComponent<ShrineComponent>();
        if (actor is null || shrine is null || shrineComponent is null || shrineComponent.IsUsed)
        {
            return ActionResult.Invalid;
        }

        if (actor.Position.ChebyshevTo(shrine.Position) > 1)
        {
            return ActionResult.Invalid;
        }

        return actor.Stats.HP > Math.Max(0, shrineComponent.HPCost)
            ? ActionResult.Success
            : ActionResult.Blocked;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var validation = Validate(world);
        if (validation != ActionResult.Success)
        {
            return ActionOutcome.Fail(validation);
        }

        var actor = world.GetEntity(ActorId)!;
        var shrine = world.GetEntity(ShrineId)!;
        var shrineComponent = shrine.GetComponent<ShrineComponent>()!;
        var hpCost = Math.Max(0, shrineComponent.HPCost);
        actor.Stats.HP -= hpCost;
        shrineComponent.IsUsed = true;
        shrineComponent.RewardChoicePending = true;
        shrineComponent.PendingRewardType = NormalizeShrineType(shrineComponent.ShrineType);
        shrineComponent.PendingActorId = ActorId;

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position, shrine.Position },
            LogMessages =
            {
                $"{actor.Name} offers {hpCost} HP to the shrine.",
                ResolveRewardMessage(shrineComponent.PendingRewardType),
            },
        };
    }

    public int GetEnergyCost() => 1000;

    private static string NormalizeShrineType(string shrineType)
    {
        return shrineType.Trim().ToLowerInvariant() switch
        {
            "stat" => "stat",
            "relic" => "relic",
            _ => "perk",
        };
    }

    private static string ResolveRewardMessage(string shrineType)
    {
        return shrineType switch
        {
            "stat" => "The shrine's runes flare, offering a stat boon.",
            "relic" => "A sacred light gathers, offering a relic choice.",
            _ => "The altar answers, offering a perk choice.",
        };
    }
}
