namespace Roguelike.Core;

public sealed class DescendAction : IAction
{
    public DescendAction(EntityId actorId)
    {
        ActorId = actorId;
    }

    public EntityId ActorId { get; }

    public ActionType Type => ActionType.Descend;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        return actor is null ? ActionResult.Invalid : world.GetTile(actor.Position) == TileType.StairsDown ? ActionResult.Success : ActionResult.Invalid;
    }

    public ActionOutcome Execute(WorldState world)
    {
        return Validate(world) == ActionResult.Success
            ? new ActionOutcome { Result = ActionResult.Success, LogMessages = { "You descend the stairs." } }
            : ActionOutcome.Fail(ActionResult.Invalid);
    }

    public int GetEnergyCost() => 0;
}