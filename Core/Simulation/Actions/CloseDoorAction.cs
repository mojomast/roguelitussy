using System.Collections.Generic;

namespace Roguelike.Core.Simulation;

public sealed class CloseDoorAction : IAction
{
    public EntityId ActorId { get; }
    public Position DoorPosition { get; }
    public ActionType Type => ActionType.CloseDoor;

    public CloseDoorAction(EntityId actorId, Position doorPosition)
    {
        ActorId = actorId;
        DoorPosition = doorPosition;
    }

    public int GetEnergyCost() => 500;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null || !actor.IsAlive)
            return ActionResult.Invalid;

        if (actor.Position.ChebyshevTo(DoorPosition) != 1)
            return ActionResult.Invalid;

        if (world.GetTile(DoorPosition) != TileType.Floor)
            return ActionResult.Invalid;

        // Can't close if an entity is blocking
        if (world.GetEntityAt(DoorPosition) != null)
            return ActionResult.Blocked;

        return ActionResult.Success;
    }

    public ActionOutcome Execute(WorldState world)
    {
        world.SetTile(DoorPosition, TileType.Door);

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            LogMessages = new List<string> { "You close the door." },
            DirtyPositions = new List<Position> { DoorPosition }
        };
    }
}
