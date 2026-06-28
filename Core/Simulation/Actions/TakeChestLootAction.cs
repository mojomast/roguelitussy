using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public sealed class TakeChestLootAction : IAction
{
    public TakeChestLootAction(EntityId actorId, EntityId chestId, IEnumerable<EntityId>? selectedItemIds = null, bool takeAll = false)
    {
        ActorId = actorId;
        ChestId = chestId;
        SelectedItemIds = selectedItemIds?.Distinct().ToArray() ?? Array.Empty<EntityId>();
        TakeAll = takeAll || SelectedItemIds.Count == 0;
    }

    public EntityId ActorId { get; }

    public EntityId ChestId { get; }

    public IReadOnlyList<EntityId> SelectedItemIds { get; }

    public bool TakeAll { get; }

    public ActionType Type => ActionType.TakeChestLoot;

    public ActionResult Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var chest = world.GetEntity(ChestId);
        var chestComponent = chest?.GetComponent<ChestComponent>();
        if (actor is null || chest is null || chestComponent is null || actor.GetComponent<InventoryComponent>() is null)
        {
            return ActionResult.Invalid;
        }

        if (actor.Position.ChebyshevTo(chest.Position) > 1 || !chestComponent.HasRolled || chestComponent.Contents.Count == 0)
        {
            return ActionResult.Invalid;
        }

        if (TakeAll)
        {
            return ActionResult.Success;
        }

        return SelectedItemIds.Count > 0 && SelectedItemIds.All(id => chestComponent.Contents.Any(item => item.InstanceId == id))
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
        var inventory = actor.GetComponent<InventoryComponent>()!;
        var selected = ResolveSelectedItems(chestComponent);
        var stowedDescriptions = new List<string>();
        var leftDescriptions = new List<string>();

        foreach (var item in selected)
        {
            if (TryAddToInventory(world, inventory, item))
            {
                chestComponent.Contents.Remove(item);
                stowedDescriptions.Add(OpenChestAction.DescribeLoot(world.ContentDatabase, item));
            }
            else
            {
                leftDescriptions.Add(OpenChestAction.DescribeLoot(world.ContentDatabase, item));
            }
        }

        var outcome = new ActionOutcome
        {
            Result = stowedDescriptions.Count > 0 ? ActionResult.Success : ActionResult.Blocked,
            DirtyPositions = { actor.Position, chest.Position },
        };

        if (outcome.Result != ActionResult.Success)
        {
            outcome.LogMessages.Add($"{actor.Name}'s pack is too full to take anything from the chest.");
            return outcome;
        }

        var stowedText = JoinDistinct(stowedDescriptions);
        if (leftDescriptions.Count > 0)
        {
            outcome.LogMessages.Add($"{actor.Name} takes {stowedText}. Left in chest: {JoinDistinct(leftDescriptions)}.");
        }
        else
        {
            outcome.LogMessages.Add($"{actor.Name} takes {stowedText} from the chest.");
        }

        if (chestComponent.Contents.Count == 0)
        {
            world.RemoveEntity(chest.Id);
            outcome.LogMessages.Add("The chest is empty.");
        }

        return outcome;
    }

    public int GetEnergyCost() => 500;

    private List<ItemInstance> ResolveSelectedItems(ChestComponent chestComponent)
    {
        if (TakeAll)
        {
            return chestComponent.Contents.ToList();
        }

        var selectedIds = SelectedItemIds.ToHashSet();
        return chestComponent.Contents.Where(item => selectedIds.Contains(item.InstanceId)).ToList();
    }

    private static bool TryAddToInventory(WorldState world, InventoryComponent inventory, ItemInstance item)
    {
        var template = world.ContentDatabase is not null && world.ContentDatabase.TryGetItemTemplate(item.TemplateId, out var resolved)
            ? resolved
            : null;
        return template is not null && template.MaxStack > 1
            ? inventory.AddWithStacking(item, template.MaxStack, world.AllocateItemInstanceId)
            : inventory.Add(item);
    }

    private static string JoinDistinct(List<string> entries)
    {
        return string.Join(", ", entries.Distinct(StringComparer.Ordinal));
    }
}
