using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class StatusEffectProcessor
{
    public void Process(IEntity entity, WorldState world)
    {
        var effects = entity.GetComponent<List<StatusEffectInstance>>();
        if (effects == null || effects.Count == 0)
            return;

        for (int i = effects.Count - 1; i >= 0; i--)
        {
            var effect = effects[i];
            ApplyTick(entity, effect);

            int remaining = effect.RemainingTurns - 1;
            if (remaining <= 0)
            {
                effects.RemoveAt(i);
            }
            else
            {
                effects[i] = effect with { RemainingTurns = remaining };
            }
        }

        if (effects.Count == 0)
            entity.RemoveComponent<List<StatusEffectInstance>>();
    }

    public void AddEffect(IEntity entity, StatusEffectInstance effect)
    {
        var effects = entity.GetComponent<List<StatusEffectInstance>>();
        if (effects == null)
        {
            effects = new List<StatusEffectInstance>();
            entity.SetComponent(effects);
        }

        // Refresh if same type already exists
        int existing = effects.FindIndex(e => e.Type == effect.Type);
        if (existing >= 0)
        {
            effects[existing] = effect;
        }
        else
        {
            effects.Add(effect);
        }
    }

    private void ApplyTick(IEntity entity, StatusEffectInstance effect)
    {
        switch (effect.Type)
        {
            case StatusEffectType.Poisoned:
                entity.Stats.HP -= effect.Magnitude;
                break;
            case StatusEffectType.Burning:
                entity.Stats.HP -= effect.Magnitude;
                break;
            case StatusEffectType.Regenerating:
                entity.Stats.HP = System.Math.Min(entity.Stats.HP + effect.Magnitude, entity.Stats.MaxHP);
                break;
        }
    }
}
