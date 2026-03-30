using System;
using System.Linq;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class WorldStateTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.WorldState adds entity on walkable tile", AddsEntityOnWalkableTile);
        registry.Add("Simulation.WorldState rejects occupied tile", RejectsOccupiedTile);
        registry.Add("Simulation.WorldState moves and removes entities", MovesAndRemovesEntities);
        registry.Add("Simulation.WorldState returns entities in radius deterministically", ReturnsEntitiesInRadius);
        registry.Add("Simulation.WorldState reports walkability and opacity", ReportsWalkabilityAndOpacity);
        registry.Add("Simulation.WorldState round trips ground items", RoundTripsGroundItems);
    }

    private static void AddsEntityOnWalkableTile()
    {
        var world = CreateWorld();
        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);

        world.Player = player;
        world.AddEntity(player);

        var resolvedById = world.GetEntity(player.Id);
        var resolvedByPosition = world.GetEntityAt(new Position(1, 1));
        Expect.NotNull(resolvedById, "World should track entities by id");
        Expect.NotNull(resolvedByPosition, "World should index entities by position");
        Expect.Equal(player, resolvedById!, "World should track entities by id");
        Expect.Equal(player, resolvedByPosition!, "World should index entities by position");
    }

    private static void RejectsOccupiedTile()
    {
        var world = CreateWorld();
        var first = new StubEntity("First", new Position(1, 1));
        var second = new StubEntity("Second", new Position(1, 1));

        world.Player = first;
        world.AddEntity(first);

        var threw = false;
        try
        {
            world.AddEntity(second);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Expect.True(threw, "Adding a second entity to the same tile should fail");
    }

    private static void MovesAndRemovesEntities()
    {
        var world = CreateWorld();
        var entity = new StubEntity("Mover", new Position(1, 1));

        world.Player = entity;
        world.AddEntity(entity);

        Expect.True(world.MoveEntity(entity.Id, new Position(2, 1)), "MoveEntity should succeed on open floor");
        Expect.True(world.GetEntityAt(new Position(1, 1)) is null, "Old position should be free after moving");
        var movedEntity = world.GetEntityAt(new Position(2, 1));
        Expect.NotNull(movedEntity, "New position should contain the moved entity");
        Expect.Equal(entity, movedEntity!, "New position should contain the entity");

        world.RemoveEntity(entity.Id);
        Expect.True(world.GetEntity(entity.Id) is null, "Removed entity should no longer resolve by id");
        Expect.True(world.GetEntityAt(new Position(2, 1)) is null, "Removed entity should free its tile");
    }

    private static void ReturnsEntitiesInRadius()
    {
        var world = CreateWorld();
        var player = new StubEntity("Player", new Position(2, 2), Faction.Player);
        var near = new StubEntity("Near", new Position(3, 2));
        var alsoNear = new StubEntity("AlsoNear", new Position(4, 4));
        var far = new StubEntity("Far", new Position(7, 7));

        world.Player = player;
        world.AddEntity(player);
        world.AddEntity(near);
        world.AddEntity(alsoNear);
        world.AddEntity(far);

        var entities = world.GetEntitiesInRadius(new Position(2, 2), 2).ToArray();

        Expect.Equal(3, entities.Length, "Only entities within radius two should be returned");
        Expect.Equal(player, entities[0], "Results should be ordered by distance then position");
        Expect.Equal(near, entities[1], "Nearest neighbor should follow the center entity");
    }

    private static void ReportsWalkabilityAndOpacity()
    {
        var world = CreateWorld();

        world.SetTile(new Position(1, 1), TileType.Floor);
        world.SetTile(new Position(2, 1), TileType.StairsDown);
        world.SetTile(new Position(3, 1), TileType.StairsUp);
        world.SetTile(new Position(4, 1), TileType.Wall);
        world.SetTile(new Position(5, 1), TileType.Water);
        world.SetTile(new Position(6, 1), TileType.Lava);
        world.SetTile(new Position(7, 1), TileType.Door);

        Expect.True(world.IsWalkable(new Position(1, 1)), "Floor should be walkable");
        Expect.True(world.IsWalkable(new Position(2, 1)), "Down stairs should be walkable");
        Expect.True(world.IsWalkable(new Position(3, 1)), "Up stairs should be walkable");
        Expect.False(world.IsWalkable(new Position(4, 1)), "Walls should not be walkable");
        Expect.False(world.IsWalkable(new Position(5, 1)), "Water should not be walkable");
        Expect.False(world.IsWalkable(new Position(6, 1)), "Lava should not be walkable");
        Expect.False(world.IsWalkable(new Position(7, 1)), "Closed doors should not be walkable");
        Expect.True(world.BlocksSight(new Position(4, 1)), "Walls should block sight");
        Expect.True(world.BlocksSight(new Position(7, 1)), "Closed doors should block sight");

        world.SetDoorOpen(new Position(7, 1), true);

        Expect.True(world.IsWalkable(new Position(7, 1)), "Opened doors should be walkable");
        Expect.False(world.BlocksSight(new Position(7, 1)), "Opened doors should not block sight");
    }

    private static void RoundTripsGroundItems()
    {
        var world = CreateWorld();
        var item = new ItemInstance { TemplateId = "potion_health" };

        world.DropItem(new Position(1, 1), item);
        Expect.True(world.HasGroundItems(new Position(1, 1)), "Dropped item should appear on the ground");

        var pickedUp = world.PickupItem(new Position(1, 1));
        Expect.NotNull(pickedUp, "PickupItem should return the dropped item");
        Expect.Equal(item.InstanceId, pickedUp!.InstanceId, "Pickup should return the matching item instance");
        Expect.False(world.HasGroundItems(new Position(1, 1)), "Ground tile should be empty after pickup");
    }

    private static WorldState CreateWorld(int width = 10, int height = 10)
    {
        var world = new WorldState();
        world.InitGrid(width, height);
        world.Seed = 123;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        return world;
    }
}