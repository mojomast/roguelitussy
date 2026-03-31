using System;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.SimulationTests;

public sealed class IdentityTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Simulation.Identity component stores race gender appearance", IdentityComponentStoresValues);
        registry.Add("Simulation.Identity defaults to human neutral default", IdentityDefaultValues);
        registry.Add("Simulation.Identity round-trips through save and load", IdentityRoundTrips);
        registry.Add("Simulation.Identity absent identity does not break save", AbsentIdentityDoesNotBreakSave);
    }

    private static void IdentityComponentStoresValues()
    {
        var entity = new StubEntity("Hero", Position.Zero, Faction.Player);
        entity.SetComponent(new IdentityComponent
        {
            RaceId = "elf",
            GenderId = "feminine",
            AppearanceId = "scarred",
            SpriteVariantId = "elf_skirmisher",
        });

        var identity = entity.GetComponent<IdentityComponent>()!;
        Expect.Equal("elf", identity.RaceId, "Race should be stored");
        Expect.Equal("feminine", identity.GenderId, "Gender should be stored");
        Expect.Equal("scarred", identity.AppearanceId, "Appearance should be stored");
        Expect.Equal("elf_skirmisher", identity.SpriteVariantId, "Sprite variant should be stored");
    }

    private static void IdentityDefaultValues()
    {
        var identity = new IdentityComponent();
        Expect.Equal("human", identity.RaceId, "Default race should be human");
        Expect.Equal("neutral", identity.GenderId, "Default gender should be neutral");
        Expect.Equal("default", identity.AppearanceId, "Default appearance should be default");
        Expect.Equal("default", identity.SpriteVariantId, "Default sprite variant should be default");
    }

    private static void IdentityRoundTrips()
    {
        var sandbox = CreateSandbox();
        try
        {
            var manager = new SaveManager(sandbox, () => new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc));
            var world = CreateWorld();
            var player = world.Player;
            player.SetComponent(new IdentityComponent
            {
                RaceId = "dwarf",
                GenderId = "masculine",
                AppearanceId = "weathered",
                SpriteVariantId = "dwarf_vanguard",
            });

            Expect.True(manager.SaveGame(world, 1).GetAwaiter().GetResult(), "Save with identity should succeed");
            var restored = manager.LoadGame(1).GetAwaiter().GetResult();
            Expect.NotNull(restored, "Saved world should load");

            var restoredIdentity = restored!.Player.GetComponent<IdentityComponent>();
            Expect.NotNull(restoredIdentity, "Identity should survive save/load");
            Expect.Equal("dwarf", restoredIdentity!.RaceId, "Race should round-trip");
            Expect.Equal("masculine", restoredIdentity.GenderId, "Gender should round-trip");
            Expect.Equal("weathered", restoredIdentity.AppearanceId, "Appearance should round-trip");
            Expect.Equal("dwarf_vanguard", restoredIdentity.SpriteVariantId, "Sprite variant should round-trip");
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static void AbsentIdentityDoesNotBreakSave()
    {
        var sandbox = CreateSandbox();
        try
        {
            var manager = new SaveManager(sandbox, () => new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc));
            var world = CreateWorld();

            Expect.True(manager.SaveGame(world, 1).GetAwaiter().GetResult(), "Save without identity should succeed");
            var restored = manager.LoadGame(1).GetAwaiter().GetResult();
            Expect.NotNull(restored, "Saved world should load without identity");

            var restoredIdentity = restored!.Player.GetComponent<IdentityComponent>();
            Expect.True(restoredIdentity is null, "No identity component should be restored when none was saved");
        }
        finally
        {
            CleanupSandbox(sandbox);
        }
    }

    private static WorldState CreateWorld()
    {
        var world = new WorldState();
        world.InitGrid(5, 5);
        world.Seed = 42;
        world.Depth = 1;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new Entity(
            "Hero",
            new Position(0, 0),
            new Stats { HP = 10, MaxHP = 10, Attack = 2, Accuracy = 1, Defense = 1, Evasion = 0, Speed = 100, ViewRadius = 8, Energy = 1000 },
            Faction.Player,
            id: new EntityId(Guid.Parse("22222222-2222-2222-2222-222222222222")));
        world.Player = player;
        world.AddEntity(player);
        return world;
    }

    private static string CreateSandbox()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "godotussy-identity-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupSandbox(string path)
    {
        if (System.IO.Directory.Exists(path))
        {
            System.IO.Directory.Delete(path, true);
        }
    }
}
