using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class ToggleEquipAction : IAction
{
    public ToggleEquipAction(EntityId actorId, EntityId itemInstanceId, ItemTemplate template)
    {
        ActorId = actorId;
        ItemInstanceId = itemInstanceId;
        Template = template;
    }

    public EntityId ActorId { get; }

    public EntityId ItemInstanceId { get; }

    public ItemTemplate Template { get; }

    public ActionType Type => ActionType.ToggleEquip;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var inventory = actor?.GetComponent<InventoryComponent>();
        if (actor is null || inventory is null || !inventory.Contains(ItemInstanceId))
        {
            return ActionResult.Invalid;
        }

        return Template.Slot == EquipSlot.None ? ActionResult.Invalid : ActionResult.Success;
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
        var item = inventory.Get(ItemInstanceId);
        if (item is null)
        {
            return ActionOutcome.Fail(ActionResult.Invalid);
        }

        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position },
        };

        if (inventory.GetEquippedSlot(item.InstanceId) == Template.Slot)
        {
            if (inventory.TryUnequip(Template.Slot, out var removed) && removed is not null)
            {
                ApplyStatModifiers(actor.Stats, removed.StatModifiers, -1);
                outcome.LogMessages.Add($"{actor.Name} unequips {Template.DisplayName}.");
            }

            return outcome;
        }

        if (inventory.TryEquip(item, Template.Slot, Template.StatModifiers, out var previous))
        {
            if (previous is not null)
            {
                ApplyStatModifiers(actor.Stats, previous.StatModifiers, -1);
            }

            ApplyStatModifiers(actor.Stats, Template.StatModifiers, 1);
            outcome.LogMessages.Add($"{actor.Name} equips {Template.DisplayName}.");
        }

        return outcome;
    }

    public int GetEnergyCost() => 500;

    private static void ApplyStatModifiers(Stats stats, IReadOnlyDictionary<string, int> modifiers, int direction)
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
                    stats.HP = Math.Min(stats.HP, stats.MaxHP);
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