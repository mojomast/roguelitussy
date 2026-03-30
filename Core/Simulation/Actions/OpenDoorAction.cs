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

        return world.GetTile(DoorPosition) == TileType.Door && !mutableWorld.IsDoorOpen(DoorPosition)
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

        world.SetDoorOpen(DoorPosition, true);
        return new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { DoorPosition },
            LogMessages = { "Door opened." },
        };
    }

    public int GetEnergyCost() => 500;
}