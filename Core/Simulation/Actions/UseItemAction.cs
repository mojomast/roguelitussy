using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class UseItemAction : IAction
{
    public UseItemAction(EntityId actorId, EntityId itemInstanceId, ItemTemplate template, AbilityTemplate? castAbility = null, Position? abilityTarget = null)
    {
        ActorId = actorId;
        ItemInstanceId = itemInstanceId;
        Template = template;
        CastAbility = castAbility;
        AbilityTarget = abilityTarget ?? default;
        HasAbilityTarget = abilityTarget.HasValue;
    }

    public EntityId ActorId { get; }

    public EntityId ItemInstanceId { get; }

    public ItemTemplate Template { get; }

    public AbilityTemplate? CastAbility { get; }

    public Position AbilityTarget { get; }

    public bool HasAbilityTarget { get; }

    public ActionType Type => ActionType.UseItem;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var inventory = actor?.GetComponent<InventoryComponent>();
        if (actor is null || inventory is null || !inventory.Contains(ItemInstanceId))
        {
            return ActionResult.Invalid;
        }

        if (Template.Slot != EquipSlot.None)
        {
            return ActionResult.Invalid;
        }

        if (TryCreateCastAbilityAction(world, actor, out var castAction, out var castValidationFailure))
        {
            return castAction.Validate(world);
        }

        return castValidationFailure ?? ActionResult.Success;
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

        var effectResult = ApplyEffect(actor, outcome, world);
        if (effectResult != ActionResult.Success)
        {
            return ActionOutcome.Fail(effectResult);
        }

        ConsumeIfNeeded(inventory, item);
        outcome.LogMessages.Add($"{actor.Name} uses {Template.DisplayName}.");
        return outcome;
    }

    public int GetEnergyCost() => 500;

    private ActionResult ApplyEffect(IEntity actor, ActionOutcome outcome, WorldState world)
    {
        if (TryCreateCastAbilityAction(world, actor, out var castAction, out var castValidationFailure))
        {
            var castOutcome = castAction.Execute(world);
            if (castOutcome.Result != ActionResult.Success)
            {
                return castOutcome.Result;
            }

            outcome.CombatEvents.AddRange(castOutcome.CombatEvents);
            outcome.LogMessages.AddRange(castOutcome.LogMessages);
            outcome.DirtyPositions.AddRange(castOutcome.DirtyPositions);
            return ActionResult.Success;
        }

        if (castValidationFailure is ActionResult failure)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(Template.UseEffect))
        {
            ApplyStatModifiers(actor.Stats, Template.StatModifiers, 1);
            return ActionResult.Success;
        }

        if (string.Equals(Template.UseEffect, "heal", StringComparison.OrdinalIgnoreCase))
        {
            var healAmount = ResolveModifier(Template.StatModifiers, "heal", 5);
            actor.Stats.HP = Math.Min(actor.Stats.MaxHP, actor.Stats.HP + healAmount);
            return ActionResult.Success;
        }

        if (TryParseApplyStatus(Template.UseEffect, out var effectType))
        {
            var duration = ResolveModifier(Template.StatModifiers, "duration", 3);
            var magnitude = ResolveModifier(Template.StatModifiers, "magnitude", 1);
            StatusEffectProcessor.ApplyEffect(actor, effectType, duration, magnitude);
            var applied = StatusEffectProcessor.GetEffect(actor, effectType);
            if (applied is not null)
            {
                outcome.CombatEvents.Add(new CombatEvent(0, Type, Array.Empty<DamageResult>(), new[] { applied }, ActorId));
            }

            return ActionResult.Success;
        }

        if (TryParseCureStatus(Template.UseEffect, out effectType))
        {
            StatusEffectProcessor.RemoveEffect(actor, effectType);
        }

        return ActionResult.Success;
    }

    private bool TryCreateCastAbilityAction(IWorldState world, IEntity actor, out CastAbilityAction castAction, out ActionResult? validationFailure)
    {
        castAction = null!;
        validationFailure = null;

        var ability = CastAbility;
        if (ability is null && TryParseCastAbility(Template.UseEffect, out var abilityId))
        {
            if (string.IsNullOrWhiteSpace(abilityId) || world is not WorldState { ContentDatabase: not null } mutableWorld || !mutableWorld.ContentDatabase.TryGetAbilityTemplate(abilityId, out ability))
            {
                validationFailure = ActionResult.Invalid;
                return false;
            }
        }

        if (ability is null)
        {
            return false;
        }

        var target = AbilityTarget;
        if (string.Equals(ability.Targeting.Type, "self", StringComparison.OrdinalIgnoreCase))
        {
            target = actor.Position;
        }
        else if (!HasAbilityTarget)
        {
            validationFailure = ActionResult.Invalid;
            return false;
        }

        castAction = new CastAbilityAction(ActorId, ability, target);
        return true;
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

        inventory.RemoveQuantity(item.InstanceId, 1, out _);
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
        const string applyStatusPrefix = "apply_status:";

        if (useEffect.StartsWith(applyStatusPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return StatusEffectProcessor.TryParseStatusEffect(useEffect[applyStatusPrefix.Length..], out effectType);
        }

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

    private static bool TryParseCastAbility(string? useEffect, out string? abilityId)
    {
        const string castPrefix = "cast_ability:";
        abilityId = null;

        if (string.IsNullOrWhiteSpace(useEffect))
        {
            return false;
        }

        if (useEffect.StartsWith(castPrefix, StringComparison.OrdinalIgnoreCase))
        {
            abilityId = useEffect[castPrefix.Length..];
            return true;
        }

        if (string.Equals(useEffect, "cast_ability", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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
