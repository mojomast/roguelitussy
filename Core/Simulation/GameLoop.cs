using System;

namespace Roguelike.Core;

public sealed class GameLoop
{
    public ActionOutcome ProcessRound(
        WorldState world,
        ITurnScheduler scheduler,
        Func<IEntity, IAction> getAction)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(getAction);

        var outcome = ActionOutcome.Ok();
        scheduler.BeginRound(world);

        while (scheduler.HasNextActor())
        {
            var actor = scheduler.GetNextActor();
            if (actor is null || !actor.IsAlive)
            {
                continue;
            }

            var action = getAction(actor);
            var validation = action.Validate(world);
            if (validation != ActionResult.Success)
            {
                outcome.LogMessages.Add($"{actor.Name}: action {action.Type} failed ({validation})");
                var failedTick = scheduler.ConsumeEnergy(actor.Id, action.GetEnergyCost());
                MergeTickResult(outcome, actor.Id, failedTick);
                continue;
            }

            var actionOutcome = action.Execute(world);
            outcome.CombatEvents.AddRange(actionOutcome.CombatEvents);
            outcome.LogMessages.AddRange(actionOutcome.LogMessages);
            outcome.DirtyPositions.AddRange(actionOutcome.DirtyPositions);
            outcome.ExpiredStatusEffects.AddRange(actionOutcome.ExpiredStatusEffects);
            outcome.BossPhaseTransitions.AddRange(actionOutcome.BossPhaseTransitions);
            var tickResult = scheduler.ConsumeEnergy(actor.Id, action.GetEnergyCost());
            MergeTickResult(outcome, actor.Id, tickResult);
        }

        scheduler.EndRound(world);

        if (world.Player is { IsAlive: true } player)
        {
            RelicProcessor.ProcessRoundEnd(world, player, outcome.LogMessages);
        }

        foreach (var entity in world.Entities)
        {
            entity.GetComponent<CooldownComponent>()?.TickAll();
        }

        return outcome;
    }

    private static void MergeTickResult(ActionOutcome outcome, EntityId actorId, StatusTickResult? tickResult)
    {
        if (tickResult is null)
        {
            return;
        }

        foreach (var expired in tickResult.ExpiredEffects)
        {
            outcome.ExpiredStatusEffects.Add((actorId, expired));
        }

        outcome.LogMessages.AddRange(tickResult.LogMessages);
        if (tickResult.Death is { } death)
        {
            outcome.DirtyPositions.Add(death.DropPosition);
        }
    }
}
