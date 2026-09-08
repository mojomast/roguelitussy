using System;
using System.IO;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class ReputationPersistenceTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.Faction reputation state drives the same discount after load", LoadedFactionStateDrivesDiscount);
    }

    private static void LoadedFactionStateDrivesDiscount()
    {
        var content = ContentLoader.LoadFromRepository();
        var player = CreatePlayer();
        player.GetComponent<FactionComponent>()!.Reputation["merchants_guild"] = 30;
        var world = CreateWorld(player, content);
        var directory = Path.Combine(Path.GetTempPath(), "roguelitussy-reputation-" + Guid.NewGuid().ToString("N"));
        var manager = new SaveManager(directory, () => new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc));

        try
        {
            Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Faction test world should save");
            var loaded = manager.LoadGame(SaveSlots.Slot1, content).GetAwaiter().GetResult();
            Expect.NotNull(loaded, "Faction test world should load");
            var loadedPlayer = loaded!.Player!;
            Expect.Equal(30, loadedPlayer.GetComponent<FactionComponent>()!.Get("merchants_guild"), "Faction reputation should round-trip as component state");
            Expect.Equal(10, ReputationService.ResolveMerchantDiscountPercent(loadedPlayer, content), "Loaded faction state should produce the same derived discount");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static Entity CreatePlayer()
    {
        var player = new Entity("Player", new Position(1, 1), new Stats { HP = 20, MaxHP = 20, Attack = 5, Defense = 3, Evasion = 2, Speed = 100 }, Faction.Player);
        player.SetComponent(new FactionComponent());
        return player;
    }

    private static WorldState CreateWorld(Entity player, IContentDatabase content)
    {
        var world = new WorldState { Seed = 1234, Depth = 1, ContentDatabase = content };
        world.InitGrid(4, 4);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.Player = player;
        world.AddEntity(player);
        return world;
    }
}
