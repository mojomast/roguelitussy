using System;
using System.IO;
using System.Text.Json.Nodes;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class ValidationTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.SaveManager rejects corrupted payloads", RejectsCorruptedPayloads);
        registry.Add("Persistence.SaveManager rejects invalid world data", RejectsInvalidWorldData);
        registry.Add("Persistence.SaveValidator rejects traps on non-trap tiles", RejectsTrapsOnNonTrapTiles);
    }

    private static void RejectsCorruptedPayloads()
    {
        using var sandbox = ValidationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)), "{ not valid json }");

        var world = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.True(world is null, "Corrupted JSON should not load");
        Expect.True(manager.GetSaveMetadata(SaveSlots.Slot1) is null, "Corrupted JSON should not produce metadata");
    }

    private static void RejectsInvalidWorldData()
    {
        using var sandbox = ValidationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot2)), InvalidSaveJson());

        var world = manager.LoadGame(SaveSlots.Slot2).GetAwaiter().GetResult();
        Expect.True(world is null, "Validation errors should prevent loading an invalid save");
        Expect.True(manager.GetSaveMetadata(SaveSlots.Slot2) is null, "Validation errors should also suppress metadata");
    }

    private static void RejectsTrapsOnNonTrapTiles()
    {
        using var sandbox = ValidationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        var world = new WorldState();
        world.InitGrid(5, 5);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.SetTile(new Position(2, 2), TileType.Trap);

        var player = new Entity(
            "Hero",
            new Position(1, 1),
            new Stats { HP = 10, MaxHP = 10, Attack = 2, Accuracy = 1, Defense = 1, Evasion = 1, Speed = 100, ViewRadius = 8, Energy = 0 },
            Faction.Player,
            id: new EntityId(Guid.Parse("11111111111111111111111111111111")));
        world.Player = player;
        world.AddEntity(player);

        var trap = new Entity(
            "spike_trap",
            new Position(2, 2),
            new Stats { HP = 1, MaxHP = 1, Attack = 0, Accuracy = 0, Defense = 0, Evasion = 0, Speed = 0, ViewRadius = 0, Energy = 0 },
            Faction.Neutral,
            blocksMovement: false,
            blocksSight: false);
        trap.SetComponent(new TrapComponent { TemplateId = "spike_trap", IsArmed = true, IsRevealed = false, TriggerCount = 0 });
        world.AddEntity(trap);

        Expect.True(manager.SaveGame(world, SaveSlots.Slot1).GetAwaiter().GetResult(), "Test setup should save a valid world with a trap tile");

        var path = Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var entities = root["floors"]![0]!["entities"]!.AsArray();
        var trapEntity = entities.First(entity => entity!["trap"] is not null)!;
        trapEntity!["position"]!["x"] = 3;
        trapEntity["position"]!["y"] = 3;
        File.WriteAllText(path, root.ToJsonString());

        var loaded = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.True(loaded is null, "Traps on non-trap tiles should fail validation");
    }

    private static string InvalidSaveJson() => """
{
  "version": 2,
  "savedAt": "2026-03-30T10:15:00Z",
  "seed": 9,
  "depth": 1,
  "turnNumber": 2,
  "width": 2,
  "height": 2,
  "tiles": "AgICAw==",
  "explored": "AQEBAQ==",
  "visible": "AQEBAQ==",
  "playerId": "11111111111111111111111111111111",
  "entities": [
    {
      "id": "11111111111111111111111111111111",
      "name": "Player",
      "position": { "x": 0, "y": 0 },
      "faction": 0,
      "blocksMovement": true,
      "blocksSight": false,
      "stats": { "hp": 10, "maxHP": 10, "attack": 2, "accuracy": 1, "defense": 1, "evasion": 1, "speed": 100, "viewRadius": 8, "energy": 0 },
      "inventory": {
        "capacity": 1,
        "items": [
          { "instanceId": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "templateId": "dagger", "currentCharges": 0, "stackCount": 1, "isIdentified": true }
        ],
        "equipped": [
          { "itemId": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "slot": 1, "statModifiers": { "attack": 2 } }
        ]
      }
    },
    {
      "id": "22222222222222222222222222222222",
      "name": "Enemy",
      "position": { "x": 0, "y": 0 },
      "faction": 1,
      "blocksMovement": true,
      "blocksSight": false,
      "stats": { "hp": 8, "maxHP": 8, "attack": 2, "accuracy": 1, "defense": 1, "evasion": 1, "speed": 100, "viewRadius": 6, "energy": 0 }
    }
  ],
  "groundItems": [],
  "openDoors": [
    { "x": 0, "y": 0 }
  ]
}
""";

    private sealed class ValidationSandbox : IDisposable
    {
        private ValidationSandbox(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public string DirectoryPath { get; }

        public static ValidationSandbox Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-validation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new ValidationSandbox(directoryPath);
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
