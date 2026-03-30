using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class GameLoop
{
    private readonly StatusEffectProcessor _statusProcessor = new();

    public ActionOutcome ProcessRound(WorldState world, ITurnScheduler scheduler, Func<IEntity, IAction> getAction)
    {
        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            CombatEvents = new List<CombatEvent>(),
            LogMessages = new List<string>(),
            DirtyPositions = new List<Position>()
        };

        scheduler.BeginRound(world);

        while (scheduler.HasNextActor())
        {
            var actor = scheduler.GetNextActor();
            if (actor == null || !actor.IsAlive)
                continue;

            var action = getAction(actor);
            var validation = action.Validate(world);

            if (validation != ActionResult.Success)
            {
                outcome.LogMessages.Add($"{actor.Name}: action {action.Type} failed ({validation})");
                scheduler.ConsumeEnergy(actor.Id, action.GetEnergyCost());
                continue;
            }

            var actionResult = action.Execute(world);
            outcome.CombatEvents.AddRange(actionResult.CombatEvents);
            outcome.LogMessages.AddRange(actionResult.LogMessages);
            outcome.DirtyPositions.AddRange(actionResult.DirtyPositions);

            scheduler.ConsumeEnergy(actor.Id, action.GetEnergyCost());
        }

        // Tick status effects for all entities
        foreach (var entity in world.Entities)
        {
            if (entity.IsAlive)
                _statusProcessor.Process(entity, world);
        }

        scheduler.EndRound(world);

        return outcome;
    }
}
