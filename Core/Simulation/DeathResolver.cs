using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class DeathResolver
{
    public sealed record DeathResolution(
        bool Removed,
        int KillsAwarded,
        ProgressionService.AwardResult ProgressionAward,
        IReadOnlyList<ItemInstance> DroppedItems,
        int GoldAwarded,
        Position DropPosition);

    public static DeathResolution ResolveKill(WorldState world, IEntity killer, IEntity victim)
    {
        if (world.GetEntity(victim.Id) is null)
        {
            return new DeathResolution(false, 0, new ProgressionService.AwardResult(0, 0, Array.Empty<int>()), Array.Empty<ItemInstance>(), 0, victim.Position);
        }

        victim.Stats.HP = 0;

        var droppedItems = RollAndDropLoot(world, victim);
        var goldAwarded = AwardGoldToKiller(world, killer, victim);

        RelicProcessor.ProcessHook("on_kill", killer, world, world.ContentDatabase, new RelicHookContext
        {
            TargetId = victim.Id,
            EnemyTag = victim.GetComponent<EnemyComponent>()?.TemplateId,
        });

        var progression = killer.GetComponent<ProgressionComponent>();
        if (progression is null)
        {
            world.RemoveEntity(victim.Id);
            return new DeathResolution(true, 0, new ProgressionService.AwardResult(0, 0, Array.Empty<int>()), droppedItems, goldAwarded, victim.Position);
        }

        progression.Kills++;

        var xpValue = victim.GetComponent<XpValueComponent>();
        var award = xpValue is null
            ? new ProgressionService.AwardResult(0, 0, Array.Empty<int>())
            : ProgressionService.AwardExperience(killer, xpValue.Value);

        world.RemoveEntity(victim.Id);
        return new DeathResolution(true, 1, award, droppedItems, goldAwarded, victim.Position);
    }

    public static DeathResolution ResolveUnattributedDeath(WorldState world, IEntity victim)
    {
        if (world.GetEntity(victim.Id) is null)
        {
            return new DeathResolution(false, 0, new ProgressionService.AwardResult(0, 0, Array.Empty<int>()), Array.Empty<ItemInstance>(), 0, victim.Position);
        }

        victim.Stats.HP = 0;
        var droppedItems = RollAndDropLoot(world, victim);

        world.RemoveEntity(victim.Id);
        return new DeathResolution(true, 0, new ProgressionService.AwardResult(0, 0, Array.Empty<int>()), droppedItems, 0, victim.Position);
    }

    public static void AppendProgressionLogMessages(ICollection<string> logMessages, string killerName, DeathResolution resolution)
    {
        if (resolution.ProgressionAward.ExperienceGained > 0)
        {
            logMessages.Add($"{killerName} gains {resolution.ProgressionAward.ExperienceGained} XP.");
        }

        foreach (var level in resolution.ProgressionAward.ReachedLevels)
        {
            logMessages.Add($"{killerName} reaches level {level}!");
        }
    }

    public static void AppendLootLogMessages(ICollection<string> logMessages, DeathResolution resolution)
    {
        if (resolution.GoldAwarded > 0)
        {
            logMessages.Add($"{resolution.GoldAwarded} gold drops from the corpse.");

            // TODO(ITM-1): GameManager should emit EventBus.EmitCurrencyChanged(playerId, wallet.Gold)
            // when the killer is the player. The cleanest integration is to snapshot the player's wallet
            // gold before/after ProcessPlayerAction (or before/after EmitStateDelta) and emit when it
            // changes. Core cannot reference Godot/EventBus, so the presentation layer must own the event.
        }

        if (resolution.DroppedItems.Count == 0)
        {
            return;
        }

        var descriptions = resolution.DroppedItems
            .Select(item => item.StackCount > 1 ? $"{item.StackCount}x {item.TemplateId}" : item.TemplateId)
            .ToArray();
        logMessages.Add($"Loot drops: {string.Join(", ", descriptions)}.");
    }

    public static void AppendDeathLogMessages(ICollection<string> logMessages, string killerName, string victimName, DeathResolution resolution)
    {
        AppendProgressionLogMessages(logMessages, killerName, resolution);

        if (resolution.Removed)
        {
            logMessages.Add($"{killerName} kills {victimName}.");
        }

        AppendLootLogMessages(logMessages, resolution);
    }

    private static IReadOnlyList<ItemInstance> RollAndDropLoot(WorldState world, IEntity victim)
    {
        var enemyComponent = victim.GetComponent<EnemyComponent>();
        if (enemyComponent is null || world.ContentDatabase is null)
        {
            return Array.Empty<ItemInstance>();
        }

        if (!world.ContentDatabase.TryGetEnemyTemplate(enemyComponent.TemplateId, out var template)
            || string.IsNullOrWhiteSpace(template.LootTableId))
        {
            return Array.Empty<ItemInstance>();
        }

        if (world.ContentDatabase is not ContentLoader loader)
        {
            return Array.Empty<ItemInstance>();
        }

        var seed = MixSeed(world.Seed, world.Depth, victim.Position, world.TurnNumber, victim.Id);
        var rng = new Random(seed);
        var rolls = LootTableResolver.RollTable(loader, template.LootTableId, rng, world.Depth);

        var droppedItems = new List<ItemInstance>();
        foreach (var roll in rolls)
        {
            var item = new ItemInstance
            {
                InstanceId = world.AllocateItemInstanceId(),
                TemplateId = roll.ItemId!,
                StackCount = Math.Max(1, roll.Count),
                IsIdentified = false,
            };
            world.DropItem(victim.Position, item);
            droppedItems.Add(item);
        }

        return droppedItems;
    }

    private static int AwardGoldToKiller(WorldState world, IEntity killer, IEntity victim)
    {
        var enemyComponent = victim.GetComponent<EnemyComponent>();
        if (enemyComponent is null || world.ContentDatabase is null)
        {
            return 0;
        }

        if (!world.ContentDatabase.TryGetEnemyTemplate(enemyComponent.TemplateId, out var template)
            || template.GoldMax <= 0)
        {
            return 0;
        }

        var wallet = killer.GetComponent<WalletComponent>();
        if (wallet is null)
        {
            return 0;
        }

        var seed = unchecked((int)(MixSeed(world.Seed, world.Depth, victim.Position, world.TurnNumber, victim.Id) ^ 0x9E3779B9));
        var rng = new Random(seed);
        var gold = rng.Next(template.GoldMin, template.GoldMax + 1);
        wallet.Gold += gold;
        return gold;
    }

    private static int MixSeed(int seed, int depth, Position position, int turnNumber, EntityId entityId)
    {
        unchecked
        {
            var hash = seed;
            hash = (hash * 397) ^ depth;
            hash = (hash * 397) ^ position.X;
            hash = (hash * 397) ^ position.Y;
            hash = (hash * 397) ^ turnNumber;
            hash = (hash * 397) ^ StableEntityIdHash(entityId);
            return hash;
        }
    }

    private static int StableEntityIdHash(EntityId entityId)
    {
        Span<byte> bytes = stackalloc byte[16];
        entityId.Value.TryWriteBytes(bytes);

        unchecked
        {
            var hash = 17;
            foreach (var value in bytes)
            {
                hash = (hash * 31) + value;
            }

            return hash;
        }
    }
}
