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
        var rolledItemCount = 0;
        var stowedDescriptions = new List<string>();
        var spilledDescriptions = new List<string>();
        var inventory = actor.GetComponent<InventoryComponent>();

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

                if (TryAddToInventory(inventory, loader, item))
                {
                    stowedDescriptions.Add(description);
                }
                else
                {
                    world.DropItem(chest.Position, item);
                    spilledDescriptions.Add(description);
                }
            }
        }

        world.RemoveEntity(chest.Id);

        var outcome = new ActionOutcome
        {
            Result = ActionResult.Success,
            DirtyPositions = { actor.Position, chest.Position },
        };

        if (rolledItemCount <= 0)
        {
            outcome.LogMessages.Add($"{actor.Name} opens the chest, but it is empty.");
            return outcome;
        }

        var stowedText = JoinDistinct(stowedDescriptions);
        var spilledText = JoinDistinct(spilledDescriptions);
        if (!string.IsNullOrWhiteSpace(stowedText) && !string.IsNullOrWhiteSpace(spilledText))
        {
            outcome.LogMessages.Add($"{actor.Name} opens the chest and stows {stowedText}; the rest spills onto the floor: {spilledText}.");
        }
        else if (!string.IsNullOrWhiteSpace(stowedText))
        {
            outcome.LogMessages.Add($"{actor.Name} opens the chest and stows {stowedText}.");
        }
        else
        {
            outcome.LogMessages.Add($"{actor.Name} opens the chest and spills {spilledText} onto the floor.");
        }

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

    private static bool TryAddToInventory(InventoryComponent? inventory, ContentLoader loader, ItemInstance item)
    {
        if (inventory is null || !loader.TryGetItemTemplate(item.TemplateId, out var template))
        {
            return false;
        }

        return template.MaxStack > 1
            ? inventory.AddWithStacking(item, template.MaxStack)
            : inventory.Add(item);
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

    private static string JoinDistinct(List<string> entries)
    {
        return string.Join(", ", entries.Distinct(StringComparer.Ordinal));
    }
}