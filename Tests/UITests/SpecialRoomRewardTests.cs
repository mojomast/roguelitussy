using System;
using System.Collections.Generic;
using System.Linq;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class SpecialRoomRewardTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.SpecialRoom populates shrine and safe recovery cache once", PopulatesShrineAndSafeCache);
    }

    private static void PopulatesShrineAndSafeCache()
    {
        var content = ContentLoader.LoadFromRepository();
        var manager = CreateManager(content, "shrine_stat");
        manager.StartNewGame(4242);

        var world = manager.World!;
        var shrine = world.Entities.Single(entity => entity.GetComponent<ShrineComponent>() is not null);
        var shrineData = shrine.GetComponent<ShrineComponent>()!;
        Expect.Equal(new Position(3, 1), shrine.Position, "The supplied shrine position should be retained when it is safe.");
        Expect.Equal("stat", shrineData.ShrineType, "Shrine type must come from its authored floor event.");
        Expect.Equal(8, shrineData.HPCost, "Shrine HP cost must come from its authored floor event.");
        Expect.False(shrine.BlocksMovement, "Shrines must be nonblocking neutral entities.");
        Expect.Equal("deep_chest_loot", world.Entities.Single(entity => entity.GetComponent<ChestComponent>() is not null)
            .GetComponent<ChestComponent>()!.LootTableId, "Special chest table must flow from generation metadata.");
        Expect.True(world.GetGroundItems().SelectMany(pair => pair.Value).Any(item => item.TemplateId == "potion_health"),
            "Safe-floor recovery potion metadata must create a ground item.");
        Expect.Equal(0, world.Entities.Count(entity => entity.Faction == Faction.Enemy), "Safe floors must not gain hostiles during population.");

        var populationCount = world.Entities.Count;
        Expect.True(manager.TravelToFloor(0), "Requesting the active cached floor should not regenerate it.");
        Expect.Equal(populationCount, world.Entities.Count, "Cached floor population must not duplicate special entities.");
    }

    private static GameManager CreateManager(IContentDatabase content, string eventId)
    {
        var manager = new GameManager();
        manager.AttachServices(new WorldState(), new TurnScheduler(), new SpecialRoomGenerator(eventId),
            new FOVCalculator(), content, new StubSaveManager(), new EventBus());
        return manager;
    }

    private sealed class SpecialRoomGenerator(string eventId) : IGenerator
    {
        public LevelData GenerateLevel(WorldState world, int seed, int depth)
        {
            world.InitGrid(8, 8);
            world.Seed = seed;
            world.Depth = depth;
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    world.SetTile(new Position(x, y), TileType.Floor);
                }
            }

            var start = new Position(1, 1);
            var exit = new Position(6, 6);
            world.SetTile(start, TileType.StairsUp);
            world.SetTile(exit, TileType.StairsDown);
            return new LevelData(
                start,
                exit,
                Array.Empty<Position>(),
                Array.Empty<Position>(),
                Array.Empty<RoomData>(),
                ChestSpawnDetails: new[] { new ChestSpawnData(new Position(4, 4), "deep_chest_loot") },
                ItemSpawnDetails: new[] { new ItemSpawnData(new Position(5, 5), "potion_health") },
                FloorType: FloorType.SafeFloor,
                ShrineSpawns: new[] { new ShrineSpawnData(new Position(3, 1), eventId) });
        }

        public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data) => Array.Empty<string>();
    }
}
