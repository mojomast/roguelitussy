using System;
using System.IO;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class ValidationTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.SaveManager rejects corrupted payloads", RejectsCorruptedPayloads);
        registry.Add("Persistence.SaveManager rejects invalid world data", RejectsInvalidWorldData);
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