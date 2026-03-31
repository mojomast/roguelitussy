using System;
using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class CooldownComponent
{
    private readonly Dictionary<string, int> _cooldowns = new();

    public bool IsOnCooldown(string abilityId) => _cooldowns.TryGetValue(abilityId, out var cd) && cd > 0;

    public int GetCooldown(string abilityId) => _cooldowns.TryGetValue(abilityId, out var cd) ? cd : 0;

    public void SetCooldown(string abilityId, int turns) => _cooldowns[abilityId] = turns;

    public void TickAll()
    {
        foreach (var key in new List<string>(_cooldowns.Keys))
        {
            _cooldowns[key] = Math.Max(0, _cooldowns[key] - 1);
        }
    }
}
