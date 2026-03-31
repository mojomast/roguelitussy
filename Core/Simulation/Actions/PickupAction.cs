namespace Roguelike.Core;

public sealed class PickupAction : IAction
{
    public PickupAction(EntityId actorId, ItemTemplate? template = null)
    {
        ActorId = actorId;
        Template = template;
    }

    public EntityId ActorId { get; }

    public ItemTemplate? Template { get; }

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

        var firstGroundItem = mutableWorld.GetItemsAt(actor.Position).Count > 0 ? mutableWorld.GetItemsAt(actor.Position)[0] : null;
        if (firstGroundItem is null)
        {
            return ActionResult.Invalid;
        }

        if (!CanAccept(inventory, firstGroundItem))
        {
            return ActionResult.Blocked;
        }

        return ActionResult.Success;
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

        if (!AddItem(inventory, item))
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

    private bool CanAccept(InventoryComponent inventory, ItemInstance item)
    {
        return Template is not null && Template.MaxStack > 1
            ? inventory.CanAccept(item, Template.MaxStack)
            : inventory.HasSpace;
    }

    private bool AddItem(InventoryComponent inventory, ItemInstance item)
    {
        return Template is not null && Template.MaxStack > 1
            ? inventory.AddWithStacking(item, Template.MaxStack)
            : inventory.Add(item);
    }
}