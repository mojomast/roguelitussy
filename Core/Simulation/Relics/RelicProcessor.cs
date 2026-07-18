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

        if (string.Equals(hook, "on_floor_enter", StringComparison.OrdinalIgnoreCase))
        {
            component.FirstHitEntityIds.Clear();
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

    /// <summary>
    /// Dispatches the on_hit relic hook for damage the attacker is about to deal and
    /// applies any active timed damage buff (e.g. Death Mask). Returns the modified damage.
    /// </summary>
    public static int ProcessOutgoingDamage(WorldState world, IEntity attacker, IEntity target, int damage, ICollection<string>? logMessages = null)
    {
        var component = attacker.GetComponent<RelicComponent>();
        if (component is null || component.RelicIds.Count == 0 || damage <= 0)
        {
            return damage;
        }

        var ctx = new RelicHookContext
        {
            TargetId = target.Id,
            DamageAmount = damage,
            ModifiedValue = damage,
            EnemyTag = target.GetComponent<EnemyComponent>()?.TemplateId,
        };
        ProcessHook("on_hit", attacker, world, world.ContentDatabase, ctx);
        var result = Math.Max(0, ctx.ModifiedValue);

        if (component.DamageBuffPercent > 0)
        {
            if (world.TurnNumber <= component.DamageBuffExpiresOnTurn)
            {
                if (result > 0)
                {
                    result += Math.Max(1, result * component.DamageBuffPercent / 100);
                }
            }
            else
            {
                component.DamageBuffPercent = 0;
                component.DamageBuffExpiresOnTurn = 0;
            }
        }

        FlushLog(ctx, logMessages);
        return result;
    }

    /// <summary>
    /// Applies damage to the victim, routing it through the on_damaged relic hook
    /// (negation, cushioning, reflection, shield charges) and firing on_low_hp when the
    /// victim crosses the low-HP threshold or would die. Returns the damage actually applied.
    /// </summary>
    public static int ApplyIncomingDamage(WorldState world, IEntity victim, EntityId? attackerId, int damage, ICollection<string>? logMessages = null)
    {
        damage = Math.Max(0, damage);
        var component = victim.GetComponent<RelicComponent>();
        if (component is null || component.RelicIds.Count == 0)
        {
            victim.Stats.HP -= damage;
            return damage;
        }

        var hpBefore = victim.Stats.HP;
        if (damage > 0)
        {
            var ctx = new RelicHookContext
            {
                TargetId = attackerId,
                DamageAmount = damage,
                ModifiedValue = damage,
            };
            ProcessHook("on_damaged", victim, world, world.ContentDatabase, ctx);
            damage = Math.Max(0, ctx.ModifiedValue);
            FlushLog(ctx, logMessages);

            if (component.ShieldCharges > 0 && damage > 0)
            {
                var absorbed = Math.Min(component.ShieldCharges, damage);
                component.ShieldCharges -= absorbed;
                damage -= absorbed;
                logMessages?.Add($"{victim.Name}'s shield absorbs {absorbed} damage.");
            }
        }

        victim.Stats.HP -= damage;

        var threshold = victim.Stats.MaxHP / 4;
        if (victim.Stats.HP <= 0 || (hpBefore > threshold && victim.Stats.HP <= threshold))
        {
            var lowCtx = new RelicHookContext
            {
                TargetId = attackerId,
                DamageAmount = damage,
                ModifiedValue = damage,
            };
            ProcessHook("on_low_hp", victim, world, world.ContentDatabase, lowCtx);
            FlushLog(lowCtx, logMessages);
        }

        return damage;
    }

    /// <summary>
    /// End-of-round relic upkeep (e.g. Cursed Blade's HP drain). Called by GameLoop.
    /// </summary>
    public static void ProcessRoundEnd(WorldState world, IEntity player, ICollection<string> logMessages)
    {
        var component = player.GetComponent<RelicComponent>();
        if (component is null || !player.IsAlive || !component.HasRelic("cursed_blade"))
        {
            return;
        }

        player.Stats.HP -= 1;
        logMessages.Add($"Cursed Blade saps 1 HP from {player.Name}.");
        if (player.Stats.HP <= 0)
        {
            var death = DeathResolver.ResolveUnattributedDeath(world, player);
            logMessages.Add($"{player.Name} succumbs to the Cursed Blade.");
            DeathResolver.AppendLootLogMessages(logMessages, death);
        }
    }

    private static void FlushLog(RelicHookContext ctx, ICollection<string>? logMessages)
    {
        if (logMessages is null)
        {
            return;
        }

        foreach (var message in ctx.LogMessages)
        {
            logMessages.Add(message);
        }
    }

    private static void ApplyRelic(RelicTemplate relic, RelicComponent component, IEntity player, IWorldState world, RelicHookContext ctx)
    {
        switch (relic.EffectType)
        {
            case "heal":
                var healed = Math.Min(Math.Max(0, relic.EffectValue), Math.Max(0, player.Stats.MaxHP - player.Stats.HP));
                if (healed > 0)
                {
                    player.Stats.HP += healed;
                    ctx.LogMessages.Add($"{relic.DisplayName} restores {healed} HP.");
                }
                break;
            case "damage_bonus":
                ApplyDamageBonus(relic, component, player, world, ctx);
                break;
            case "gold_bonus":
                var wallet = player.GetComponent<WalletComponent>();
                if (wallet is not null && relic.EffectValue > 0)
                {
                    wallet.Gold += relic.EffectValue;
                    ctx.LogMessages.Add($"{relic.DisplayName} grants {relic.EffectValue} gold.");
                }
                break;
            case "stat_mod":
                ApplyStatMod(relic, component, player, world, ctx);
                break;
            case "shield":
                ApplyShield(relic, component, player, world, ctx);
                break;
            case "echo_bonus":
                if (relic.RelicId == "soul_collector")
                {
                    var progression = player.GetComponent<ProgressionComponent>();
                    if (progression is not null && progression.Kills > 0 && progression.Kills % 5 == 0)
                    {
                        var amount = Math.Max(0, relic.EffectValue);
                        player.Stats.MaxHP += amount;
                        player.Stats.HP += amount;
                        ctx.LogMessages.Add($"{relic.DisplayName} swells with souls: +{amount} max HP.");
                    }
                }

                break;
        }
    }

    private static void ApplyDamageBonus(RelicTemplate relic, RelicComponent component, IEntity player, IWorldState world, RelicHookContext ctx)
    {
        switch (relic.RelicId)
        {
            case "predator_mark":
                if (ctx.TargetId is { } markTargetId
                    && component.FirstHitEntityIds.Add(markTargetId)
                    && ctx.ModifiedValue > 0)
                {
                    ctx.ModifiedValue *= 2;
                    ctx.LogMessages.Add($"{relic.DisplayName} doubles the blow!");
                }

                return;
            case "death_mask":
                // on_damaged: arm a timed outgoing-damage buff instead of amplifying the incoming hit.
                component.DamageBuffPercent = Math.Max(0, relic.EffectValue);
                component.DamageBuffExpiresOnTurn = world.TurnNumber + 5;
                ctx.LogMessages.Add($"{relic.DisplayName} feeds on pain: +{relic.EffectValue}% damage for 5 turns.");
                return;
            case "thorn_wrap":
            case "mirror_shard":
                if (ctx.TargetId is { } attackerId
                    && world.GetEntity(attackerId) is { IsAlive: true } attacker
                    && relic.EffectValue > 0)
                {
                    attacker.Stats.HP -= relic.EffectValue;
                    ctx.LogMessages.Add($"{relic.DisplayName} reflects {relic.EffectValue} damage to {attacker.Name}.");
                    if (attacker.Stats.HP <= 0 && world is WorldState worldState)
                    {
                        var death = DeathResolver.ResolveKill(worldState, player, attacker);
                        DeathResolver.AppendDeathLogMessages(ctx.LogMessages, player.Name, attacker.Name, death);
                    }
                }

                return;
        }

        if (ctx.ModifiedValue > 0 && relic.EffectValue > 0)
        {
            ctx.ModifiedValue += relic.EffectValue;
            ctx.LogMessages.Add($"{relic.DisplayName} adds {relic.EffectValue} damage.");
        }
    }

    private static void ApplyStatMod(RelicTemplate relic, RelicComponent component, IEntity player, IWorldState world, RelicHookContext ctx)
    {
        switch (relic.RelicId)
        {
            case "glass_cannon":
                player.Stats.Attack *= 2;
                player.Stats.Defense = Math.Max(0, player.Stats.Defense / 2);
                break;
            case "berserker_heart":
                player.Stats.Attack += Math.Max(0, relic.EffectValue);
                ctx.LogMessages.Add($"{relic.DisplayName} surges: +{relic.EffectValue} attack!");
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
                    ctx.LogMessages.Add($"{relic.DisplayName} hardens: +{amount} max HP.");
                }
                break;
            case "shadow_step":
                StatusEffectProcessor.ApplyEffect(player, StatusEffectType.Hasted, 2, sourceEntityId: player.Id);
                ctx.LogMessages.Add($"{relic.DisplayName} quickens your step.");
                break;
            case "cartographer_lens":
                if (world is WorldState lensWorld)
                {
                    var explored = lensWorld.GetRawExplored();
                    Array.Fill(explored, true);
                    ctx.LogMessages.Add($"{relic.DisplayName} reveals the floor.");
                }
                break;
            case "merchant_badge":
                ApplyMerchantDiscount(relic, component, world, ctx);
                break;
            case "cursed_blade":
                if (component.AppliedOneTimeRelics.Add(relic.RelicId))
                {
                    player.Stats.Attack += Math.Max(0, relic.EffectValue);
                    ctx.LogMessages.Add($"{relic.DisplayName} thrums: +{relic.EffectValue} attack, but it drinks 1 HP each turn.");
                }
                break;
        }
    }

    private static void ApplyMerchantDiscount(RelicTemplate relic, RelicComponent component, IWorldState world, RelicHookContext ctx)
    {
        if (component.LastMerchantDiscountDepth == world.Depth)
        {
            return;
        }

        component.LastMerchantDiscountDepth = world.Depth;
        var discounted = false;
        foreach (var entity in world.Entities)
        {
            var merchant = entity.GetComponent<MerchantComponent>();
            if (merchant is null)
            {
                continue;
            }

            foreach (var offer in merchant.Offers)
            {
                if (offer.Price > 1)
                {
                    offer.Price = Math.Max(1, offer.Price * (100 - Math.Max(0, relic.EffectValue)) / 100);
                    discounted = true;
                }
            }
        }

        if (discounted)
        {
            ctx.LogMessages.Add($"{relic.DisplayName} secures better prices from merchants.");
        }
    }

    private static void ApplyShield(RelicTemplate relic, RelicComponent component, IEntity player, IWorldState world, RelicHookContext ctx)
    {
        switch (relic.RelicId)
        {
            case "iron_skin":
                var previousCharges = component.ShieldCharges;
                component.ShieldCharges = Math.Max(component.ShieldCharges, Math.Max(0, relic.EffectValue));
                if (component.ShieldCharges > previousCharges)
                {
                    ctx.LogMessages.Add($"{relic.DisplayName} forms a {component.ShieldCharges} HP shield.");
                }
                break;
            case "lucky_coin":
                if (ctx.ModifiedValue > 0 && world is WorldState luckWorld)
                {
                    luckWorld.CombatResolver ??= new CombatResolver(luckWorld.Seed);
                    if (luckWorld.CombatResolver.NextRandom(100) < 10)
                    {
                        ctx.ModifiedValue = 0;
                        ctx.LogMessages.Add($"{relic.DisplayName} flashes and negates the damage!");
                    }
                }
                break;
            case "void_amulet":
                if (ctx.ModifiedValue > 0 && relic.EffectValue > 0)
                {
                    var cushioned = ctx.ModifiedValue - Math.Max(1, ctx.ModifiedValue - relic.EffectValue);
                    if (cushioned > 0)
                    {
                        ctx.ModifiedValue -= cushioned;
                        ctx.LogMessages.Add($"{relic.DisplayName} cushions the blow (-{cushioned} damage).");
                    }
                }
                break;
            case "time_anchor":
                var removedHaste = StatusEffectProcessor.RemoveEffect(player, StatusEffectType.Hasted);
                var removedFrozen = StatusEffectProcessor.RemoveEffect(player, StatusEffectType.Frozen);
                if (removedHaste || removedFrozen)
                {
                    ctx.LogMessages.Add($"{relic.DisplayName} steadies your tempo.");
                }
                break;
            case "phoenix_ash":
                if (!component.LowHpRelicFired && player.Stats.HP <= 0)
                {
                    component.LowHpRelicFired = true;
                    player.Stats.HP = Math.Max(1, relic.EffectValue);
                    ctx.LogMessages.Add($"{relic.DisplayName} blazes! {player.Name} survives with {player.Stats.HP} HP.");
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
            R("mirror_shard", "Mirror Shard", "Reflect a sliver of damage back when struck.", "uncommon", "on_damaged", "damage_bonus", 2),
            R("time_anchor", "Time Anchor", "Start each floor steadied against haste and slow effects.", "rare", "on_floor_enter", "shield", 3),
            R("soul_collector", "Soul Collector", "Gain extra power from each kill.", "rare", "on_kill", "echo_bonus", 3),
            R("void_amulet", "Void Amulet", "Darkness cushions incoming blows.", "legendary", "on_damaged", "shield", 4),
            R("alchemist_stone", "Alchemist Stone", "Healing effects are modestly amplified.", "uncommon", "on_rest", "heal", 2),
        };

        return relics.ToDictionary(relic => relic.RelicId, StringComparer.OrdinalIgnoreCase);
    }
}
