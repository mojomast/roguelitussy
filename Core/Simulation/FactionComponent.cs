using System.Collections.Generic;

namespace Roguelike.Core;

public sealed class FactionComponent
{
    public Dictionary<string, int> Reputation { get; } = new()
    {
        ["merchants_guild"] = 0,
        ["thieves_compact"] = 0,
        ["warriors_order"] = 0,
    };

    public bool IsMerchantsGuildHostile => Get("merchants_guild") <= -30;

    public bool IsThievesCompactFriendly => Get("thieves_compact") >= 25;

    public bool IsWarriorsOrderFriendly => Get("warriors_order") >= 20;

    public int Get(string factionId) => Reputation.TryGetValue(factionId, out var value) ? value : 0;
}
