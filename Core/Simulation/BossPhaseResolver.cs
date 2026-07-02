using System;
using System.Linq;

namespace Roguelike.Core;

public static class BossPhaseResolver
{
    public static bool TryApplyTransitions(WorldState world, IEntity boss, ActionOutcome outcome, EntityId sourceEntityId)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(boss);
        ArgumentNullException.ThrowIfNull(outcome);

        if (boss.Stats.HP <= 0 || world.ContentDatabase is null)
        {
            return false;
        }

        var enemy = boss.GetComponent<EnemyComponent>();
        if (enemy is null || !world.ContentDatabase.TryGetEnemyTemplate(enemy.TemplateId, out var template) || template.BossPhases is not { Count: > 0 })
        {
            return false;
        }

        var component = boss.GetComponent<BossPhaseComponent>();
        if (component is null)
        {
            component = new BossPhaseComponent();
            boss.SetComponent(component);
        }

        var hpPercent = boss.Stats.MaxHP <= 0 ? 0.0 : (double)boss.Stats.HP / boss.Stats.MaxHP;
        var transitioned = false;
        foreach (var phase in template.BossPhases.OrderBy(phase => phase.Phase))
        {
            if (component.TriggeredPhases.Contains(phase.Phase) || hpPercent > phase.Threshold)
            {
                continue;
            }

            component.CurrentPhase = Math.Max(component.CurrentPhase, phase.Phase);
            component.TriggeredPhases.Add(phase.Phase);
            ApplyPhaseEffects(boss, phase, world, sourceEntityId);
            outcome.DirtyPositions.Add(boss.Position);
            outcome.LogMessages.Add(string.IsNullOrWhiteSpace(phase.Message)
                ? $"{boss.Name} enters phase {phase.Phase}!"
                : phase.Message.Replace("{name}", boss.Name, StringComparison.Ordinal));
            outcome.BossPhaseTransitions.Add((boss.Id, phase.Phase));
            transitioned = true;
        }

        return transitioned;
    }

    private static void ApplyPhaseEffects(IEntity boss, BossPhaseTemplate phase, WorldState world, EntityId sourceEntityId)
    {
        if (phase.StatBoost != 0)
        {
            boss.Stats.Attack += phase.StatBoost;
        }

        if (!string.IsNullOrWhiteSpace(phase.AbilityId))
        {
            var abilities = boss.GetComponent<AbilitiesComponent>();
            if (abilities is null)
            {
                abilities = new AbilitiesComponent();
                boss.SetComponent(abilities);
            }

            if (!abilities.Slots.Any(slot => string.Equals(slot.AbilityId, phase.AbilityId, StringComparison.Ordinal)))
            {
                abilities.Slots.Add(new EnemyAbilitySlot { AbilityId = phase.AbilityId, Cooldown = 0, Priority = 100 + phase.Phase });
            }
        }

        if (!string.IsNullOrWhiteSpace(phase.StatusEffect)
            && StatusEffectProcessor.TryParseStatusEffect(phase.StatusEffect, out var statusType))
        {
            if (world.ContentDatabase is not null)
            {
                StatusEffectProcessor.ApplyEffect(boss, statusType, world.ContentDatabase, 3, sourceEntityId: sourceEntityId);
            }
            else
            {
                StatusEffectProcessor.ApplyEffect(boss, statusType, 3, sourceEntityId: sourceEntityId);
            }
        }
    }
}
