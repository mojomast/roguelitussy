using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed class OpenChestAction : IAction
{
    public OpenChestAction(EntityId actorId, EntityId chestId)
    {
        ActorId = actorId;
        ChestId = chestId;
    }

    public EntityId ActorId { get; }

    public EntityId ChestId { get; }

    public ActionType Type => ActionType.OpenChest;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var chest = world.GetEntity(ChestId);
        if (actor is null || chest is null)
        {
            return ActionResult.Invalid;
        }

        return actor.Position.ChebyshevTo(chest.Position) <= 1 && chest.GetComponent<ChestComponent>() is not null
            ? ActionResult.Success
            : ActionResult.Invalid;
    }

    public ActionOutcome Execute(WorldState world)
    {
        var validation = Validate(world);
        if (validation != ActionResult.Success)
        {
            return ActionOutcome.Fail(validation);
        }

        var actor = world.GetEntity(ActorId)!;
        var chest = world.GetEntity(ChestId)!;
        var chestComponent = chest.GetComponent<ChestComponent>()!;
        var foundDescriptions = new List<string>();
        var rolledItemCount = 0;

        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position, chest.Position },
        };

        if (chestComponent.HasRolled)
        {
            outcome.LogMessages.Add(chestComponent.Contents.Count > 0
                ? $"{actor.Name} checks the open chest. Loot inside: {DescribeLoot(world.ContentDatabase, chestComponent.Contents)}."
                : $"{actor.Name} checks the open chest, but it is empty.");
            return outcome;
        }

        if (world.ContentDatabase is ContentLoader loader && loader.LootTables.ContainsKey(chestComponent.LootTableId))
        {
            var rng = new Random(MixSeed(world.Seed, world.Depth, chest.Position));
            var rolls = LootTableResolver.RollTable(loader, chestComponent.LootTableId, rng, world.Depth);
            foreach (var roll in rolls)
            {
                var item = new ItemInstance
                {
                    InstanceId = EntityId.NewSeeded(rng),
                    TemplateId = roll.ItemId,
                    StackCount = Math.Max(1, roll.Count),
                    IsIdentified = false,
                };
                var stackCount = Math.Max(1, roll.Count);
                rolledItemCount += stackCount;
                var description = DescribeLoot(loader, roll.ItemId, stackCount);
                foundDescriptions.Add(description);
                chestComponent.Contents.Add(item);
            }
        }

        chestComponent.HasRolled = true;

        if (rolledItemCount <= 0)
        {
            outcome.LogMessages.Add($"{actor.Name} opens the chest, but it is empty.");
            return outcome;
        }

        var foundText = JoinDistinct(foundDescriptions);
        outcome.LogMessages.Add($"{actor.Name} opens the chest. Loot found: {foundText}. Choose what to take.");

        return outcome;
    }

    public int GetEnergyCost() => 1000;

    private static int MixSeed(int seed, int depth, Position position)
    {
        unchecked
        {
            return seed ^ (depth * 7919) ^ (position.X * 73856093) ^ (position.Y * 19349663);
        }
    }

    private static string DescribeLoot(ContentLoader loader, string itemId, int stackCount)
    {
        if (loader.TryGetItemTemplate(itemId, out var itemTemplate))
        {
            return stackCount > 1
                ? $"{stackCount}x {itemTemplate.DisplayName}"
                : itemTemplate.DisplayName;
        }

        return stackCount > 1 ? $"{stackCount}x {itemId}" : itemId;
    }

    private static string DescribeLoot(IContentDatabase? content, IReadOnlyList<ItemInstance> items)
    {
        var descriptions = new List<string>();
        foreach (var item in items)
        {
            descriptions.Add(DescribeLoot(content, item));
        }

        return JoinDistinct(descriptions);
    }

    internal static string DescribeLoot(IContentDatabase? content, ItemInstance item)
    {
        var stackCount = Math.Max(1, item.StackCount);
        if (content is not null && content.TryGetItemTemplate(item.TemplateId, out var itemTemplate))
        {
            return stackCount > 1
                ? $"{stackCount}x {itemTemplate.DisplayName}"
                : itemTemplate.DisplayName;
        }

        return stackCount > 1 ? $"{stackCount}x {item.TemplateId}" : item.TemplateId;
    }

    private static string JoinDistinct(List<string> entries)
    {
        return string.Join(", ", entries.Distinct(StringComparer.Ordinal));
    }
}
