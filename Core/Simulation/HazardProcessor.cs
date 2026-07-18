using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class HazardProcessor
{
    public static event Action<EntityId, string>? TrapTriggered;

    public static bool OnEntityEnteredTile(WorldState world, IEntity actor, ActionOutcome outcome)
    {
        if (!actor.IsAlive)
        {
            return false;
        }

        var trap = FindArmedTrapAt(world, actor.Position);
        if (trap is null)
        {
            return false;
        }

        var trapComponent = trap.GetComponent<TrapComponent>()!;
        if (!trapComponent.IsArmed)
        {
            return false;
        }

        if (world.ContentDatabase is null || !world.ContentDatabase.TryGetTrapTemplate(trapComponent.TemplateId, out var template))
        {
            return false;
        }

        if (ActorAvoidsGroundTraps(world, actor, template))
        {
            return false;
        }

        world.CombatResolver ??= new CombatResolver(world.Seed);

        if (template.TriggerChance < 100 && world.CombatResolver.NextRandom(100) >= template.TriggerChance)
        {
            return false;
        }

        trapComponent.IsArmed = false;
        trapComponent.IsRevealed = true;
        trapComponent.TriggerCount++;

        outcome.LogMessages.Add($"{actor.Name} triggers {template.DisplayName}!");

        var damageResults = new List<DamageResult>();
        var statusEffectsApplied = new List<StatusEffectInstance>();

        var rawDamage = 0;
        if (template.DamageMax > 0)
        {
            rawDamage = template.DamageMin == template.DamageMax
                ? template.DamageMin
                : world.CombatResolver.NextRandom(template.DamageMin, template.DamageMax + 1);
        }

        var finalDamage = rawDamage > 0
            ? world.CombatResolver.ApplyArmor(rawDamage, actor, template.DamageType)
            : 0;

        if (finalDamage > 0)
        {
            finalDamage = RelicProcessor.ApplyIncomingDamage(world, actor, null, finalDamage, outcome.LogMessages);
            if (finalDamage > 0)
            {
                outcome.LogMessages.Add($"{actor.Name} takes {finalDamage} {template.DamageType.ToString().ToLowerInvariant()} damage from {template.DisplayName}.");
            }
        }

        var isKill = actor.Stats.HP <= 0;
        damageResults.Add(new DamageResult(trap.Id, actor.Id, rawDamage, finalDamage, template.DamageType, false, false, isKill));

        if (!isKill && !string.IsNullOrWhiteSpace(template.StatusEffect))
        {
            ApplyTrapStatus(world, actor, template, statusEffectsApplied, outcome.LogMessages);
        }

        if (statusEffectsApplied.Count > 0)
        {
            outcome.CombatEvents.Add(new CombatEvent(world.TurnNumber, ActionType.Move, damageResults, statusEffectsApplied));
        }
        else
        {
            outcome.CombatEvents.Add(new CombatEvent(world.TurnNumber, ActionType.Move, damageResults, Array.Empty<StatusEffectInstance>()));
        }

        if (isKill)
        {
            var death = DeathResolver.ResolveUnattributedDeath(world, actor);
            outcome.LogMessages.Add($"{actor.Name} dies to {template.DisplayName}.");
            DeathResolver.AppendLootLogMessages(outcome.LogMessages, death);
        }

        TrapTriggered?.Invoke(actor.Id, template.TemplateId);
        return true;
    }

    private static IEntity? FindArmedTrapAt(WorldState world, Position position)
    {
        foreach (var entity in world.Entities)
        {
            if (entity.Position == position && entity.GetComponent<TrapComponent>() is { IsArmed: true })
            {
                return entity;
            }
        }

        return null;
    }

    private static bool ActorAvoidsGroundTraps(WorldState world, IEntity actor, TrapTemplate template)
    {
        if (template.AvoidFlags is not null)
        {
            foreach (var flag in template.AvoidFlags)
            {
                if (StatusEffectProcessor.HasFlag(actor, flag))
                {
                    return true;
                }

                if (world.ContentDatabase is not null && StatusEffectProcessor.HasFlag(actor, flag, world.ContentDatabase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ApplyTrapStatus(
        WorldState world,
        IEntity actor,
        TrapTemplate template,
        List<StatusEffectInstance> statusEffectsApplied,
        List<string> logMessages)
    {
        if (!StatusEffectProcessor.TryParseStatusEffect(template.StatusEffect!, out var statusType) || statusType == StatusEffectType.None)
        {
            return;
        }

        var applied = world.ContentDatabase is not null
            ? StatusEffectProcessor.ApplyEffect(actor, statusType, world.ContentDatabase, template.StatusDuration, template.StatusMagnitude)
            : StatusEffectProcessor.ApplyEffect(actor, statusType, template.StatusDuration, template.StatusMagnitude);

        if (!applied)
        {
            return;
        }

        var instance = StatusEffectProcessor.GetEffect(actor, statusType);
        if (instance is not null)
        {
            statusEffectsApplied.Add(instance);
            logMessages.Add($"{actor.Name} is afflicted with {statusType} from {template.DisplayName}!");
        }
    }
}
