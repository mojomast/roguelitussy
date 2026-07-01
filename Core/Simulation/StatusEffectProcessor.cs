using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class StatusTickResult
{
    public int DamageTaken { get; init; }

    public int HealingDone { get; init; }

    public bool Died { get; init; }

    public DeathResolver.DeathResolution? Death { get; init; }

    public IReadOnlyList<StatusEffectType> ExpiredEffects { get; init; } = Array.Empty<StatusEffectType>();

    public IReadOnlyList<string> LogMessages { get; init; } = Array.Empty<string>();
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

    public static int GetEffectiveSpeed(IEntity entity, IContentDatabase db)
    {
        var speed = (double)entity.Stats.Speed;
        var hasModifier = false;

        foreach (var effect in GetEffects(entity))
        {
            if (!TryGetDefinition(effect.Type, db, out var definition))
            {
                continue;
            }

            foreach (var modifier in definition!.StatModifiers)
            {
                if (!string.Equals(modifier.Stat, "speed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                hasModifier = true;
                speed = ApplyStatModifier(speed, modifier);
            }
        }

        return hasModifier ? (int)Math.Round(speed) : entity.Stats.Speed;
    }

    public static bool ApplyEffect(IEntity entity, StatusEffectType type, int duration, int magnitude = 1, EntityId? sourceEntityId = null)
    {
        if (type == StatusEffectType.None || duration <= 0 || magnitude <= 0)
        {
            return false;
        }

        ApplyLifecycleEffects(entity, type);

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
            var refreshed = new StatusEffectInstance(type, Math.Max(current.RemainingTurns, duration), nextMagnitude, sourceEntityId ?? current.SourceEntityId);
            component.Replace(existingIndex, refreshed);
            return true;
        }

        component.Add(new StatusEffectInstance(type, duration, Math.Min(maxStacks, magnitude), sourceEntityId));
        return true;
    }

    public static bool ApplyEffect(IEntity entity, StatusEffectType type, IContentDatabase db, int duration, int magnitude = 1, EntityId? sourceEntityId = null)
    {
        if (type == StatusEffectType.None || duration <= 0 || magnitude <= 0)
        {
            return false;
        }

        if (!TryGetDefinition(type, db, out var definition))
        {
            return ApplyEffect(entity, type, duration, magnitude, sourceEntityId);
        }

        ApplyLifecycleEffects(entity, type, definition!);

        var component = entity.GetComponent<StatusEffectsComponent>();
        if (component is null)
        {
            component = new StatusEffectsComponent();
            entity.SetComponent(component);
        }

        var existingIndex = component.FindIndex(type);
        var stackable = definition.Stackable;
        var maxStacks = definition.MaxStacks ?? 1;

        if (existingIndex >= 0)
        {
            var current = component[existingIndex];
            var nextMagnitude = stackable ? Math.Min(maxStacks, current.Magnitude + magnitude) : Math.Max(current.Magnitude, magnitude);
            var nextDuration = definition.Refreshable ? Math.Max(current.RemainingTurns, duration) : current.RemainingTurns;
            var refreshed = new StatusEffectInstance(type, nextDuration, nextMagnitude, sourceEntityId ?? current.SourceEntityId);
            component.Replace(existingIndex, refreshed);
            return true;
        }

        component.Add(new StatusEffectInstance(type, duration, Math.Min(maxStacks, magnitude), sourceEntityId));
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
        EntityId? lethalSourceId = null;

        for (var index = component.Count - 1; index >= 0; index--)
        {
            var effect = component[index];
            switch (effect.Type)
            {
                case StatusEffectType.Poisoned:
                    var poisonDamage = 2 * effect.Magnitude;
                    if (effect.SourceEntityId is { } poisonSourceId && world.GetEntity(poisonSourceId) is { } poisonSource)
                    {
                        var context = new RelicHookContext
                        {
                            TargetId = entity.Id,
                            DamageAmount = poisonDamage,
                            ModifiedValue = poisonDamage,
                        };
                        RelicProcessor.ProcessHook("on_poison_tick", poisonSource, world, world.ContentDatabase, context);
                        poisonDamage = Math.Max(0, context.ModifiedValue);
                    }

                    damageTaken += poisonDamage;
                    entity.Stats.HP -= poisonDamage;
                    lethalSourceId = entity.Stats.HP <= 0 ? effect.SourceEntityId : lethalSourceId;
                    break;
                case StatusEffectType.Burning:
                    damageTaken += 3 * effect.Magnitude;
                    entity.Stats.HP -= 3 * effect.Magnitude;
                    lethalSourceId = entity.Stats.HP <= 0 ? effect.SourceEntityId : lethalSourceId;
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
        DeathResolver.DeathResolution? death = null;
        var logMessages = new List<string>();
        if (died)
        {
            var killer = lethalSourceId is { } sourceId ? world.GetEntity(sourceId) : null;
            death = killer is null
                ? DeathResolver.ResolveUnattributedDeath(world, entity)
                : DeathResolver.ResolveKill(world, killer, entity);

            var killerName = killer?.Name ?? "Something";
            DeathResolver.AppendDeathLogMessages(logMessages, killerName, entity.Name, death);

            // TODO(ITM-1): TurnScheduler and GameLoop currently discard these log messages
            // because ITurnScheduler.ConsumeEnergy returns void. Add a log sink to the scheduler
            // interface or flush StatusTickResult.LogMessages into the round outcome.
        }

        return new StatusTickResult
        {
            DamageTaken = damageTaken,
            HealingDone = healingDone,
            Died = died,
            Death = death,
            ExpiredEffects = expired,
            LogMessages = logMessages,
        };
    }

    public static StatusTickResult Tick(WorldState world, EntityId entityId, IContentDatabase db)
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
        EntityId? lethalSourceId = null;

        for (var index = component.Count - 1; index >= 0; index--)
        {
            var effect = component[index];
            if (TryGetDefinition(effect.Type, db, out var definition))
            {
                foreach (var tickEffect in definition!.TickEffects)
                {
                    switch (tickEffect.Type)
                    {
                        case "damage":
                            var damage = tickEffect.Value * effect.Magnitude;
                            if (effect.Type == StatusEffectType.Poisoned
                                && effect.SourceEntityId is { } poisonSourceId
                                && world.GetEntity(poisonSourceId) is { } poisonSource)
                            {
                                var context = new RelicHookContext
                                {
                                    TargetId = entity.Id,
                                    DamageAmount = damage,
                                    ModifiedValue = damage,
                                };
                                RelicProcessor.ProcessHook("on_poison_tick", poisonSource, world, db, context);
                                damage = Math.Max(0, context.ModifiedValue);
                            }

                            damageTaken += damage;
                            entity.Stats.HP -= damage;
                            lethalSourceId = entity.Stats.HP <= 0 ? effect.SourceEntityId : lethalSourceId;
                            break;
                        case "heal":
                            var heal = tickEffect.Value * effect.Magnitude;
                            var healed = Math.Min(heal, Math.Max(0, entity.Stats.MaxHP - entity.Stats.HP));
                            healingDone += healed;
                            entity.Stats.HP += healed;
                            break;
                    }
                }
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
        DeathResolver.DeathResolution? death = null;
        var logMessages = new List<string>();
        if (died)
        {
            var killer = lethalSourceId is { } sourceId ? world.GetEntity(sourceId) : null;
            death = killer is null
                ? DeathResolver.ResolveUnattributedDeath(world, entity)
                : DeathResolver.ResolveKill(world, killer, entity);

            var killerName = killer?.Name ?? "Something";
            DeathResolver.AppendDeathLogMessages(logMessages, killerName, entity.Name, death);

            // TODO(ITM-1): TurnScheduler and GameLoop currently discard these log messages
            // because ITurnScheduler.ConsumeEnergy returns void. Add a log sink to the scheduler
            // interface or flush StatusTickResult.LogMessages into the round outcome.
        }

        return new StatusTickResult
        {
            DamageTaken = damageTaken,
            HealingDone = healingDone,
            Died = died,
            Death = death,
            ExpiredEffects = expired,
            LogMessages = logMessages,
        };
    }

    public static bool HasFlag(IEntity entity, string flag)
    {
        return flag switch
        {
            "skip_turn" => HasEffect(entity, StatusEffectType.Stunned) || HasEffect(entity, StatusEffectType.Frozen),
            "phase_through_walls" => HasEffect(entity, StatusEffectType.Phased),
            "immune_physical" => HasEffect(entity, StatusEffectType.Phased),
            "flying" => HasEffect(entity, StatusEffectType.Flying),
            _ => false,
        };
    }

    public static bool HasFlag(IEntity entity, string flag, IContentDatabase db)
    {
        foreach (var effect in GetEffects(entity))
        {
            if (!TryGetDefinition(effect.Type, db, out var definition))
            {
                continue;
            }

            foreach (var authoredFlag in definition!.Flags)
            {
                if (string.Equals(authoredFlag, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
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
            case "flying":
            case "fly":
                type = StatusEffectType.Flying;
                return true;
            default:
                type = StatusEffectType.None;
                return false;
        }
    }

    private static bool IsStackable(StatusEffectType type) =>
        type is StatusEffectType.Poisoned or StatusEffectType.Regenerating or StatusEffectType.Shielded or StatusEffectType.Corroded;

    private static int GetMaxStacks(StatusEffectType type) => type switch
    {
        StatusEffectType.Poisoned => 5,
        StatusEffectType.Regenerating => 3,
        StatusEffectType.Shielded => 3,
        StatusEffectType.Corroded => 3,
        _ => 1,
    };

    private static void ApplyLifecycleEffects(IEntity entity, StatusEffectType type)
    {
        if (type == StatusEffectType.Burning)
        {
            RemoveEffect(entity, StatusEffectType.Frozen);
        }
        else if (type == StatusEffectType.Frozen)
        {
            RemoveEffect(entity, StatusEffectType.Burning);
        }
    }

    private static void ApplyLifecycleEffects(IEntity entity, StatusEffectType type, StatusEffectDefinition definition)
    {
        foreach (var effect in definition.OnApplyEffects)
        {
            if (string.Equals(effect.Type, "remove_status", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(effect.StatusId)
                && TryParseStatusEffect(effect.StatusId, out var statusType))
            {
                RemoveEffect(entity, statusType);
            }
        }
    }

    private static double ApplyStatModifier(double value, StatusStatModifierDefinition modifier)
    {
        var operation = modifier.Operation;
        if (string.Equals(operation, "add", StringComparison.OrdinalIgnoreCase))
        {
            return value + modifier.Value;
        }

        if (string.Equals(operation, "multiply", StringComparison.OrdinalIgnoreCase))
        {
            return value * modifier.Value;
        }

        if (string.Equals(operation, "set", StringComparison.OrdinalIgnoreCase))
        {
            return modifier.Value;
        }

        return value;
    }

    private static bool TryGetDefinition(StatusEffectType type, IContentDatabase? db, out StatusEffectDefinition definition)
    {
        var id = GetStatusEffectId(type);
        if (id is not null && db is not null && db.TryGetStatusEffect(id, out var fromDb))
        {
            definition = fromDb;
            return true;
        }

        definition = null!;
        return false;
    }

    private static string? GetStatusEffectId(StatusEffectType type) => type switch
    {
        StatusEffectType.Poisoned => "poisoned",
        StatusEffectType.Burning => "burning",
        StatusEffectType.Frozen => "frozen",
        StatusEffectType.Stunned => "stunned",
        StatusEffectType.Hasted => "haste",
        StatusEffectType.Invisible => null,
        StatusEffectType.Regenerating => "regenerating",
        StatusEffectType.Weakened => "weakened",
        StatusEffectType.Shielded => "shielded",
        StatusEffectType.Empowered => "empowered",
        StatusEffectType.Corroded => "corroded",
        StatusEffectType.Phased => "phased",
        StatusEffectType.Flying => "flying",
        _ => null,
    };

    private static string Normalize(string value) => value.Trim().Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
}
