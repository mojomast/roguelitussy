using Roguelike.Core;

namespace Godotussy;

internal static class UIActionFactory
{
    public static IAction? CreateDirectionalAction(IWorldState? world, EntityId actorId, Position delta)
    {
        if (world is null || delta == Position.Zero)
        {
            return null;
        }

        var actor = world.GetEntity(actorId);
        if (actor is null)
        {
            return null;
        }

        var target = actor.Position + delta;
        var occupant = world.GetEntityAt(target);
        if (occupant is not null && occupant.Faction != actor.Faction)
        {
            return new AttackAction(actorId, occupant.Id);
        }

        if (world is WorldState mutableWorld && world.GetTile(target) == TileType.Door && !mutableWorld.IsDoorOpen(target))
        {
            return new OpenDoorAction(actorId, target);
        }

        return new MoveAction(actorId, delta);
    }

    public static IAction? CreateWaitAction(IWorldState? world, EntityId actorId)
    {
        return world?.GetEntity(actorId) is null ? null : new WaitAction(actorId);
    }

    public static IAction? CreatePickupAction(IWorldState? world, EntityId actorId)
    {
        return world?.GetEntity(actorId) is null ? null : new PickupAction(actorId);
    }

    public static IAction? CreateStairsAction(IWorldState? world, EntityId actorId)
    {
        var actor = world?.GetEntity(actorId);
        if (actor is null || world is null)
        {
            return null;
        }

        return world.GetTile(actor.Position) switch
        {
            TileType.StairsDown => new DescendAction(actorId),
            TileType.StairsUp => new AscendAction(actorId),
            _ => null,
        };
    }

    public static IAction? CreateUseItemAction(IWorldState? world, IContentDatabase? content, EntityId actorId, EntityId itemInstanceId)
    {
        if (world is null || content is null)
        {
            return null;
        }

        var actor = world.GetEntity(actorId);
        var inventory = actor?.GetComponent<InventoryComponent>();
        var item = inventory?.Get(itemInstanceId);
        if (item is null || !content.TryGetItemTemplate(item.TemplateId, out var template))
        {
            return null;
        }

        return new UseItemAction(actorId, itemInstanceId, template);
    }

    public static IAction? CreateDropItemAction(IWorldState? world, EntityId actorId, EntityId itemInstanceId)
    {
        return world?.GetEntity(actorId) is null ? null : new DropItemAction(actorId, itemInstanceId);
    }
}