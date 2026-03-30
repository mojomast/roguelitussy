using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests;

public sealed class ArchitectureSmokeTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Architecture.Position distance helpers", PositionDistanceHelpers);
        registry.Add("Architecture.WorldState tracks blocking entities", WorldStateTracksBlockingEntities);
        registry.Add("Architecture.Stub services cooperate", StubServicesCooperate);
    }

    private static void PositionDistanceHelpers()
    {
        var a = new Position(1, 2);
        var b = new Position(4, 6);

        Expect.Equal(7, a.DistanceTo(b), "Manhattan distance should match");
        Expect.Equal(4, a.ChebyshevTo(b), "Chebyshev distance should match");
    }

    private static void WorldStateTracksBlockingEntities()
    {
        var world = new WorldState();
        world.InitGrid(5, 5);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        world.Player = player;
        world.AddEntity(player);

        Expect.False(world.IsWalkable(new Position(1, 1)), "A blocking entity should prevent walking onto its tile");
        Expect.NotNull(world.GetEntityAt(new Position(1, 1)), "Entity lookup should resolve the blocking entity");
    }

    private static void StubServicesCooperate()
    {
        var generator = new StubGenerator();
        var world = new WorldState();
        var data = generator.GenerateLevel(world, 123, 0);
        var player = new StubEntity("Player", data.PlayerSpawn, Faction.Player);
        world.Player = player;
        world.AddEntity(player);

        var content = new StubContentDatabase();
        var saveManager = new StubSaveManager();

        Expect.True(content.TryGetItemTemplate("potion_health", out _), "Stub content should expose an item template");
        Expect.True(saveManager.SaveGame(world, 0).Result, "Stub save manager should accept saving a world");
    }
}