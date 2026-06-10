namespace Roguelike.Core;

public sealed class PickupAction : IAction
{
    public PickupAction(EntityId actorId, ItemTemplate? template = null, bool autoEquipUpgrades = false)
    {
        ActorId = actorId;
        Template = template;
        AutoEquipUpgrades = autoEquipUpgrades;
    }

    public EntityId ActorId { get; }

    public ItemTemplate? Template { get; }

    public bool AutoEquipUpgrades { get; }

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

        if (!AddItem(world, inventory, item))
        {
            world.DropItem(actor.Position, item);
            return ActionOutcome.Fail(ActionResult.Blocked);
        }

        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position },
            LogMessages = { $"{actor.Name} picks up {item.TemplateId}." },
        };

        TryAutoEquip(world, actor, inventory, item, outcome);
        return outcome;
    }

    public int GetEnergyCost() => 500;

    private bool CanAccept(InventoryComponent inventory, ItemInstance item)
    {
        return Template is not null && Template.MaxStack > 1
            ? inventory.CanAccept(item, Template.MaxStack)
            : inventory.HasSpace;
    }

    private bool AddItem(WorldState world, InventoryComponent inventory, ItemInstance item)
    {
        return Template is not null && Template.MaxStack > 1
            ? inventory.AddWithStacking(item, Template.MaxStack, world.AllocateItemInstanceId)
            : inventory.Add(item);
    }

    private void TryAutoEquip(WorldState world, IEntity actor, InventoryComponent inventory, ItemInstance item, ActionOutcome outcome)
    {
        if (!AutoEquipUpgrades
            || Template is null
            || Template.Slot == EquipSlot.None
            || Template.MaxStack > 1
            || !inventory.Contains(item.InstanceId)
            || !RequirementValidator.MeetsRequirements(actor, Template)
            || !EquipmentUpgradeScorer.IsStrictUpgrade(Template, inventory.GetEquipped(Template.Slot)))
        {
            return;
        }

        var equipOutcome = new ToggleEquipAction(actor.Id, item.InstanceId, Template).Execute(world);
        if (equipOutcome.Result == ActionResult.Success && inventory.GetEquipped(Template.Slot)?.Item.InstanceId == item.InstanceId)
        {
            outcome.LogMessages.Add($"{actor.Name} auto-equips {Template.DisplayName}.");
        }
    }
}
