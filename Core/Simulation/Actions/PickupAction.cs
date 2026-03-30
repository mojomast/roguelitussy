using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core.Simulation;

public sealed class PickupAction : IAction
{
    public EntityId ActorId { get; }
    public EntityId ItemEntityId { get; }
    public ActionType Type => ActionType.PickupItem;

    public PickupAction(EntityId actorId, EntityId itemEntityId)
    {
        ActorId = actorId;
        ItemEntityId = itemEntityId;
    }

    public int GetEnergyCost() => 500;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null || !actor.IsAlive)
            return ActionResult.Invalid;

        var itemEntity = world.GetEntity(ItemEntityId);
        if (itemEntity == null)
            return ActionResult.Invalid;

        // Item must be at actor's position and be non-blocking (ground item)
        if (itemEntity.Position != actor.Position)
            return ActionResult.Invalid;

        var inventory = actor.GetComponent<Inventory>();
        if (inventory != null && inventory.IsFull)
            return ActionResult.Blocked;

        var item = itemEntity.GetComponent<ItemInstance>();
        if (item == null)
            return ActionResult.Invalid;

        return ActionResult.Success;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var itemEntity = world.GetEntity(ItemEntityId);
        if (actor == null || itemEntity == null)
            return ActionOutcome.Fail(ActionResult.Invalid);

        var item = itemEntity.GetComponent<ItemInstance>();
        if (item == null)
            return ActionOutcome.Fail(ActionResult.Invalid);

        var inventory = actor.GetComponent<Inventory>();
        if (inventory == null)
        {
            inventory = new Inventory();
            actor.SetComponent(inventory);
        }

        inventory.Add(item);
        world.RemoveEntity(ItemEntityId);

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            LogMessages = new List<string> { $"{actor.Name} picks up {item.TemplateId}." },
            DirtyPositions = new List<Position> { actor.Position }
        };
    }
}
