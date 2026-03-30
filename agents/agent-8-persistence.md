# Agent 8: Persistence Agent — Detailed Specification

## Mission
Implement save/load system: serialize complete game state to JSON files, support multiple save slots, autosave on floor transition, save file versioning, and load validation. Must perfectly restore game state including entity positions, inventories, status effects, scheduler energy, fog-of-war exploration, and ground items.

---

## 1. Files to Create

| File | Purpose |
|------|---------|
| `Core/Persistence/SaveManager.cs` | Main save/load logic implementing `ISaveManager` |
| `Core/Persistence/SaveSerializer.cs` | Serialization/deserialization of game state |
| `Core/Persistence/SaveMigrator.cs` | Version migration for save files |
| `Core/Persistence/SaveValidator.cs` | Load validation and integrity checks |
| `Tests/PersistenceTests/SaveManagerTests.cs` | Save/load tests |
| `Tests/PersistenceTests/MigrationTests.cs` | Version migration tests |
| `Tests/PersistenceTests/ValidationTests.cs` | Validation tests |

---

## 2. Save Format

### File Location
```
user://saves/
├── slot_1.json
├── slot_2.json
├── slot_3.json
└── autosave.json
```

Using Godot's `user://` path (maps to `%APPDATA%/godot/app_userdata/Godotussy Roguelike/` on Windows).

### Save Slot Constants
```csharp
public static class SaveSlots
{
    public const int Slot1 = 1;
    public const int Slot2 = 2;
    public const int Slot3 = 3;
    public const int Autosave = 0;  // slot 0 = autosave
    public const int MaxSlots = 4;  // 0-3
}
```

### Complete Save File Schema

```json
{
  "version": 1,
  "timestamp": "2026-03-30T14:22:00Z",
  "seed": 42,
  "currentFloor": 3,
  "turnNumber": 412,
  "mapWidth": 80,
  "mapHeight": 50,
  "tiles": "base64-encoded-flat-array-of-tile-type-bytes",
  "explored": "base64-encoded-flat-array-of-bools",
  "player": {
    "id": 1,
    "name": "Player",
    "spriteId": "player",
    "posX": 25,
    "posY": 18,
    "faction": 0,
    "isPlayer": true,
    "hp": 75,
    "maxHP": 100,
    "attack": 10,
    "defense": 8,
    "speed": 100,
    "viewRadius": 8,
    "inventory": ["health_potion", "iron_sword", "fire_scroll"],
    "equippedWeapon": "iron_sword",
    "equippedArmor": "leather_mail",
    "maxInventorySize": 20,
    "statusEffects": [
      { "type": 0, "remainingTurns": 3, "tickDamage": 2, "stacks": 1 }
    ],
    "energy": 200
  },
  "entities": [
    {
      "id": 2,
      "name": "Skeleton",
      "spriteId": "skeleton",
      "posX": 30,
      "posY": 22,
      "faction": 1,
      "isPlayer": false,
      "hp": 15,
      "maxHP": 20,
      "attack": 6,
      "defense": 3,
      "speed": 100,
      "viewRadius": 6,
      "inventory": [],
      "equippedWeapon": null,
      "equippedArmor": null,
      "maxInventorySize": 0,
      "statusEffects": [],
      "energy": 800,
      "aiProfileId": "aggressive",
      "enemyTypeId": "skeleton"
    }
  ],
  "groundItems": [
    { "itemId": "health_potion", "x": 15, "y": 10 },
    { "itemId": "gold_ring", "x": 40, "y": 30 }
  ],
  "schedulerEnergy": {
    "1": 200,
    "2": 800
  },
  "stairsDownX": 60,
  "stairsDownY": 35,
  "stairsUpX": 10,
  "stairsUpY": 8
}
```

### What Goes Into Save File (Comprehensive List)

| Data | Source | Required for Restore |
|------|--------|---------------------|
| Save version | Static | Migration support |
| Timestamp | `DateTime.UtcNow` | Display in load menu |
| RNG seed | `GameManager.Seed` | Deterministic replay (optional) |
| Current floor | `GameManager.CurrentFloor` | Floor context |
| Turn number | Turn counter | Stats / ordering |
| Map tiles | `WorldState._tiles[,]` | Full map layout |
| Explored flags | `WorldState._explored[,]` | Fog of war memory |
| Player entity | `EntityData` (full) | Player state |
| All NPC entities | `List<EntityData>` (full) | Enemy state |
| Ground items | `List<(itemId, pos)>` | Items on floor |
| Scheduler energy | `Dict<entityId, energy>` | Turn ordering |
| Stairs positions | `WorldState` | Navigation |

---

## 3. Serialization Approach

### Tile Grid Encoding

2D `TileType[,]` arrays are flattened to 1D byte arrays and Base64-encoded to keep JSON compact:

```csharp
public static class SaveSerializer
{
    public static string EncodeTiles(TileType[,] tiles, int width, int height)
    {
        var bytes = new byte[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bytes[y * width + x] = (byte)tiles[x, y];
        return Convert.ToBase64String(bytes);
    }

    public static TileType[,] DecodeTiles(string base64, int width, int height)
    {
        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length != width * height)
            throw new InvalidDataException($"Tile data size mismatch: expected {width * height}, got {bytes.Length}");

        var tiles = new TileType[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                tiles[x, y] = (TileType)bytes[y * width + x];
        return tiles;
    }

    public static string EncodeExplored(bool[,] explored, int width, int height)
    {
        var bytes = new byte[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bytes[y * width + x] = explored[x, y] ? (byte)1 : (byte)0;
        return Convert.ToBase64String(bytes);
    }

    public static bool[,] DecodeExplored(string base64, int width, int height)
    {
        var bytes = Convert.FromBase64String(base64);
        var explored = new bool[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                explored[x, y] = bytes[y * width + x] == 1;
        return explored;
    }
}
```

### Entity Serialization

Use `System.Text.Json` with a flat DTO that maps 1:1 to `EntityData`:

```csharp
public class EntitySaveData
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string SpriteId { get; set; } = "";
    public int PosX { get; set; }
    public int PosY { get; set; }
    public int Faction { get; set; }
    public bool IsPlayer { get; set; }
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
    public int ViewRadius { get; set; }
    public List<string> Inventory { get; set; } = new();
    public string? EquippedWeapon { get; set; }
    public string? EquippedArmor { get; set; }
    public int MaxInventorySize { get; set; }
    public List<StatusEffectSaveData> StatusEffects { get; set; } = new();
    public int Energy { get; set; }
    public string? AIProfileId { get; set; }
    public string? EnemyTypeId { get; set; }
}

public class StatusEffectSaveData
{
    public int Type { get; set; }
    public int RemainingTurns { get; set; }
    public int TickDamage { get; set; }
    public int Stacks { get; set; }
}

// Conversion methods:
public static EntitySaveData ToSaveData(EntityData entity) => new()
{
    Id = entity.Id,
    Name = entity.Name,
    SpriteId = entity.SpriteId,
    PosX = entity.Position.X,
    PosY = entity.Position.Y,
    Faction = (int)entity.Faction,
    IsPlayer = entity.IsPlayer,
    HP = entity.HP,
    MaxHP = entity.MaxHP,
    Attack = entity.Attack,
    Defense = entity.Defense,
    Speed = entity.Speed,
    ViewRadius = entity.ViewRadius,
    Inventory = new List<string>(entity.Inventory),
    EquippedWeapon = entity.EquippedWeapon,
    EquippedArmor = entity.EquippedArmor,
    MaxInventorySize = entity.MaxInventorySize,
    StatusEffects = entity.StatusEffects.Select(e => new StatusEffectSaveData
    {
        Type = (int)e.Type,
        RemainingTurns = e.RemainingTurns,
        TickDamage = e.TickDamage,
        Stacks = e.Stacks
    }).ToList(),
    Energy = entity.Energy,
    AIProfileId = entity.AIProfileId,
    EnemyTypeId = entity.EnemyTypeId
};

public static EntityData FromSaveData(EntitySaveData save) => new()
{
    Id = save.Id,
    Name = save.Name,
    SpriteId = save.SpriteId,
    Position = new Vec2I(save.PosX, save.PosY),
    Faction = (Faction)save.Faction,
    IsPlayer = save.IsPlayer,
    HP = save.HP,
    MaxHP = save.MaxHP,
    Attack = save.Attack,
    Defense = save.Defense,
    Speed = save.Speed,
    ViewRadius = save.ViewRadius,
    Inventory = new List<string>(save.Inventory),
    EquippedWeapon = save.EquippedWeapon,
    EquippedArmor = save.EquippedArmor,
    MaxInventorySize = save.MaxInventorySize,
    StatusEffects = save.StatusEffects.Select(e => new ActiveStatusEffect
    {
        Type = (StatusType)e.Type,
        RemainingTurns = e.RemainingTurns,
        TickDamage = e.TickDamage,
        Stacks = e.Stacks
    }).ToList(),
    Energy = save.Energy,
    AIProfileId = save.AIProfileId,
    EnemyTypeId = save.EnemyTypeId
};
```

---

## 4. SaveManager Implementation

```csharp
public class SaveManager : ISaveManager
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string GetSavePath(int slot)
    {
        var dir = OS.GetUserDataDir() + "/saves";
        if (!DirAccess.DirExistsAbsolute(dir))
            DirAccess.MakeDirRecursiveAbsolute(dir);

        return slot == 0
            ? dir + "/autosave.json"
            : dir + $"/slot_{slot}.json";
    }

    public bool Save(int slot, IWorldState world, ITurnScheduler scheduler, int floor, int seed)
    {
        try
        {
            var payload = BuildPayload(world, scheduler, floor, seed);
            var json = JsonSerializer.Serialize(payload, JsonOpts);
            var path = GetSavePath(slot);
            System.IO.File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Save failed: {ex.Message}");
            return false;
        }
    }

    public (IWorldState world, int floor, int seed)? Load(int slot)
    {
        try
        {
            var path = GetSavePath(slot);
            if (!System.IO.File.Exists(path)) return null;

            var json = System.IO.File.ReadAllText(path);
            var payload = JsonSerializer.Deserialize<SaveFileData>(json, JsonOpts);
            if (payload == null) return null;

            // Validate
            var errors = SaveValidator.Validate(payload);
            if (errors.Count > 0)
            {
                foreach (var err in errors) GD.PrintErr($"Save validation: {err}");
                return null;
            }

            // Migrate if needed
            if (payload.Version < CurrentVersion)
                payload = SaveMigrator.Migrate(payload, CurrentVersion);

            // Reconstruct WorldState
            var world = RestoreWorldState(payload);
            return (world, payload.CurrentFloor, payload.Seed);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Load failed: {ex.Message}");
            return null;
        }
    }

    public bool SlotExists(int slot)
    {
        return System.IO.File.Exists(GetSavePath(slot));
    }

    public void DeleteSlot(int slot)
    {
        var path = GetSavePath(slot);
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    public SavePayload? ReadSlotMetadata(int slot)
    {
        // Quick read for save slot picker (timestamp, floor, player HP)
        try
        {
            var path = GetSavePath(slot);
            if (!System.IO.File.Exists(path)) return null;
            var json = System.IO.File.ReadAllText(path);
            return JsonSerializer.Deserialize<SavePayload>(json, JsonOpts);
        }
        catch { return null; }
    }

    private SaveFileData BuildPayload(IWorldState world, ITurnScheduler scheduler, int floor, int seed)
    {
        var player = world.GetAllEntities().First(e => e.IsPlayer);
        var npcs = world.GetAllEntities().Where(e => !e.IsPlayer).ToList();

        return new SaveFileData
        {
            Version = CurrentVersion,
            Timestamp = DateTime.UtcNow,
            Seed = seed,
            CurrentFloor = floor,
            MapWidth = world.Width,
            MapHeight = world.Height,
            // Tiles and explored encoded as base64
            Player = SaveSerializer.ToSaveData(player),
            Entities = npcs.Select(SaveSerializer.ToSaveData).ToList(),
            GroundItems = world.GetGroundItems()
                .Select(gi => new GroundItemSaveData { ItemId = gi.itemId, X = gi.pos.X, Y = gi.pos.Y })
                .ToList(),
            StairsDownX = world.StairsDownPos.X,
            StairsDownY = world.StairsDownPos.Y,
            StairsUpX = world.StairsUpPos.X,
            StairsUpY = world.StairsUpPos.Y,
        };
        // Note: Tiles and Explored encoding happens inside the serializer
    }
}
```

---

## 5. Save File Versioning Strategy

### Version Field
Every save file has a `"version": N` integer at the top level.

### Migration Rules

| From → To | Changes | Migration Logic |
|-----------|---------|-----------------|
| 1 → 2 | (example) Add `xpPoints` field | Set `xpPoints = 0` for all entities |
| 2 → 3 | (example) Rename `hp` to `hitPoints` | Rename field in JSON |

### SaveMigrator.cs

```csharp
public static class SaveMigrator
{
    public static SaveFileData Migrate(SaveFileData data, int targetVersion)
    {
        while (data.Version < targetVersion)
        {
            data = data.Version switch
            {
                // 1 => MigrateV1ToV2(data),
                // 2 => MigrateV2ToV3(data),
                _ => throw new InvalidOperationException($"No migration path from version {data.Version}")
            };
        }
        return data;
    }

    // Example migration:
    // private static SaveFileData MigrateV1ToV2(SaveFileData data)
    // {
    //     data.Version = 2;
    //     // Add new fields with defaults
    //     return data;
    // }
}
```

### Backward Compatibility
- Never delete fields, only add or rename.
- Unknown fields in JSON are silently ignored by `System.Text.Json` (safe for forward compat).
- If version > `CurrentVersion`, refuse to load with error message.

---

## 6. Autosave on Floor Transition

```csharp
// In GameManager.TransitionFloor():
public void TransitionFloor(int newFloor)
{
    // ... generate/load new floor ...

    // Autosave
    SaveMgr?.Save(SaveSlots.Autosave, World!, Scheduler!, newFloor, Seed);

    var eventBus = GetNode<EventBus>("/root/EventBus");
    eventBus.EmitSignal(EventBus.SignalName.SaveCompleted, true);
}
```

### Autosave Rules
- Triggers AFTER the new floor is fully generated and player is placed.
- Slot 0 is always autosave — overwrites without confirmation.
- Manual saves to slots 1-3 require user confirmation if slot is occupied (handled by UI Agent).

---

## 7. Load Validation

```csharp
public static class SaveValidator
{
    public static List<string> Validate(SaveFileData data)
    {
        var errors = new List<string>();

        // Version check
        if (data.Version < 1)
            errors.Add("Invalid save version");
        if (data.Version > 1) // CurrentVersion
            errors.Add($"Save version {data.Version} is newer than supported");

        // Map dimensions
        if (data.MapWidth <= 0 || data.MapHeight <= 0)
            errors.Add("Invalid map dimensions");
        if (data.MapWidth > 200 || data.MapHeight > 200)
            errors.Add("Map dimensions exceed maximum (200x200)");

        // Player exists
        if (data.Player == null)
            errors.Add("No player data");
        else
        {
            if (data.Player.HP <= 0)
                errors.Add("Player HP is zero or negative");
            if (data.Player.MaxHP <= 0)
                errors.Add("Player MaxHP is zero or negative");
            if (!data.Player.IsPlayer)
                errors.Add("Player entity not marked as player");
        }

        // Entity positions in bounds
        foreach (var entity in data.Entities ?? new())
        {
            if (entity.PosX < 0 || entity.PosX >= data.MapWidth ||
                entity.PosY < 0 || entity.PosY >= data.MapHeight)
                errors.Add($"Entity {entity.Id} ({entity.Name}) position out of bounds");

            if (entity.HP <= 0)
                errors.Add($"Entity {entity.Id} has non-positive HP (dead entities shouldn't be saved)");
        }

        // No duplicate entity IDs
        var ids = new HashSet<int>();
        if (data.Player != null) ids.Add(data.Player.Id);
        foreach (var e in data.Entities ?? new())
        {
            if (!ids.Add(e.Id))
                errors.Add($"Duplicate entity ID: {e.Id}");
        }

        // Floor range
        if (data.CurrentFloor < 1)
            errors.Add("Invalid floor number");

        // Ground items have valid positions
        foreach (var item in data.GroundItems ?? new())
        {
            if (item.X < 0 || item.X >= data.MapWidth || item.Y < 0 || item.Y >= data.MapHeight)
                errors.Add($"Ground item {item.ItemId} position out of bounds");
        }

        // Tile data present (if using base64, check length)
        // Explored data present

        return errors;
    }
}
```

---

## 8. Test Scenarios (12)

| # | Test Scenario | Expected |
|---|---------------|----------|
| P1 | Save game state to slot 1 | JSON file created at correct path |
| P2 | Load game from slot 1 | WorldState matches pre-save state exactly |
| P3 | Player position preserved | After load, player at same coordinates |
| P4 | Player inventory preserved | After load, same items in inventory |
| P5 | Player status effects preserved | After load, same effects with same durations |
| P6 | Enemy positions preserved | After load, all enemies at same positions |
| P7 | Ground items preserved | After load, items on ground at same positions |
| P8 | Explored fog preserved | After load, explored tiles match |
| P9 | Scheduler energy preserved | After load, entity energies match |
| P10 | Autosave on floor transition | Autosave file created when changing floors |
| P11 | Load nonexistent slot | Returns null, no crash |
| P12 | Load corrupted JSON | Returns null with validation errors, no crash |
| P13 | Save version mismatch (future version) | Refuses to load with error |
| P14 | Delete save slot | File removed from disk |
| P15 | Slot existence check | SlotExists returns correct bool |

### Round-Trip Test Pattern

```csharp
[Test]
public void SaveLoad_RoundTrip_PreservesState()
{
    // Arrange: Create a world with known state
    var world = new WorldState(20, 15);
    // ... set tiles, spawn entities, place items ...

    var scheduler = new TurnScheduler();
    scheduler.RegisterActor(1, 100);
    scheduler.RegisterActor(2, 150);

    var manager = new SaveManager();

    // Act
    manager.Save(1, world, scheduler, 3, 42);
    var result = manager.Load(1);

    // Assert
    Assert.NotNull(result);
    var (loadedWorld, floor, seed) = result.Value;
    Assert.Equal(3, floor);
    Assert.Equal(42, seed);
    Assert.Equal(world.Width, loadedWorld.Width);
    Assert.Equal(world.Height, loadedWorld.Height);
    // ... Compare every tile, entity, item ...
}
```

---

## 9. Dependencies

| Dependency | Provider | Notes |
|------------|----------|-------|
| `IWorldState` | Agent 2 | Serialize/deserialize world |
| `ITurnScheduler` | Agent 2 | Save/restore energy values |
| `EntityData`, `SavePayload` DTOs | Agent 1 | Data structures |
| `GameManager` | Agent 1 | Trigger autosave, provide seed/floor |
| `EventBus` | Agent 1 | SaveCompleted/LoadCompleted signals |
| Content IDs | Agent 9 | Item/enemy IDs referenced in saves must be valid |

**Note**: Save files reference content by string ID. If content is removed between versions, loaded saves may reference missing content — handle gracefully (skip unknown items, log warning).
