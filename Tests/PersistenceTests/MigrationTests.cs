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
        registry.Add("Persistence.SaveMigrator migrates v7 saves into v8 floor payloads", MigratesV7SaveToV8FloorPayload);
        registry.Add("Persistence.SaveMigrator migrates v8 saves into current version", MigratesV8SaveToCurrent);
        registry.Add("Persistence.SaveMigrator migrates v9 saves into v10 with default character options", MigratesV9SaveToV10CharacterOptions);
        registry.Add("Persistence.SaveMigrator migrates v10 saves into v12 with trap entities", MigratesV10SaveToV12TrapEntities);
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
        Expect.Equal(SaveSerializer.CurrentVersion, metadata!.Version, "Migrated save should report the new version");
        Expect.True(metadata.ContentVersion is null, "Migrated v2 saves should report unknown content version.");
        Expect.True(metadata.ContentHash is null, "Migrated v2 saves should report unknown content hash.");
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

    private static void MigratesV7SaveToV8FloorPayload()
    {
        using var sandbox = MigrationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)), Version7SaveJson());

        var run = manager.LoadRun(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(run, "Version 7 save should migrate and load as a run snapshot");
        Expect.Equal(5, run!.CurrentFloor, "Migrated v7 save should preserve active floor depth");
        Expect.True(run.Floors.ContainsKey(5), "Migrated v7 save should create a single active floor payload");
        Expect.Equal("V7 Hero", run.ActiveWorld.Player.Name, "Migrated v7 save should restore the player");

        var metadata = manager.GetSaveMetadata(SaveSlots.Slot1);
        Expect.NotNull(metadata, "Migrated v7 save should expose metadata");
        Expect.Equal(SaveSerializer.CurrentVersion, metadata!.Version, "Migrated v7 save should report the current normalized version");
        Expect.True(metadata.ContentVersion is null, "Migrated v7 saves should report unknown content version.");
        Expect.True(metadata.ContentHash is null, "Migrated v7 saves should report unknown content hash.");
    }

    private static string Version7SaveJson() => """
{
  "version": 7,
  "savedAt": "2026-06-10T10:15:00Z",
  "seed": 123,
  "depth": 5,
  "turnNumber": 77,
  "combatRandomState": 99,
  "itemRandomState": 100,
  "width": 3,
  "height": 3,
  "tiles": "AgICAgICAgIC",
  "explored": "BwA=",
  "visible": "AgA=",
  "playerId": "11111111111111111111111111111111",
  "entities": [
    {
      "id": "11111111111111111111111111111111",
      "name": "V7 Hero",
      "position": { "x": 1, "y": 1 },
      "faction": 0,
      "blocksMovement": true,
      "blocksSight": false,
      "stats": { "hp": 10, "maxHP": 10, "attack": 2, "accuracy": 1, "defense": 1, "evasion": 1, "speed": 100, "viewRadius": 8, "energy": 0 }
    }
  ],
  "groundItems": [],
  "openDoors": []
}
""";

    private static void MigratesV8SaveToCurrent()
    {
        using var sandbox = MigrationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)), Version8SaveJson());

        var run = manager.LoadRun(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(run, "Version 8 save should migrate and load as a run snapshot");
        Expect.Equal(3, run!.CurrentFloor, "Migrated v8 save should preserve active floor depth");
        Expect.True(run.Floors.ContainsKey(3), "Migrated v8 save should create a single active floor payload");
        Expect.Equal("V8 Hero", run.ActiveWorld.Player.Name, "Migrated v8 save should restore the player");

        var metadata = manager.GetSaveMetadata(SaveSlots.Slot1);
        Expect.NotNull(metadata, "Migrated v8 save should expose metadata");
        Expect.Equal(SaveSerializer.CurrentVersion, metadata!.Version, "Migrated v8 save should report the current normalized version");
    }

    private static void MigratesV9SaveToV10CharacterOptions()
    {
        using var sandbox = MigrationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)), Version9SaveJson());

        var run = manager.LoadRun(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(run, "Version 9 save should migrate and load as a run snapshot");
        Expect.Equal(4, run!.CurrentFloor, "Migrated v9 save should preserve active floor depth");
        Expect.True(run.Floors.ContainsKey(4), "Migrated v9 save should create a single active floor payload");
        Expect.Equal("V9 Hero", run.ActiveWorld.Player.Name, "Migrated v9 save should restore the player");
        Expect.Equal("Vanguard", run.CharacterOptions.Archetype, "Migrated v9 save should default to Vanguard archetype");
        Expect.Equal("human", run.CharacterOptions.RaceId, "Migrated v9 save should default to human race");

        var metadata = manager.GetSaveMetadata(SaveSlots.Slot1);
        Expect.NotNull(metadata, "Migrated v9 save should expose metadata");
        Expect.Equal(SaveSerializer.CurrentVersion, metadata!.Version, "Migrated v9 save should report the current normalized version");
    }

    private static void MigratesV10SaveToV12TrapEntities()
    {
        using var sandbox = MigrationSandbox.Create();
        var manager = new SaveManager(sandbox.DirectoryPath);
        File.WriteAllText(Path.Combine(sandbox.DirectoryPath, SaveSlots.GetFileName(SaveSlots.Slot1)), Version10SaveJson());

        var run = manager.LoadRun(SaveSlots.Slot1).GetAwaiter().GetResult();
        Expect.NotNull(run, "Version 10 save should migrate and load as a run snapshot");
        Expect.Equal(5, run!.CurrentFloor, "Migrated v10 save should preserve active floor depth");
        Expect.True(run.Floors.ContainsKey(5), "Migrated v10 save should create a single active floor payload");
        Expect.Equal("V10 Hero", run.ActiveWorld.Player.Name, "Migrated v10 save should restore the player");
        Expect.Equal(0, run.ActiveWorld.Entities.Count(entity => entity.GetComponent<TrapComponent>() is not null), "Migrated v10 save should default traps to empty");

        var metadata = manager.GetSaveMetadata(SaveSlots.Slot1);
        Expect.NotNull(metadata, "Migrated v10 save should expose metadata");
        Expect.Equal(SaveSerializer.CurrentVersion, metadata!.Version, "Migrated v10 save should report the current normalized version");
    }

    private static string Version8SaveJson() => """
{
  "version": 8,
  "savedAt": "2026-06-10T10:15:00Z",
  "seed": 123,
  "depth": 3,
  "turnNumber": 42,
  "combatRandomState": 99,
  "itemRandomState": 100,
  "width": 3,
  "height": 3,
  "tiles": "AgICAgICAgIC",
  "explored": "BwA=",
  "visible": "AgA=",
  "playerId": "11111111111111111111111111111111",
  "entities": [
    {
      "id": "11111111111111111111111111111111",
      "name": "V8 Hero",
      "position": { "x": 1, "y": 1 },
      "faction": 0,
      "blocksMovement": true,
      "blocksSight": false,
      "stats": { "hp": 10, "maxHP": 10, "attack": 2, "accuracy": 1, "defense": 1, "evasion": 1, "speed": 100, "viewRadius": 8, "energy": 0 }
    }
  ],
  "groundItems": [],
  "openDoors": []
}
""";

    private static string Version10SaveJson() => """
    {
      "version": 10,
      "savedAt": "2026-06-10T10:15:00Z",
      "seed": 123,
      "depth": 5,
      "turnNumber": 44,
      "combatRandomState": 99,
      "itemRandomState": 100,
      "width": 3,
      "height": 3,
      "tiles": "AgICAgICAgIC",
      "explored": "BwA=",
      "visible": "AgA=",
      "playerId": "11111111111111111111111111111111",
      "entities": [
        {
          "id": "11111111111111111111111111111111",
          "name": "V10 Hero",
          "position": { "x": 1, "y": 1 },
          "faction": 0,
          "blocksMovement": true,
          "blocksSight": false,
          "stats": { "hp": 10, "maxHP": 10, "attack": 2, "accuracy": 1, "defense": 1, "evasion": 1, "speed": 100, "viewRadius": 8, "energy": 0 }
        }
      ],
      "groundItems": [],
      "openDoors": []
    }
    """;

    private static string Version9SaveJson() => """
{
  "version": 9,
  "savedAt": "2026-06-10T10:15:00Z",
  "seed": 123,
  "depth": 4,
  "turnNumber": 43,
  "combatRandomState": 99,
  "itemRandomState": 100,
  "width": 3,
  "height": 3,
  "tiles": "AgICAgICAgIC",
  "explored": "BwA=",
  "visible": "AgA=",
  "playerId": "11111111111111111111111111111111",
  "entities": [
    {
      "id": "11111111111111111111111111111111",
      "name": "V9 Hero",
      "position": { "x": 1, "y": 1 },
      "faction": 0,
      "blocksMovement": true,
      "blocksSight": false,
      "stats": { "hp": 10, "maxHP": 10, "attack": 2, "accuracy": 1, "defense": 1, "evasion": 1, "speed": 100, "viewRadius": 8, "energy": 0 }
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
