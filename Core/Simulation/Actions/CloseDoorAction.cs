namespace Roguelike.Core;

public sealed class CloseDoorAction : IAction
{
    public CloseDoorAction(EntityId actorId, Position doorPosition)
    {
        ActorId = actorId;
        DoorPosition = doorPosition;
    }

    public EntityId ActorId { get; }

    public Position DoorPosition { get; }

    public ActionType Type => ActionType.CloseDoor;

    public ActionResult Validate(IWorldState world)
    {
        if (world is not WorldState mutableWorld)
        {
            return ActionResult.Invalid;
        }

        var actor = world.GetEntity(ActorId);
        if (actor is null || actor.Position.ChebyshevTo(DoorPosition) > 1)
        {
            return ActionResult.Invalid;
        }

        if (world.GetEntityAt(DoorPosition) is not null)
        {
            return ActionResult.Blocked;
        }

        return world.GetTile(DoorPosition) == TileType.Door && mutableWorld.IsDoorOpen(DoorPosition)
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

        world.SetDoorOpen(DoorPosition, false);
        return new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { DoorPosition },
            LogMessages = { "Door closed." },
        };
    }

    public int GetEnergyCost() => 500;
}