namespace Roguelike.Core.Simulation;

public sealed class WaitAction : IAction
{
    public EntityId ActorId { get; }
    public ActionType Type => ActionType.Wait;

    public WaitAction(EntityId actorId)
    {
        ActorId = actorId;
    }

    public int GetEnergyCost() => 1000;

    public ActionResult Validate(IWorldState world) => ActionResult.Success;

    public ActionOutcome Execute(WorldState world) => ActionOutcome.Ok();
}
