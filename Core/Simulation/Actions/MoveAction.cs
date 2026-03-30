using System.Collections.Generic;

namespace Roguelike.Core.Simulation;

public sealed class MoveAction : IAction
{
    public EntityId ActorId { get; }
    public ActionType Type => ActionType.Move;
    public Position Target { get; }

    public MoveAction(EntityId actorId, Position target)
    {
        ActorId = actorId;
        Target = target;
    }

    public int GetEnergyCost() => 1000;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null || !actor.IsAlive)
            return ActionResult.Invalid;

        if (actor.Position.ChebyshevTo(Target) != 1)
            return ActionResult.Invalid;

        if (!world.IsWalkable(Target))
            return ActionResult.Blocked;

        return ActionResult.Success;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null)
            return ActionOutcome.Fail(ActionResult.Invalid);

        var oldPos = actor.Position;
        actor.Position = Target;
        world.UpdateEntityPosition(ActorId, oldPos, Target);

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = new List<Position> { oldPos, Target }
        };
    }
}
