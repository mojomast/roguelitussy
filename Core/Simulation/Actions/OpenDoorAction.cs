using System.Collections.Generic;

namespace Roguelike.Core.Simulation;

public sealed class OpenDoorAction : IAction
{
    public EntityId ActorId { get; }
    public Position DoorPosition { get; }
    public ActionType Type => ActionType.OpenDoor;

    public OpenDoorAction(EntityId actorId, Position doorPosition)
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

        if (world.GetTile(DoorPosition) != TileType.Door)
            return ActionResult.Invalid;

        return ActionResult.Success;
    }

    public ActionOutcome Execute(WorldState world)
    {
        world.SetTile(DoorPosition, TileType.Floor);

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            LogMessages = new List<string> { "You open the door." },
            DirtyPositions = new List<Position> { DoorPosition }
        };
    }
}
