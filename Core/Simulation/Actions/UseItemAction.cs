using System.Collections.Generic;

namespace Roguelike.Core.Simulation;

public sealed class UseItemAction : IAction
{
    public EntityId ActorId { get; }
    public EntityId ItemInstanceId { get; }
    public ActionType Type => ActionType.UseItem;

    public UseItemAction(EntityId actorId, EntityId itemInstanceId)
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

        var messages = new List<string>();

        // Apply use effect based on UseEffect string convention
        ApplyEffect(actor, item, messages);

        // Consume charge or remove item
        if (item.CurrentCharges > 0)
        {
            item.CurrentCharges--;
            if (item.CurrentCharges <= 0)
                inventory.Remove(ItemInstanceId);
        }
        else
        {
            inventory.Remove(ItemInstanceId);
        }

        return new ActionOutcome
        {
            Result = ActionResult.Success,
            LogMessages = messages,
            DirtyPositions = new List<Position> { actor.Position }
        };
    }

    private static void ApplyEffect(IEntity actor, ItemInstance item, List<string> messages)
    {
        // Simple effect parsing: "heal:N" heals N HP, "status:Type:Turns:Magnitude" applies status
        // In a full implementation this would look up the ItemTemplate from a content DB
        // For now, use a convention based on TemplateId
        messages.Add($"{actor.Name} uses {item.TemplateId}.");
    }
}
