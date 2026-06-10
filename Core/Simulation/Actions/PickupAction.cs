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

        if (!CanAccept(mutableWorld, inventory, firstGroundItem))
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

        var template = ResolveTemplate(world, item);
        if (!AddItem(world, inventory, item, template))
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

        TryAutoEquip(world, actor, inventory, item, template, outcome);
        return outcome;
    }

    public int GetEnergyCost() => 500;

    private bool CanAccept(WorldState world, InventoryComponent inventory, ItemInstance item)
    {
        var template = ResolveTemplate(world, item);
        return template is not null && template.MaxStack > 1
            ? inventory.CanAccept(item, template.MaxStack)
            : inventory.HasSpace;
    }

    private bool AddItem(WorldState world, InventoryComponent inventory, ItemInstance item, ItemTemplate? template)
    {
        return template is not null && template.MaxStack > 1
            ? inventory.AddWithStacking(item, template.MaxStack, world.AllocateItemInstanceId)
            : inventory.Add(item);
    }

    private ItemTemplate? ResolveTemplate(WorldState world, ItemInstance item)
    {
        if (Template is not null && Template.TemplateId == item.TemplateId)
        {
            return Template;
        }

        return world.ContentDatabase is not null && world.ContentDatabase.TryGetItemTemplate(item.TemplateId, out var template)
            ? template
            : null;
    }

    private void TryAutoEquip(WorldState world, IEntity actor, InventoryComponent inventory, ItemInstance item, ItemTemplate? template, ActionOutcome outcome)
    {
        if (!AutoEquipUpgrades
            || template is null
            || template.Slot == EquipSlot.None
            || template.MaxStack > 1
            || !inventory.Contains(item.InstanceId)
            || !RequirementValidator.MeetsRequirements(actor, template)
            || !EquipmentUpgradeScorer.IsStrictUpgrade(template, inventory.GetEquipped(template.Slot)))
        {
            return;
        }

        var equipOutcome = new ToggleEquipAction(actor.Id, item.InstanceId, template).Execute(world);
        if (equipOutcome.Result == ActionResult.Success && inventory.GetEquipped(template.Slot)?.Item.InstanceId == item.InstanceId)
        {
            outcome.LogMessages.Add($"{actor.Name} auto-equips {template.DisplayName}.");
        }
    }
}
