using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubBrain : IBrain
{
    public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder)
    {
        return new StubWaitAction(self.Id);
    }
}

public sealed class StubWaitAction : IAction
{
    public EntityId ActorId { get; }
    public ActionType Type => ActionType.Wait;

    public StubWaitAction(EntityId actorId) => ActorId = actorId;

    public ActionResult Validate(IWorldState world) => ActionResult.Success;
    public ActionOutcome Execute(WorldState world) => ActionOutcome.Ok();
    public int GetEnergyCost() => 1000;
}
