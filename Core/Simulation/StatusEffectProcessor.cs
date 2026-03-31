using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class StatusTickResult
{
    public int DamageTaken { get; init; }

    public int HealingDone { get; init; }

    public bool Died { get; init; }

    public IReadOnlyList<StatusEffectType> ExpiredEffects { get; init; } = Array.Empty<StatusEffectType>();
}

public static class StatusEffectProcessor
{
    public static IReadOnlyList<StatusEffectInstance> GetEffects(IEntity entity) =>
        entity.GetComponent<StatusEffectsComponent>()?.Effects ?? Array.Empty<StatusEffectInstance>();

    public static bool HasEffect(IEntity entity, StatusEffectType type) => GetEffect(entity, type) is not null;

    public static StatusEffectInstance? GetEffect(IEntity entity, StatusEffectType type)
    {
        var component = entity.GetComponent<StatusEffectsComponent>();
        if (component is null)
        {
            return null;
        }

        var index = component.FindIndex(type);
        return index >= 0 ? component[index] : null;
    }

    public static int GetMagnitude(IEntity entity, StatusEffectType type) => GetEffect(entity, type)?.Magnitude ?? 0;

    public static int GetEffectiveSpeed(IEntity entity)
    {
        if (HasEffect(entity, StatusEffectType.Frozen))
        {
            return 50;
        }

        if (HasEffect(entity, StatusEffectType.Hasted))
        {
            return 150;
        }

        return entity.Stats.Speed;
    }

    public static bool ApplyEffect(IEntity entity, StatusEffectType type, int duration, int magnitude = 1)
    {
        if (type == StatusEffectType.None || duration <= 0 || magnitude <= 0)
        {
            return false;
        }

        var component = entity.GetComponent<StatusEffectsComponent>();
        if (component is null)
        {
            component = new StatusEffectsComponent();
            entity.SetComponent(component);
        }

        var existingIndex = component.FindIndex(type);
        var stackable = IsStackable(type);
        var maxStacks = GetMaxStacks(type);

        if (existingIndex >= 0)
        {
            var current = component[existingIndex];
            var nextMagnitude = stackable ? Math.Min(maxStacks, current.Magnitude + magnitude) : Math.Max(current.Magnitude, magnitude);
            var refreshed = new StatusEffectInstance(type, Math.Max(current.RemainingTurns, duration), nextMagnitude);
            component.Replace(existingIndex, refreshed);
            return true;
        }

        component.Add(new StatusEffectInstance(type, duration, Math.Min(maxStacks, magnitude)));
        return true;
    }

    public static bool RemoveEffect(IEntity entity, StatusEffectType type)
    {
        var component = entity.GetComponent<StatusEffectsComponent>();
        return component is not null && component.Remove(type);
    }

    public static StatusTickResult Tick(WorldState world, EntityId entityId)
    {
        var entity = world.GetEntity(entityId);
        if (entity is null)
        {
            return new StatusTickResult();
        }

        var component = entity.GetComponent<StatusEffectsComponent>();
        if (component is null || component.Count == 0)
        {
            return new StatusTickResult();
        }

        var damageTaken = 0;
        var healingDone = 0;
        var expired = new List<StatusEffectType>();

        for (var index = component.Count - 1; index >= 0; index--)
        {
            var effect = component[index];
            switch (effect.Type)
            {
                case StatusEffectType.Poisoned:
                    damageTaken += 2 * effect.Magnitude;
                    entity.Stats.HP -= 2 * effect.Magnitude;
                    break;
                case StatusEffectType.Burning:
                    damageTaken += 3 * effect.Magnitude;
                    entity.Stats.HP -= 3 * effect.Magnitude;
                    break;
                case StatusEffectType.Regenerating:
                    var healed = Math.Min(2 * effect.Magnitude, Math.Max(0, entity.Stats.MaxHP - entity.Stats.HP));
                    healingDone += healed;
                    entity.Stats.HP += healed;
                    break;
            }

            var remaining = effect.RemainingTurns - 1;
            if (remaining <= 0)
            {
                expired.Add(effect.Type);
                component.RemoveAt(index);
            }
            else
            {
                component.Replace(index, effect with { RemainingTurns = remaining });
            }
        }

        entity.Stats.HP = Math.Min(entity.Stats.HP, entity.Stats.MaxHP);
        var died = entity.Stats.HP <= 0;
        if (died)
        {
            world.RemoveEntity(entity.Id);
        }

        return new StatusTickResult
        {
            DamageTaken = damageTaken,
            HealingDone = healingDone,
            Died = died,
            ExpiredEffects = expired,
        };
    }

    public static bool TryParseStatusEffect(string value, out StatusEffectType type)
    {
        switch (Normalize(value))
        {
            case "poison":
            case "poisoned":
                type = StatusEffectType.Poisoned;
                return true;
            case "burn":
            case "burning":
                type = StatusEffectType.Burning;
                return true;
            case "freeze":
            case "frozen":
                type = StatusEffectType.Frozen;
                return true;
            case "stun":
            case "stunned":
                type = StatusEffectType.Stunned;
                return true;
            case "haste":
            case "hasted":
                type = StatusEffectType.Hasted;
                return true;
            case "invisible":
            case "invisibility":
                type = StatusEffectType.Invisible;
                return true;
            case "regenerating":
            case "regeneration":
            case "regen":
                type = StatusEffectType.Regenerating;
                return true;
            case "weakened":
            case "weaken":
                type = StatusEffectType.Weakened;
                return true;
            case "shield":
            case "shielded":
                type = StatusEffectType.Shielded;
                return true;
            case "empower":
            case "empowered":
                type = StatusEffectType.Empowered;
                return true;
            case "corrode":
            case "corroded":
                type = StatusEffectType.Corroded;
                return true;
            case "phase":
            case "phased":
                type = StatusEffectType.Phased;
                return true;
            default:
                type = StatusEffectType.None;
                return false;
        }
    }

    private static bool IsStackable(StatusEffectType type) =>
        type is StatusEffectType.Poisoned or StatusEffectType.Regenerating or StatusEffectType.Shielded;

    private static int GetMaxStacks(StatusEffectType type) => type switch
    {
        StatusEffectType.Poisoned => 5,
        StatusEffectType.Regenerating => 3,
        StatusEffectType.Shielded => 3,
        _ => 1,
    };

    private static string Normalize(string value) => value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
}