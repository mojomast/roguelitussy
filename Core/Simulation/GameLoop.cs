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
                scheduler.ConsumeEnergy(actor.Id, action.GetEnergyCost());
                continue;
            }

            var actionOutcome = action.Execute(world);
            outcome.CombatEvents.AddRange(actionOutcome.CombatEvents);
            outcome.LogMessages.AddRange(actionOutcome.LogMessages);
            outcome.DirtyPositions.AddRange(actionOutcome.DirtyPositions);
            scheduler.ConsumeEnergy(actor.Id, action.GetEnergyCost());
        }

        scheduler.EndRound(world);

        foreach (var entity in world.Entities)
        {
            entity.GetComponent<CooldownComponent>()?.TickAll();
        }

        return outcome;
    }
}