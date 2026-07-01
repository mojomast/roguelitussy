using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class RelicProcessor
{
    private static readonly IReadOnlyDictionary<string, RelicTemplate> BuiltInRelics = CreateBuiltInRelics();

    public static bool AddRelic(IEntity player, IContentDatabase? content, string relicId)
    {
        if (!TryGetRelicTemplate(content, relicId, out var template))
        {
            return false;
        }

        var component = player.GetComponent<RelicComponent>();
        if (component is null)
        {
            component = new RelicComponent();
            player.SetComponent(component);
        }

        return component.AddRelic(relicId, template);
    }

    public static bool TryGetRelicTemplate(IContentDatabase? content, string relicId, out RelicTemplate template)
    {
        if (content is IRelicContentDatabase relicContent
            && relicContent.TryGetRelicTemplate(relicId, out template!))
        {
            return true;
        }

        return BuiltInRelics.TryGetValue(relicId, out template!);
    }

    public static IReadOnlyList<RelicTemplate> GetKnownRelics(IContentDatabase? content)
    {
        if (content is IRelicContentDatabase relicContent)
        {
            return relicContent.RelicTemplates.Values.ToArray();
        }

        return BuiltInRelics.Values.ToArray();
    }

    public static void ProcessHook(string hook, IEntity player, IWorldState world, IContentDatabase? content, RelicHookContext ctx)
    {
        var component = player.GetComponent<RelicComponent>();
        if (component is null || component.RelicIds.Count == 0)
        {
            return;
        }

        foreach (var relicId in component.RelicIds.ToArray())
        {
            if (!TryGetRelicTemplate(content, relicId, out var relic)
                || !string.Equals(relic.TriggerHook, hook, StringComparison.OrdinalIgnoreCase)
                || !MatchesCondition(relic, world, ctx))
            {
                continue;
            }

            ApplyRelic(relic, component, player, world, ctx);
        }
    }

    private static void ApplyRelic(RelicTemplate relic, RelicComponent component, IEntity player, IWorldState world, RelicHookContext ctx)
    {
        switch (relic.EffectType)
        {
            case "heal":
                player.Stats.HP = Math.Min(player.Stats.MaxHP, player.Stats.HP + Math.Max(0, relic.EffectValue));
                break;
            case "damage_bonus":
                ApplyDamageBonus(relic, world, ctx);
                break;
            case "gold_bonus":
                var wallet = player.GetComponent<WalletComponent>();
                if (wallet is not null)
                {
                    wallet.Gold += Math.Max(0, relic.EffectValue);
                }
                break;
            case "stat_mod":
                ApplyStatMod(relic, player, world);
                break;
            case "shield":
                ApplyShield(relic, component, player, ctx);
                break;
            case "echo_bonus":
                break;
        }
    }

    private static void ApplyDamageBonus(RelicTemplate relic, IWorldState world, RelicHookContext ctx)
    {
        if (relic.RelicId == "predator_mark" && ctx.ModifiedValue > 0)
        {
            ctx.ModifiedValue *= 2;
            return;
        }

        if (relic.RelicId == "death_mask" && ctx.ModifiedValue > 0)
        {
            ctx.ModifiedValue += Math.Max(1, ctx.ModifiedValue * relic.EffectValue / 100);
            return;
        }

        if (relic.RelicId == "thorn_wrap" && ctx.TargetId is { } attackerId)
        {
            var attacker = world.GetEntity(attackerId);
            if (attacker is not null && attacker.IsAlive)
            {
                attacker.Stats.HP = Math.Max(0, attacker.Stats.HP - Math.Max(0, relic.EffectValue));
            }

            return;
        }

        ctx.ModifiedValue += Math.Max(0, relic.EffectValue);
    }

    private static void ApplyStatMod(RelicTemplate relic, IEntity player, IWorldState world)
    {
        switch (relic.RelicId)
        {
            case "glass_cannon":
                player.Stats.Attack *= 2;
                player.Stats.Defense = Math.Max(0, player.Stats.Defense / 2);
                break;
            case "berserker_heart":
                player.Stats.Attack += Math.Max(0, relic.EffectValue);
                break;
            case "warlord_crest":
                player.Stats.Attack += Math.Min(10, Math.Max(0, world.Depth - 1) * Math.Max(0, relic.EffectValue));
                break;
            case "bone_amulet":
                var progression = player.GetComponent<ProgressionComponent>();
                if (progression is not null && progression.Kills > 0 && progression.Kills % 3 == 0)
                {
                    var amount = Math.Max(0, relic.EffectValue);
                    player.Stats.MaxHP += amount;
                    player.Stats.HP += amount;
                }
                break;
            case "shadow_step":
                StatusEffectProcessor.ApplyEffect(player, StatusEffectType.Hasted, 2, sourceEntityId: player.Id);
                break;
            case "cartographer_lens":
            case "merchant_badge":
            case "cursed_blade":
                break;
        }
    }

    private static void ApplyShield(RelicTemplate relic, RelicComponent component, IEntity player, RelicHookContext ctx)
    {
        switch (relic.RelicId)
        {
            case "iron_skin":
                component.ShieldCharges = Math.Max(component.ShieldCharges, Math.Max(0, relic.EffectValue));
                break;
            case "lucky_coin":
                if (ctx.ModifiedValue > 0 && DeterministicChance(player.Id, ctx.ModifiedValue, 10))
                {
                    ctx.ModifiedValue = 0;
                }
                break;
            case "phoenix_ash":
                if (!component.LowHpRelicFired && player.Stats.HP <= 0)
                {
                    component.LowHpRelicFired = true;
                    player.Stats.HP = Math.Max(1, relic.EffectValue);
                }
                break;
            default:
                if (component.ShieldCharges > 0 && ctx.ModifiedValue > 0)
                {
                    var absorbed = Math.Min(component.ShieldCharges, ctx.ModifiedValue);
                    component.ShieldCharges -= absorbed;
                    ctx.ModifiedValue -= absorbed;
                }
                break;
        }
    }

    private static bool MatchesCondition(RelicTemplate relic, IWorldState world, RelicHookContext ctx)
    {
        if (string.IsNullOrWhiteSpace(relic.ConditionTag))
        {
            return true;
        }

        if (string.Equals(relic.ConditionTag, ctx.ItemTag, StringComparison.OrdinalIgnoreCase)
            || string.Equals(relic.ConditionTag, ctx.EnemyTag, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(relic.ConditionTag, "poisoned", StringComparison.OrdinalIgnoreCase)
            && ctx.TargetId is { } targetId)
        {
            var target = world.GetEntity(targetId);
            return target is not null && StatusEffectProcessor.HasEffect(target, StatusEffectType.Poisoned);
        }

        return false;
    }

    private static bool DeterministicChance(EntityId entityId, int salt, int percent)
    {
        var hash = entityId.Value.GetHashCode();
        unchecked
        {
            hash = (hash * 397) ^ salt;
        }

        return Math.Abs(hash % 100) < percent;
    }

    private static IReadOnlyDictionary<string, RelicTemplate> CreateBuiltInRelics()
    {
        RelicTemplate R(string id, string name, string description, string rarity, string hook, string effect, int value, string? condition = null, bool unique = true) => new()
        {
            RelicId = id,
            DisplayName = name,
            Description = description,
            Rarity = rarity,
            TriggerHook = hook,
            EffectType = effect,
            EffectValue = value,
            ConditionTag = condition,
            IsUnique = unique,
        };

        var relics = new[]
        {
            R("vampire_fang", "Vampire Fang", "Heal 1 HP per kill.", "common", "on_kill", "heal", 1),
            R("glass_cannon", "Glass Cannon", "Double attack, half defense.", "rare", "on_floor_enter", "stat_mod", 0),
            R("toxic_core", "Toxic Core", "Poison ticks deal +3 bonus damage.", "uncommon", "on_poison_tick", "damage_bonus", 3),
            R("gold_tooth", "Gold Tooth", "Earn +2 gold per kill.", "common", "on_kill", "gold_bonus", 2),
            R("iron_skin", "Iron Skin", "Start each floor with a 5 HP shield.", "uncommon", "on_floor_enter", "shield", 5),
            R("berserker_heart", "Berserker Heart", "+4 attack when below 25% HP.", "rare", "on_low_hp", "stat_mod", 4),
            R("echo_shard", "Echo Shard", "Earn +5 bonus Echoes when floor is fully cleared.", "uncommon", "on_floor_enter", "echo_bonus", 5),
            R("thorn_wrap", "Thorn Wrap", "Reflect 1 damage to attacker when hit.", "common", "on_damaged", "damage_bonus", 1),
            R("lucky_coin", "Lucky Coin", "10% chance to negate damage taken.", "uncommon", "on_damaged", "shield", 0),
            R("rest_stone", "Rest Stone", "Heal +1 extra HP per rest tick.", "common", "on_rest", "heal", 1),
            R("warlord_crest", "Warlord's Crest", "+2 attack per floor cleared (max +10).", "legendary", "on_floor_enter", "stat_mod", 2),
            R("bone_amulet", "Bone Amulet", "+5 max HP per 3 enemies killed.", "uncommon", "on_kill", "stat_mod", 5),
            R("shadow_step", "Shadow Step", "+3 evasion for 2 turns after killing an enemy.", "rare", "on_kill", "stat_mod", 3),
            R("merchant_badge", "Merchant Badge", "Shop items cost 15% less.", "uncommon", "on_floor_enter", "stat_mod", 15),
            R("cursed_blade", "Cursed Blade", "+8 attack, but take 1 damage per turn.", "rare", "on_floor_enter", "stat_mod", 8),
            R("cartographer_lens", "Cartographer's Lens", "Reveal full floor map on enter.", "uncommon", "on_floor_enter", "stat_mod", 0),
            R("phoenix_ash", "Phoenix Ash", "Once per run: survive a lethal hit with 1 HP.", "legendary", "on_low_hp", "shield", 1),
            R("predator_mark", "Predator's Mark", "First hit on an enemy each floor deals double damage.", "rare", "on_hit", "damage_bonus", 0),
            R("leech_stone", "Leech Stone", "Poisoned enemies take +2 bonus damage from all attacks.", "uncommon", "on_hit", "damage_bonus", 2, "poisoned"),
            R("death_mask", "Death Mask", "+20% damage for 5 turns after taking damage.", "rare", "on_damaged", "damage_bonus", 20),
        };

        return relics.ToDictionary(relic => relic.RelicId, StringComparer.OrdinalIgnoreCase);
    }
}
