using System.Collections.Generic;

namespace Roguelike.Core.Simulation;

public sealed class DescendAction : IAction
{
    public EntityId ActorId { get; }
    public ActionType Type => ActionType.Descend;

    public DescendAction(EntityId actorId)
    {
        ActorId = actorId;
    }

    public int GetEnergyCost() => 1000;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null || !actor.IsAlive)
            return ActionResult.Invalid;

        if (world.GetTile(actor.Position) != TileType.StairsDown)
            return ActionResult.Invalid;

        return ActionResult.Success;
    }

    public ActionOutcome Execute(WorldState world)
    {
        world.Depth++;

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            LogMessages = new List<string> { "You descend deeper into the dungeon." }
        };
    }
}
