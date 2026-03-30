namespace Roguelike.Core;

public sealed class WaitAction : IAction
{
    public WaitAction(EntityId actorId)
    {
        ActorId = actorId;
    }

    public EntityId ActorId { get; }

    public ActionType Type => ActionType.Wait;

    public ActionResult Validate(IWorldState world) => world.GetEntity(ActorId) is null ? ActionResult.Invalid : ActionResult.Success;

    public ActionOutcome Execute(WorldState world)
    {
        return Validate(world) == ActionResult.Success
            ? new ActionOutcome { Result = ActionResult.Success, LogMessages = { "Waiting..." } }
            : ActionOutcome.Fail(ActionResult.Invalid);
    }

    public int GetEnergyCost() => 1000;
}