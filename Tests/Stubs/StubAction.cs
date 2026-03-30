using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public sealed class StubAction : IAction
{
    public StubAction(EntityId actorId, ActionType type = ActionType.Wait, int energyCost = 1000)
    {
        ActorId = actorId;
        Type = type;
        EnergyCost = energyCost;
    }

    public EntityId ActorId { get; }

    public ActionType Type { get; }

    public int EnergyCost { get; }

    public ActionResult Validate(IWorldState world) => world.GetEntity(ActorId) is null ? ActionResult.Invalid : ActionResult.Success;

    public ActionOutcome Execute(WorldState world) => ActionOutcome.Ok();

    public int GetEnergyCost() => EnergyCost;
}
