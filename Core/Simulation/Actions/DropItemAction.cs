namespace Roguelike.Core;

public sealed class DropItemAction : IAction
{
    public DropItemAction(EntityId actorId, EntityId itemInstanceId, int quantity = int.MaxValue)
    {
        ActorId = actorId;
        ItemInstanceId = itemInstanceId;
        Quantity = quantity;
    }

    public EntityId ActorId { get; }

    public EntityId ItemInstanceId { get; }

    public int Quantity { get; }

    public ActionType Type => ActionType.DropItem;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var inventory = actor?.GetComponent<InventoryComponent>();
        return actor is null || inventory is null || !inventory.Contains(ItemInstanceId) ? ActionResult.Invalid : ActionResult.Success;
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
        var slot = inventory.GetEquippedSlot(ItemInstanceId);
        if (slot != EquipSlot.None && inventory.TryUnequip(slot, out var equipped) && equipped is not null)
        {
            ApplyStatModifiers(actor.Stats, equipped.StatModifiers, -1);
        }

        if (!inventory.RemoveQuantity(ItemInstanceId, Quantity, out var item) || item is null)
        {
            return ActionOutcome.Fail(ActionResult.Invalid);
        }

        world.DropItem(actor.Position, item);
        var droppedCount = item.StackCount > 1 ? $" x{item.StackCount}" : string.Empty;
        return new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position },
            LogMessages = { $"{actor.Name} drops {item.TemplateId}{droppedCount}." },
        };
    }

    public int GetEnergyCost() => 500;

    private static void ApplyStatModifiers(Stats stats, System.Collections.Generic.IReadOnlyDictionary<string, int> modifiers, int direction)
    {
        foreach (var modifier in modifiers)
        {
            var value = modifier.Value * direction;
            switch (modifier.Key.ToLowerInvariant())
            {
                case "hp":
                    stats.HP += value;
                    break;
                case "max_hp":
                case "maxhp":
                    stats.MaxHP += value;
                    stats.HP = System.Math.Min(stats.HP, stats.MaxHP);
                    break;
                case "attack":
                    stats.Attack += value;
                    break;
                case "accuracy":
                    stats.Accuracy += value;
                    break;
                case "defense":
                    stats.Defense += value;
                    break;
                case "evasion":
                    stats.Evasion += value;
                    break;
                case "speed":
                    stats.Speed += value;
                    break;
                case "view_radius":
                case "viewradius":
                    stats.ViewRadius += value;
                    break;
            }
        }
    }
}