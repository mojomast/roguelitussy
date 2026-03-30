using System.Collections.Generic;

namespace Roguelike.Core.Simulation;

public sealed class DropItemAction : IAction
{
    public EntityId ActorId { get; }
    public EntityId ItemInstanceId { get; }
    public ActionType Type => ActionType.DropItem;

    public DropItemAction(EntityId actorId, EntityId itemInstanceId)
    {
        ActorId = actorId;
        ItemInstanceId = itemInstanceId;
    }

    public int GetEnergyCost() => 500;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null || !actor.IsAlive)
            return ActionResult.Invalid;

        var inventory = actor.GetComponent<Inventory>();
        if (inventory == null)
            return ActionResult.Invalid;

        var item = inventory.Items.Find(i => i.InstanceId == ItemInstanceId);
        if (item == null)
            return ActionResult.Invalid;

        return ActionResult.Success;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null)
            return ActionOutcome.Fail(ActionResult.Invalid);

        var inventory = actor.GetComponent<Inventory>();
        var item = inventory?.Items.Find(i => i.InstanceId == ItemInstanceId);
        if (inventory == null || item == null)
            return ActionOutcome.Fail(ActionResult.Invalid);

        inventory.Remove(ItemInstanceId);

        // Create a ground entity for the dropped item
        var groundItem = new Entity(
            EntityId.New(),
            item.TemplateId,
            actor.Position,
            new Stats(),
            Faction.Neutral
        );
        groundItem.BlocksMovement = false;
        groundItem.SetComponent(item);
        world.AddEntity(groundItem);

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            LogMessages = new List<string> { $"{actor.Name} drops {item.TemplateId}." },
            DirtyPositions = new List<Position> { actor.Position }
        };
    }
}
