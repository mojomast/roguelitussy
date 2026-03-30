using System;
using System.IO;
using Roguelike.Core;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.PersistenceTests;

public sealed class MigrationTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Persistence.SaveMigrator migrates v1 saves into current world state", MigratesLegacyV1Save);
      registry.Add("Persistence.SaveMigrator migrates v2 saves into bit-packed version 3", MigratesV2SaveToV3);
    }

    private static void MigratesLegacyV1Save()
    {
        using var sandbox = MigrationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)), LegacySaveJson());

        var world = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(world, "Legacy save should migrate and load successfully");
        Expect.Equal(17, world!.Seed, "Migrated world should preserve the seed");
        Expect.Equal(2, world.Depth, "Migrated world should preserve the floor depth");
        Expect.Equal(41, world.TurnNumber, "Migrated world should preserve the turn number");
        Expect.Equal("Legacy Hero", world.Player.Name, "Migrated world should restore the player");
        Expect.Equal(2, world.Entities.Count, "Migrated world should restore player and enemy entities");
        Expect.True(world.HasGroundItems(new Position(2, 2)), "Migrated world should restore ground items");
        Expect.Equal(TileType.Door, world.GetTile(new Position(1, 1)), "Migrated world should preserve door tiles");
        Expect.False(world.IsDoorOpen(new Position(1, 1)), "Legacy saves without door state should load with closed doors");
        Expect.Equal(300, world.Player.Stats.Energy, "Migrator should preserve scheduler-backed energy");
    }

    private static string LegacySaveJson() => """
{
  "version": 1,
  "timestamp": "2026-03-30T10:15:00Z",
  "seed": 17,
  "currentFloor": 2,
  "turnNumber": 41,
  "mapWidth": 4,
  "mapHeight": 4,
  "tiles": "AgICAgIDAgICAgICAgICBA==",
  "explored": "AQEBAQEBAQEBAQEBAQEBAA==",
  "player": {
    "id": 1,
    "name": "Legacy Hero",
    "posX": 0,
    "posY": 0,
    "faction": 0,
    "hp": 12,
    "maxHP": 14,
    "attack": 5,
    "defense": 2,
    "speed": 100,
    "viewRadius": 8,
    "inventory": ["rusty_sword"],
    "equippedWeapon": "rusty_sword",
    "maxInventorySize": 3,
    "statusEffects": [
      { "type": 1, "remainingTurns": 2, "stacks": 1 }
    ]
  },
  "entities": [
    {
      "id": 2,
      "name": "Goblin",
      "posX": 2,
      "posY": 1,
      "faction": 1,
      "hp": 6,
      "maxHP": 6,
      "attack": 3,
      "defense": 1,
      "speed": 90,
      "viewRadius": 6,
      "energy": 50
    }
  ],
  "groundItems": [
    { "itemId": "healing_herb", "x": 2, "y": 2 }
  ],
  "schedulerEnergy": {
    "1": 300,
    "2": 50
  }
}
""";

    private static void MigratesV2SaveToV3()
    {
        using var sandbox = MigrationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)), Version2SaveJson());

        var world = manager.LoadGame(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(world, "Version 2 save should migrate and load successfully");
        Expect.True(world!.IsExplored(new Position(0, 0)), "Migrated explored data should survive conversion");
        Expect.True(world.IsVisible(new Position(1, 1)), "Migrated visible data should survive conversion");

        var metadata = manager.GetSaveMetadata(SaveSlots.Slot1);
        Expect.NotNull(metadata, "Migrated save should expose metadata");
        Expect.Equal(3, metadata!.Version, "Migrated save should report the new version");
    }

    private static string Version2SaveJson() => """
{
  "version": 2,
  "savedAt": "2026-03-30T10:15:00Z",
  "seed": 17,
  "depth": 2,
  "turnNumber": 41,
  "width": 4,
  "height": 4,
  "tiles": "AgICAgIDAgICAgICAgICBA==",
  "explored": "AQABAAABAAAAAQAAAAEBAA==",
  "visible": "AAAAAAABAAAAAAAAAAAAAA==",
  "playerId": "11111111111111111111111111111111",
  "entities": [
    {
      "id": "11111111111111111111111111111111",
      "name": "Legacy Hero",
      "position": { "x": 0, "y": 0 },
      "faction": 0,
      "blocksMovement": true,
      "blocksSight": false,
      "stats": {
        "hp": 12,
        "maxHp": 14,
        "attack": 5,
        "accuracy": 0,
        "defense": 2,
        "evasion": 0,
        "speed": 100,
        "viewRadius": 8,
        "energy": 300
      }
    }
  ],
  "groundItems": [],
  "openDoors": []
}
""";

    private sealed class MigrationSandbox : IDisposable
    {
        private MigrationSandbox(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public string DirectoryPath { get; }

        public static MigrationSandbox Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), "godotussy-migration-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new MigrationSandbox(directoryPath);
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