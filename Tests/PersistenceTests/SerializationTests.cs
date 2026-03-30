using System;
using System.IO;
using System.Text.Json;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class SerializationTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.SaveSerializer bit-packed flags round-trip", BitPackedFlagsRoundTrip);
        registry.Add("Persistence.SaveSerializer bit-packed all false", BitPackedAllFalse);
        registry.Add("Persistence.SaveSerializer bit-packed all true", BitPackedAllTrue);
        registry.Add("Core.EntityId seeded ids are deterministic", SeededIdsAreDeterministic);
    }

    private static void BitPackedFlagsRoundTrip()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(3, 3);
        world.SetVisible(new Position(0, 0), true);
        world.SetVisible(new Position(2, 2), true);
        world.SetVisible(new Position(1, 1), true);
        world.ClearVisibility();
        world.SetVisible(new Position(1, 1), true);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed for round-trip test");
        var json = File.ReadAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)));
        using var document = JsonDocument.Parse(json);
        var explored = Convert.FromBase64String(document.RootElement.GetProperty("explored").GetString()!);
        var visible = Convert.FromBase64String(document.RootElement.GetProperty("visible").GetString()!);

        Expect.Equal(2, explored.Length, "3x3 explored map should be bit-packed into 2 bytes");
        Expect.Equal(2, visible.Length, "3x3 visible map should be bit-packed into 2 bytes");

        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Saved world should load again");
        Expect.True(restored!.IsExplored(new Position(0, 0)), "Explored flag should survive round-trip");
        Expect.True(restored.IsExplored(new Position(2, 2)), "Explored flags should preserve multiple cells");
        Expect.True(restored.IsVisible(new Position(1, 1)), "Visible flags should survive round-trip");
        Expect.False(restored.IsVisible(new Position(0, 0)), "Only current visibility should remain visible after reload");
    }

    private static void BitPackedAllFalse()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(10, 10);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed for all-false flags");
        var json = File.ReadAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)));
        using var document = JsonDocument.Parse(json);
        var explored = Convert.FromBase64String(document.RootElement.GetProperty("explored").GetString()!);
        var visible = Convert.FromBase64String(document.RootElement.GetProperty("visible").GetString()!);

        Expect.Equal(13, explored.Length, "100 explored cells should occupy 13 packed bytes");
        Expect.Equal(13, visible.Length, "100 visible cells should occupy 13 packed bytes");
    }

    private static void BitPackedAllTrue()
    {
        using var sandbox = SaveSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath, sandbox.Clock);
        var world = CreateWorld(10, 10);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetVisible(new Position(x, y), true);
            }
        }

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Save should succeed for all-true flags");
        var restored = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(restored, "Saved world should load again");

        for (var y = 0; y < restored!.Height; y++)
        {
            for (var x = 0; x < restored.Width; x++)
            {
                Expect.True(restored.IsExplored(new Position(x, y)), "All cells should remain explored");
                Expect.True(restored.IsVisible(new Position(x, y)), "All cells should remain visible");
            }
        }
    }

    private static void SeededIdsAreDeterministic()
    {
        var first = EntityId.NewSeeded(new Random(42));
        var second = EntityId.NewSeeded(new Random(42));
        Expect.Equal(first, second, "Seeded EntityIds should be deterministic");
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
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-serialization-tests", Guid.NewGuid().ToString("N"));
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