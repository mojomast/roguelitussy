using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class StatusEffectsComponent
{
    private readonly List<StatusEffectInstance> _effects = new();

    public IReadOnlyList<StatusEffectInstance> Effects => _effects;

    public int Count => _effects.Count;

    public StatusEffectInstance this[int index] => _effects[index];

    internal int FindIndex(StatusEffectType type)
    {
        for (var i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Type == type)
            {
                return i;
            }
        }

        return -1;
    }

    internal void Add(StatusEffectInstance effect) => _effects.Add(effect);

    internal void Replace(int index, StatusEffectInstance effect) => _effects[index] = effect;

    internal void RemoveAt(int index) => _effects.RemoveAt(index);

    internal bool Remove(StatusEffectType type)
    {
        var index = FindIndex(type);
        if (index < 0)
        {
            return false;
        }

        _effects.RemoveAt(index);
        return true;
    }
}