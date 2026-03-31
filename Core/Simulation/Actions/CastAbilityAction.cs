using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class CastAbilityAction : IAction
{
    public CastAbilityAction(EntityId actorId, AbilityTemplate ability, Position targetPosition, IReadOnlyList<EntityId>? specificTargets = null)
    {
        ActorId = actorId;
        Ability = ability;
        TargetPosition = targetPosition;
        SpecificTargets = specificTargets;
    }

    public EntityId ActorId { get; }

    public AbilityTemplate Ability { get; }

    public Position TargetPosition { get; }

    public IReadOnlyList<EntityId>? SpecificTargets { get; }

    public ActionType Type => ActionType.CastAbility;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor is null || !actor.IsAlive)
        {
            return ActionResult.Invalid;
        }

        if (Ability.Targeting.Type != "self")
        {
            var range = actor.Position.ChebyshevTo(TargetPosition);
            if (range > Ability.Targeting.Range)
            {
                return ActionResult.Blocked;
            }

            if (Ability.Targeting.RequiresLos && !AbilityResolver.HasLineOfSight(actor.Position, TargetPosition, world))
            {
                return ActionResult.Blocked;
            }

            if (Ability.Targeting.RequiresWalkable && !world.IsWalkable(TargetPosition))
            {
                return ActionResult.Blocked;
            }
        }

        if (Ability.Targeting.Type == "single")
        {
            var target = world.GetEntityAt(TargetPosition);
            if (target is null || !target.IsAlive)
            {
                return ActionResult.Invalid;
            }
        }

        var cooldowns = actor.GetComponent<CooldownComponent>();
        if (cooldowns is not null && cooldowns.IsOnCooldown(Ability.AbilityId))
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
        world.CombatResolver ??= new CombatResolver(world.Seed);
        var rng = new Random(world.Seed + world.TurnNumber + ActorId.GetHashCode());

        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position, TargetPosition },
        };

        var targets = AbilityResolver.ResolveTargets(Ability, actor, TargetPosition, world);
        var totalDamageDealt = 0;
        var damageResults = new List<DamageResult>();
        var statusEffectsApplied = new List<StatusEffectInstance>();

        foreach (var effect in Ability.Effects)
        {
            switch (effect.Type)
            {
                case "damage":
                    var damageTargets = AbilityResolver.FilterByRelation(targets, actor, effect.Filter);
                    foreach (var target in damageTargets)
                    {
                        var rawDamage = AbilityResolver.CalculateAbilityDamage(effect, actor, rng);
                        var finalDamage = world.CombatResolver.ApplyArmor(rawDamage, target, effect.DamageType);
                        var isKill = target.Stats.HP - finalDamage <= 0;

                        target.Stats.HP -= finalDamage;
                        totalDamageDealt += finalDamage;
                        outcome.DirtyPositions.Add(target.Position);

                        var result = new DamageResult(ActorId, target.Id, rawDamage, finalDamage, effect.DamageType, false, false, isKill);
                        damageResults.Add(result);

                        if (isKill || target.Stats.HP <= 0)
                        {
                            target.Stats.HP = 0;
                            world.RemoveEntity(target.Id);
                            outcome.LogMessages.Add($"{actor.Name}'s {Ability.DisplayName} kills {target.Name} for {finalDamage} damage.");
                        }
                        else
                        {
                            outcome.LogMessages.Add($"{actor.Name}'s {Ability.DisplayName} hits {target.Name} for {finalDamage} damage.");
                        }
                    }
                    break;

                case "apply_status":
                    if (effect.StatusEffect is null)
                    {
                        break;
                    }

                    if (!StatusEffectProcessor.TryParseStatusEffect(effect.StatusEffect, out var statusType))
                    {
                        break;
                    }

                    var statusTargets = AbilityResolver.FilterByRelation(targets, actor, effect.Filter);
                    foreach (var target in statusTargets)
                    {
                        if (effect.StatusChance < 100 && rng.Next(100) >= effect.StatusChance)
                        {
                            continue;
                        }

                        StatusEffectProcessor.ApplyEffect(target, statusType, effect.StatusDuration);
                        var applied = StatusEffectProcessor.GetEffect(target, statusType);
                        if (applied is not null)
                        {
                            statusEffectsApplied.Add(applied);
                            outcome.LogMessages.Add($"{target.Name} is affected by {effect.StatusEffect}.");
                        }
                    }
                    break;

                case "teleport":
                    if (world.MoveEntity(ActorId, TargetPosition))
                    {
                        outcome.LogMessages.Add($"{actor.Name} teleports to ({TargetPosition.X}, {TargetPosition.Y}).");
                        outcome.DirtyPositions.Add(TargetPosition);
                    }
                    break;

                case "heal_self":
                    var healAmount = (int)(totalDamageDealt * effect.HealFactor);
                    if (healAmount > 0)
                    {
                        actor.Stats.HP = Math.Min(actor.Stats.MaxHP, actor.Stats.HP + healAmount);
                        outcome.LogMessages.Add($"{actor.Name} heals for {healAmount}.");
                    }
                    break;
            }
        }

        if (damageResults.Count > 0 || statusEffectsApplied.Count > 0)
        {
            outcome.CombatEvents.Add(new CombatEvent(
                world.TurnNumber,
                ActionType.CastAbility,
                damageResults.ToArray(),
                statusEffectsApplied.ToArray()));
        }

        outcome.LogMessages.Insert(0, $"{actor.Name} casts {Ability.DisplayName}.");

        var abilityCooldowns = actor.GetComponent<CooldownComponent>();
        var abilitiesComp = actor.GetComponent<AbilitiesComponent>();
        if (abilityCooldowns is not null && abilitiesComp is not null)
        {
            var slot = abilitiesComp.Slots.Find(s => s.AbilityId == Ability.AbilityId);
            if (slot is not null && slot.Cooldown > 0)
            {
                abilityCooldowns.SetCooldown(Ability.AbilityId, slot.Cooldown);
            }
        }

        return outcome;
    }

    public int GetEnergyCost() => Ability.EnergyCost;
}
