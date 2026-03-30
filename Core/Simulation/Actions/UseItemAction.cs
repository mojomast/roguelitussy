using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class UseItemAction : IAction
{
    public UseItemAction(EntityId actorId, EntityId itemInstanceId, ItemTemplate template)
    {
        ActorId = actorId;
        ItemInstanceId = itemInstanceId;
        Template = template;
    }

    public EntityId ActorId { get; }

    public EntityId ItemInstanceId { get; }

    public ItemTemplate Template { get; }

    public ActionType Type => ActionType.UseItem;

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

        if (Template.Slot != EquipSlot.None)
        {
            ToggleEquipment(actor, inventory, item, outcome);
            return outcome;
        }

        ApplyEffect(actor, outcome);
        ConsumeIfNeeded(inventory, item);
        outcome.LogMessages.Add($"{actor.Name} uses {Template.DisplayName}." );
        return outcome;
    }

    public int GetEnergyCost() => 500;

    private void ToggleEquipment(IEntity actor, InventoryComponent inventory, ItemInstance item, ActionOutcome outcome)
    {
        if (inventory.GetEquippedSlot(item.InstanceId) == Template.Slot)
        {
            if (inventory.TryUnequip(Template.Slot, out var removed) && removed is not null)
            {
                ApplyStatModifiers(actor.Stats, removed.StatModifiers, -1);
                outcome.LogMessages.Add($"{actor.Name} unequips {Template.DisplayName}." );
            }

            return;
        }

        if (inventory.TryEquip(item, Template.Slot, Template.StatModifiers, out var previous))
        {
            if (previous is not null)
            {
                ApplyStatModifiers(actor.Stats, previous.StatModifiers, -1);
            }

            ApplyStatModifiers(actor.Stats, Template.StatModifiers, 1);
            outcome.LogMessages.Add($"{actor.Name} equips {Template.DisplayName}." );
        }
    }

    private void ApplyEffect(IEntity actor, ActionOutcome outcome)
    {
        if (string.IsNullOrWhiteSpace(Template.UseEffect))
        {
            ApplyStatModifiers(actor.Stats, Template.StatModifiers, 1);
            return;
        }

        if (string.Equals(Template.UseEffect, "heal", StringComparison.OrdinalIgnoreCase))
        {
            var healAmount = ResolveModifier(Template.StatModifiers, "heal", 5);
            actor.Stats.HP = Math.Min(actor.Stats.MaxHP, actor.Stats.HP + healAmount);
            return;
        }

        if (TryParseApplyStatus(Template.UseEffect, out var effectType))
        {
            var duration = ResolveModifier(Template.StatModifiers, "duration", 3);
            var magnitude = ResolveModifier(Template.StatModifiers, "magnitude", 1);
            StatusEffectProcessor.ApplyEffect(actor, effectType, duration, magnitude);
            var applied = StatusEffectProcessor.GetEffect(actor, effectType);
            if (applied is not null)
            {
                outcome.CombatEvents.Add(new CombatEvent(0, Type, Array.Empty<DamageResult>(), new[] { applied }));
            }

            return;
        }

        if (TryParseCureStatus(Template.UseEffect, out effectType))
        {
            StatusEffectProcessor.RemoveEffect(actor, effectType);
        }
    }

    private void ConsumeIfNeeded(InventoryComponent inventory, ItemInstance item)
    {
        if (Template.Category is not ItemCategory.Consumable and not ItemCategory.Scroll)
        {
            return;
        }

        if (Template.MaxCharges > 1)
        {
            item.CurrentCharges = item.CurrentCharges <= 0 ? Template.MaxCharges : item.CurrentCharges;
            item.CurrentCharges--;
            if (item.CurrentCharges > 0)
            {
                return;
            }
        }

        inventory.Remove(item.InstanceId, out _);
    }

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

    private static int ResolveModifier(IReadOnlyDictionary<string, int> modifiers, string key, int fallback) =>
        modifiers.TryGetValue(key, out var value) ? value : fallback;

    private static bool TryParseApplyStatus(string useEffect, out StatusEffectType effectType)
    {
        const string statusPrefix = "status:";
        const string applyPrefix = "apply:";

        if (useEffect.StartsWith(statusPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return StatusEffectProcessor.TryParseStatusEffect(useEffect[statusPrefix.Length..], out effectType);
        }

        if (useEffect.StartsWith(applyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return StatusEffectProcessor.TryParseStatusEffect(useEffect[applyPrefix.Length..], out effectType);
        }

        effectType = StatusEffectType.None;
        return false;
    }

    private static bool TryParseCureStatus(string useEffect, out StatusEffectType effectType)
    {
        const string curePrefix = "cure:";
        if (useEffect.StartsWith(curePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return StatusEffectProcessor.TryParseStatusEffect(useEffect[curePrefix.Length..], out effectType);
        }

        effectType = StatusEffectType.None;
        return false;
    }
}