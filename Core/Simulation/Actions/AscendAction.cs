namespace Roguelike.Core;

public sealed class AscendAction : IAction
{
    public AscendAction(EntityId actorId)
    {
        ActorId = actorId;
    }

    public EntityId ActorId { get; }

    public ActionType Type => ActionType.Ascend;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        return actor is null ? ActionResult.Invalid : world.GetTile(actor.Position) == TileType.StairsUp ? ActionResult.Success : ActionResult.Invalid;
    }

    public ActionOutcome Execute(WorldState world)
    {
        return Validate(world) == ActionResult.Success
            ? new ActionOutcome { Result = ActionResult.Success, LogMessages = { "You ascend the stairs." } }
            : ActionOutcome.Fail(ActionResult.Invalid);
    }

    public int GetEnergyCost() => 0;
}