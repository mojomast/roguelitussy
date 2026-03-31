namespace Roguelike.Core;

public sealed class OpenDoorAction : IAction
{
    public OpenDoorAction(EntityId actorId, Position doorPosition)
    {
        ActorId = actorId;
        DoorPosition = doorPosition;
    }

    public EntityId ActorId { get; }

    public Position DoorPosition { get; }

    public ActionType Type => ActionType.OpenDoor;

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

        return world.GetTile(DoorPosition) == TileType.Door
            && !mutableWorld.IsDoorOpen(DoorPosition)
            && world.GetEntityAt(DoorPosition) is null
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
        var from = actor.Position;

        world.SetDoorOpen(DoorPosition, true);
        if (!world.MoveEntity(ActorId, DoorPosition))
        {
            world.SetDoorOpen(DoorPosition, false);
            return ActionOutcome.Fail(ActionResult.Blocked);
        }

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { from, DoorPosition },
            LogMessages = { $"{actor.Name} opens the door and moves through." },
        };
    }

    public int GetEnergyCost() => 1000;
}