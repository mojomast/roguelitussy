using System;
using System.IO;
using System.Text.Json;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class ProgressionPersistenceTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.Progression data round-trips through save/load", ProgressionRoundTrip);
        registry.Add("Persistence.Progression v3 saves load without progression", V3SavesMigrateWithoutProgression);
    }

    private static void ProgressionRoundTrip()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(5, 5);

        var player = world.Player;
        player.SetComponent(new ProgressionComponent
        {
            Level = 4,
            Experience = 420,
            ExperienceToNextLevel = 500,
            UnspentStatPoints = 3,
            Kills = 12,
        });

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed for progression round-trip");

        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Saved world should load again");

        var restoredPlayer = restored!.Player;
        var progression = restoredPlayer.GetComponent<ProgressionComponent>();
        Expect.NotNull(progression, "Progression component should survive round-trip");
        Expect.Equal(4, progression!.Level, "Level should survive round-trip");
        Expect.Equal(420, progression.Experience, "Experience should survive round-trip");
        Expect.Equal(500, progression.ExperienceToNextLevel, "ExperienceToNextLevel should survive round-trip");
        Expect.Equal(3, progression.UnspentStatPoints, "UnspentStatPoints should survive round-trip");
        Expect.Equal(12, progression.Kills, "Kills should survive round-trip");
    }

    private static void V3SavesMigrateWithoutProgression()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);

        var world = CreateWorld(5, 5);
        var json = SaveSerializer.ToJson(world, sandbox.Clock());

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var mutableJson = JsonSerializer.Deserialize<JsonElement>(json);

        var v3Json = json.Replace($"\"version\": {SaveSerializer.CurrentVersion}", "\"version\": 3");

        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)), v3Json);

        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "V3 saves should load successfully after migration");

        var player = restored!.Player;
        Expect.NotNull(player, "Player should exist after migration");

        var progression = player.GetComponent<ProgressionComponent>();
        Expect.True(progression is null, "V3 saves should not fabricate progression data");
    }

    private static WorldState CreateWorld(int width, int height)
    {
        var world = new WorldState();
        world.InitGrid(width, height);
        world.Seed = 9001;
        world.Depth = 2;
        var player = new Entity(
            "Hero",
            new Position(0, 0),
            new Stats { HP = 10, MaxHP = 10, Attack = 2, Accuracy = 1, Defense = 1, Evasion = 0, Speed = 100, ViewRadius = 8, Energy = 1000 },
            Faction.Player,
            id: new EntityId(Guid.Parse("11111111-1111-1111-1111-111111111111")));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.Player = player;
        world.AddEntity(player);
        return world;
    }

    private sealed class SaveSandbox : IDisposable
    {
        private SaveSandbox(string directoryPath, DateTime timestamp)
        {
            DirectoryPath = directoryPath;
            Timestamp = timestamp;
        }

        public string DirectoryPath { get; }

        public DateTime Timestamp { get; }

        public Func<DateTime> Clock => () => Timestamp;

        public static SaveSandbox Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-progression-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new SaveSandbox(directoryPath, new DateTime(2026, 3, 30, 12, 0, 0, DateTimeKind.Utc));
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }
}
