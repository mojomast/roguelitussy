namespace Roguelike.Core;

public sealed class PickupAction : IAction
{
    public PickupAction(EntityId actorId)
    {
        ActorId = actorId;
    }

    public EntityId ActorId { get; }

    public ActionType Type => ActionType.PickupItem;

    public ActionResult Validate(IWorldState world)
    {
        if (world is not WorldState mutableWorld)
        {
            return ActionResult.Invalid;
        }

        var actor = world.GetEntity(ActorId);
        var inventory = actor?.GetComponent<InventoryComponent>();
        if (actor is null || inventory is null)
        {
            return ActionResult.Invalid;
        }

        if (!inventory.HasSpace)
        {
            return ActionResult.Blocked;
        }

        return mutableWorld.HasGroundItems(actor.Position) ? ActionResult.Success : ActionResult.Invalid;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var validation = Validate(world);
        if (validation != ActionResult.Success)
        {
            return ActionOutcome.Fail(validation);
        }

        var actor = world.GetEntity(ActorId)!;
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var item = world.PickupItem(actor.Position);
        if (item is null)
        {
            return ActionOutcome.Fail(ActionResult.Invalid);
        }

        if (!inventory.Add(item))
        {
            world.DropItem(actor.Position, item);
            return ActionOutcome.Fail(ActionResult.Blocked);
        }

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position },
            LogMessages = { $"{actor.Name} picks up {item.TemplateId}." },
        };
    }

    public int GetEnergyCost() => 500;
}