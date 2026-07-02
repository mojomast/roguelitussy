using System;

namespace Roguelike.Core;

public static class ReputationService
{
    public static void OnEnemyKilled(IEntity player, string enemyTemplateId, IContentDatabase? content)
    {
        Adjust(player, "warriors_order", enemyTemplateId.StartsWith("boss_", StringComparison.Ordinal) ? 10 : 3);
        if (enemyTemplateId.StartsWith("boss_", StringComparison.Ordinal))
        {
            Adjust(player, "thieves_compact", 5);
            Adjust(player, "merchants_guild", -5);
        }
    }

    public static void OnBossKilled(IEntity player, IContentDatabase? content)
    {
        Adjust(player, "warriors_order", 10);
        Adjust(player, "thieves_compact", 5);
        Adjust(player, "merchants_guild", -5);
    }

    public static void OnShopSteal(IEntity player, IContentDatabase? content)
    {
        Adjust(player, "merchants_guild", -20);
        Adjust(player, "thieves_compact", 15);
    }

    public static void OnShopPurchase(IEntity player, int goldSpent, IContentDatabase? content) =>
        Adjust(player, "merchants_guild", Math.Max(0, goldSpent) / 50);

    public static void OnShrineUsed(IEntity player, IContentDatabase? content) =>
        Adjust(player, "thieves_compact", 5);

    public static void ApplyPassiveStats(IEntity player)
    {
        var reputation = GetOrCreate(player);
        if (reputation.Get("thieves_compact") >= 50)
        {
            player.Stats.Evasion += 5;
        }
    }

    public static string GetReputationLabel(int rep) => rep switch
    {
        <= -30 => "Hostile",
        <= -10 => "Unfriendly",
        >= 60 => "Honored",
        >= 30 => "Friendly",
        _ => "Neutral",
    };

    private static void Adjust(IEntity player, string factionId, int delta)
    {
        var component = GetOrCreate(player);
        component.Reputation[factionId] = component.Get(factionId) + delta;
    }

    private static FactionComponent GetOrCreate(IEntity player)
    {
        var component = player.GetComponent<FactionComponent>();
        if (component is not null)
        {
            return component;
        }

        component = new FactionComponent();
        player.SetComponent(component);
        return component;
    }
}
