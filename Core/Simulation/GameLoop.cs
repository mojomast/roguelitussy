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
                if (failedTick?.ExpiredEffects.Count > 0)
                {
                    foreach (var expired in failedTick.ExpiredEffects)
                    {
                        outcome.ExpiredStatusEffects.Add((actor.Id, expired));
                    }
                }

                continue;
            }

            var actionOutcome = action.Execute(world);
            outcome.CombatEvents.AddRange(actionOutcome.CombatEvents);
            outcome.LogMessages.AddRange(actionOutcome.LogMessages);
            outcome.DirtyPositions.AddRange(actionOutcome.DirtyPositions);
            var tickResult = scheduler.ConsumeEnergy(actor.Id, action.GetEnergyCost());
            if (tickResult?.ExpiredEffects.Count > 0)
            {
                foreach (var expired in tickResult.ExpiredEffects)
                {
                    outcome.ExpiredStatusEffects.Add((actor.Id, expired));
                }
            }
        }

        scheduler.EndRound(world);

        foreach (var entity in world.Entities)
        {
            entity.GetComponent<CooldownComponent>()?.TickAll();
        }

        return outcome;
    }
}
