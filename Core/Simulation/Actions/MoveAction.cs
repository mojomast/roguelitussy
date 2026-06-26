using System;

namespace Roguelike.Core;

public sealed class MoveAction : IAction
{
    public MoveAction(EntityId actorId, Position delta)
    {
        ActorId = actorId;
        Delta = delta;
    }

    public EntityId ActorId { get; }

    public Position Delta { get; }

    public ActionType Type => ActionType.Move;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor is null || !actor.IsAlive)
        {
            return ActionResult.Invalid;
        }

        if (!IsStep(Delta))
        {
            return ActionResult.Invalid;
        }

        var target = actor.Position + Delta;
        if (!world.InBounds(target))
        {
            return ActionResult.Blocked;
        }

        var phased = StatusEffectProcessor.HasFlag(actor, "phase_through_walls");

        if (!phased && Math.Abs(Delta.X) == 1 && Math.Abs(Delta.Y) == 1)
        {
            var adjacentX = actor.Position.Offset(Delta.X, 0);
            var adjacentY = actor.Position.Offset(0, Delta.Y);
            if (!world.IsWalkable(adjacentX) && !world.IsWalkable(adjacentY))
            {
                return ActionResult.Blocked;
            }
        }

        var occupant = world.GetEntityAt(target);
        if (occupant is not null && occupant.BlocksMovement)
        {
            return CanSwapWith(actor, occupant) ? ActionResult.Success : ActionResult.Blocked;
        }

        if (!phased && !world.IsWalkable(target))
        {
            return ActionResult.Blocked;
        }

        return ActionResult.Success;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var validation = Validate(world);
        if (validation != ActionResult.Success)
        {
            return ActionOutcome.Fail(validation);
        }

        var actor = world.GetEntity(ActorId)!;
        var from = actor.Position;
        var target = from + Delta;
        var phased = StatusEffectProcessor.HasFlag(actor, "phase_through_walls");

        if (world.GetEntityAt(target) is { } occupant)
        {
            if (!CanSwapWith(actor, occupant) || !world.TrySwapEntities(actor.Id, occupant.Id))
            {
                return ActionOutcome.Fail(ActionResult.Blocked);
            }

            var swapOutcome = new ActionOutcome
            {
                Result = ActionResult.Success,
                DirtyPositions = { from, target },
                LogMessages = { $"{actor.Name} swaps places with {occupant.Name}." },
            };

            HazardProcessor.OnEntityEnteredTile(world, actor, swapOutcome);
            HazardProcessor.OnEntityEnteredTile(world, occupant, swapOutcome);
            return swapOutcome;
        }

        if (phased && !world.IsWalkable(target))
        {
            actor.Position = target;
            world.UpdateEntityPosition(ActorId, from, target);
            var phasedOutcome = new ActionOutcome
            {
                Result = ActionResult.Success,
                DirtyPositions = { from, target },
                LogMessages = { $"{actor.Name} moves to {target}." },
            };

            HazardProcessor.OnEntityEnteredTile(world, actor, phasedOutcome);
            return phasedOutcome;
        }

        if (!world.MoveEntity(ActorId, target))
        {
            return ActionOutcome.Fail(ActionResult.Blocked);
        }

        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { from, target },
            LogMessages = { $"{actor.Name} moves to {target}." },
        };

        HazardProcessor.OnEntityEnteredTile(world, actor, outcome);
        return outcome;
    }

    public int GetEnergyCost() => 1000;

    private static bool IsStep(Position delta) => delta != Position.Zero && Math.Abs(delta.X) <= 1 && Math.Abs(delta.Y) <= 1;

    private static bool CanSwapWith(IEntity actor, IEntity occupant)
    {
        return actor.Faction == Faction.Player
            && occupant.Faction == Faction.Neutral
            && occupant.GetComponent<NpcComponent>() is not null
            && actor.IsAlive
            && occupant.IsAlive;
    }
}
