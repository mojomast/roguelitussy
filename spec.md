# Roguelike Engine â€” Complete One-Shot Parallel Build Spec

## 1. Executive Summary

This document is the **complete, self-contained build spec** for a deterministic roguelike engine built on **Godot 4.4+ with a pure C# simulation core**. Nine parallel subagents build subsystems independently against frozen shared contracts (C# interfaces + Godot signals). Each agent has a detailed spec in `agents/agent-N-*.md`.

**Core design pillars:**

- **Deterministic**: Same seed = same game. All randomness flows from a seeded `System.Random`. No `DateTime.Now`, no `Guid.NewGuid()` in simulation paths.
- **Energy-based turn system**: Speed-100 entity gains 1000 energy/round. Energy threshold to act = 1000. Variable speed enables haste, slow, and multi-action turns.
- **Symmetric shadowcasting FOV**: Industry-standard 2D roguelike visibility. Tiles lit for the viewer are also lit from the tile's perspective.
- **BSP dungeon generation**: Binary Space Partition room placement + corridor stitching. Deterministic given seed + depth.
- **Utility AI**: Enemies evaluate scored actions (attack, chase, flee, patrol) rather than hard-coded state machines. Stateless between turns â€” all memory stored as entity components.
- **Data-driven content**: Items, enemies, abilities, loot tables defined in JSON. Zero hardcoded stats.
- **Parallel-safe architecture**: Frozen contracts â†’ 9 agents build simultaneously â†’ integration pass.

---

## 2. Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| **Engine** | Godot 4.4 (.NET/C#) | Rendering, input, UI, audio, scene management |
| **Simulation core** | Pure C# (no Godot deps) | WorldState, actions, combat, turns, status effects. Independently testable. |
| **Content** | JSON schemas | Loaded at runtime by `ContentDatabase`. Hot-reloadable in debug. |
| **Rendering** | Godot `TileMapLayer` + `Sprite2D` | 16Ã—16 pixel tiles. 4 layers: floor, walls, objects, fog. |
| **UI** | Godot `Control` nodes | HUD, inventory, combat log, character sheet, menus. |
| **Persistence** | JSON save files | `user://saves/save_{slot}.json`. Versioned format with migration support. |
| **Testing** | xUnit or NUnit | Pure C# tests for simulation, generation, AI, persistence, content. |
| **Signals** | Godot C# signals via `EventBus` autoload | All cross-system communication. No direct node references between agents. |

---

## 3. Project Structure

```
godotussy/
â”œâ”€â”€ project.godot                              # Godot project config (autoloads, input map, display)
â”œâ”€â”€ godotussy.csproj                           # C# project file
â”œâ”€â”€ godotussy.sln                              # Solution file
â”œâ”€â”€ .editorconfig                              # Code style (4-space indent, LF, UTF-8)
â”œâ”€â”€ .gitignore                                 # .godot/, bin/, obj/, *.tmp
â”‚
â”œâ”€â”€ Core/                                      # Pure C# â€” no Godot dependencies
â”‚   â”œâ”€â”€ Contracts/                             # Shared interfaces, DTOs, enums (Architecture owns, all read)
â”‚   â”‚   â”œâ”€â”€ Types/
â”‚   â”‚   â”‚   â”œâ”€â”€ Position.cs                    # Immutable grid coordinate
â”‚   â”‚   â”‚   â”œâ”€â”€ EntityId.cs                    # Strongly-typed GUID wrapper
â”‚   â”‚   â”‚   â”œâ”€â”€ Enums.cs                       # TileType, ActionType, ActionResult, DamageType, Faction, etc.
â”‚   â”‚   â”‚   â”œâ”€â”€ Stats.cs                       # Mutable stat block (HP, ATK, DEF, Speed, Energy)
â”‚   â”‚   â”‚   â”œâ”€â”€ DamageResult.cs                # Immutable damage calculation result
â”‚   â”‚   â”‚   â”œâ”€â”€ CombatEvent.cs                 # Full combat exchange record
â”‚   â”‚   â”‚   â”œâ”€â”€ ItemTemplate.cs                # Static item definition from JSON
â”‚   â”‚   â”‚   â”œâ”€â”€ ItemInstance.cs                # Runtime item with charges/stack/identified state
â”‚   â”‚   â”‚   â”œâ”€â”€ EnemyTemplate.cs               # Static enemy definition from JSON
â”‚   â”‚   â”‚   â”œâ”€â”€ StatusEffectInstance.cs         # Active status effect with duration
â”‚   â”‚   â”‚   â”œâ”€â”€ LevelData.cs                   # Generated level metadata (spawns, rooms)
â”‚   â”‚   â”‚   â”œâ”€â”€ RoomData.cs                    # Room rectangle for generation/minimap
â”‚   â”‚   â”‚   â””â”€â”€ SaveMetadata.cs                # Lightweight save slot info for UI
â”‚   â”‚   â”œâ”€â”€ IEntity.cs                         # Entity interface (id, pos, stats, components)
â”‚   â”‚   â”œâ”€â”€ IAction.cs                         # Action interface + ActionOutcome
â”‚   â”‚   â”œâ”€â”€ IWorldState.cs                     # Read-only world view
â”‚   â”‚   â”œâ”€â”€ WorldState.cs                      # Mutable world implementation
â”‚   â”‚   â”œâ”€â”€ ITurnScheduler.cs                  # Energy-based turn scheduling
â”‚   â”‚   â”œâ”€â”€ IGenerator.cs                      # Dungeon generator contract
â”‚   â”‚   â”œâ”€â”€ IPathfinder.cs                     # A* pathfinding contract
â”‚   â”‚   â”œâ”€â”€ IBrain.cs                          # AI decision-maker contract
â”‚   â”‚   â”œâ”€â”€ ISaveManager.cs                    # Save/load contract
â”‚   â”‚   â”œâ”€â”€ IFOV.cs                            # Field-of-view contract
â”‚   â”‚   â””â”€â”€ IContentDatabase.cs               # Content data access contract
â”‚   â”‚
â”‚   â”œâ”€â”€ Simulation/                            # Agent 2
â”‚   â”‚   â”œâ”€â”€ Entity.cs                          # IEntity implementation with component dict
â”‚   â”‚   â”œâ”€â”€ GameLoop.cs                        # Master turn loop (begin round â†’ actors â†’ end round)
â”‚   â”‚   â”œâ”€â”€ TurnScheduler.cs                   # ITurnScheduler implementation
â”‚   â”‚   â”œâ”€â”€ CombatResolver.cs                  # Hit/miss/crit/damage formulas
â”‚   â”‚   â”œâ”€â”€ StatusEffectProcessor.cs           # Tick, expire, apply status effects
â”‚   â”‚   â”œâ”€â”€ Inventory.cs                       # Entity inventory component
â”‚   â”‚   â””â”€â”€ Actions/
â”‚   â”‚       â”œâ”€â”€ MoveAction.cs                  # Move to adjacent tile
â”‚   â”‚       â”œâ”€â”€ AttackAction.cs                # Melee attack adjacent entity
â”‚   â”‚       â”œâ”€â”€ WaitAction.cs                  # Skip turn (costs energy)
â”‚   â”‚       â”œâ”€â”€ PickupAction.cs                # Pick up item at feet
â”‚   â”‚       â”œâ”€â”€ UseItemAction.cs               # Use consumable from inventory
â”‚   â”‚       â”œâ”€â”€ DropItemAction.cs              # Drop item to ground
â”‚   â”‚       â”œâ”€â”€ DescendAction.cs               # Go down stairs
â”‚   â”‚       â”œâ”€â”€ AscendAction.cs                # Go up stairs
â”‚   â”‚       â”œâ”€â”€ OpenDoorAction.cs              # Open adjacent door
â”‚   â”‚       â””â”€â”€ CloseDoorAction.cs             # Close adjacent door
â”‚   â”‚
â”‚   â”œâ”€â”€ Generation/                            # Agent 4
â”‚   â”‚   â”œâ”€â”€ DungeonGenerator.cs                # IGenerator implementation (BSP)
â”‚   â”‚   â”œâ”€â”€ BSPNode.cs                         # Binary space partition tree node
â”‚   â”‚   â”œâ”€â”€ RoomPlacer.cs                      # Place rooms within BSP leaves
â”‚   â”‚   â”œâ”€â”€ CorridorBuilder.cs                 # Connect rooms with L-shaped corridors
â”‚   â”‚   â”œâ”€â”€ LevelValidator.cs                  # Validate connectivity, room count, reachability
â”‚   â”‚   â””â”€â”€ Prefabs/
â”‚   â”‚       â””â”€â”€ RoomPrefabLoader.cs            # Load room templates from JSON
â”‚   â”‚
â”‚   â”œâ”€â”€ AI/                                    # Agent 5
â”‚   â”‚   â”œâ”€â”€ Pathfinder.cs                      # IPathfinder implementation (A*)
â”‚   â”‚   â”œâ”€â”€ BrainFactory.cs                    # Map brain type strings â†’ IBrain instances
â”‚   â”‚   â””â”€â”€ Brains/
â”‚   â”‚       â”œâ”€â”€ MeleeRusherBrain.cs            # Chase + melee attack
â”‚   â”‚       â”œâ”€â”€ RangedKiterBrain.cs            # Keep distance + ranged attack
â”‚   â”‚       â”œâ”€â”€ PatrolGuardBrain.cs            # Patrol route, chase on sight
â”‚   â”‚       â””â”€â”€ FleeingBrain.cs               # Run when low HP
â”‚   â”‚
â”‚   â”œâ”€â”€ Persistence/                           # Agent 8
â”‚   â”‚   â”œâ”€â”€ SaveManager.cs                     # ISaveManager implementation
â”‚   â”‚   â”œâ”€â”€ WorldStateSerializer.cs            # Serialize/deserialize WorldState
â”‚   â”‚   â”œâ”€â”€ EntitySerializer.cs                # Serialize/deserialize entities + components
â”‚   â”‚   â””â”€â”€ SaveMigrator.cs                    # Version migration for save format changes
â”‚   â”‚
â”‚   â””â”€â”€ Content/                               # Agent 9
â”‚       â”œâ”€â”€ ContentLoader.cs                   # IContentDatabase implementation, JSON parsing
â”‚       â”œâ”€â”€ LootTableResolver.cs               # Weighted random item selection
â”‚       â””â”€â”€ DifficultyScaler.cs                # Scale stats/spawns by dungeon depth
â”‚
â”œâ”€â”€ Scripts/                                   # Godot-dependent C# scripts
â”‚   â”œâ”€â”€ Autoloads/
â”‚   â”‚   â”œâ”€â”€ GameManager.cs                     # Master game loop, ties simulation to Godot
â”‚   â”‚   â”œâ”€â”€ EventBus.cs                        # Global signal hub (all cross-system events)
â”‚   â”‚   â””â”€â”€ ContentDatabase.cs                 # Godot-side content wrapper (loads from res://)
â”‚   â”‚
â”‚   â”œâ”€â”€ World/                                 # Agent 3 â€” Rendering
â”‚   â”‚   â”œâ”€â”€ WorldView.cs                       # Master rendering controller
â”‚   â”‚   â”œâ”€â”€ FOVCalculator.cs                   # Symmetric shadowcasting (IFOV impl)
â”‚   â”‚   â”œâ”€â”€ EntityRenderer.cs                  # Sprite pool for entities
â”‚   â”‚   â”œâ”€â”€ AnimationController.cs             # Tween-based move/attack/death animations
â”‚   â”‚   â”œâ”€â”€ CameraController.cs               # Follow player, clamp to map bounds
â”‚   â”‚   â””â”€â”€ DamagePopup.cs                     # Floating damage number
â”‚   â”‚
â”‚   â”œâ”€â”€ UI/                                    # Agent 6
â”‚   â”‚   â”œâ”€â”€ HUD.cs                             # HP bar, energy, depth, turn counter
â”‚   â”‚   â”œâ”€â”€ InventoryUI.cs                     # Grid inventory panel
â”‚   â”‚   â”œâ”€â”€ CombatLog.cs                       # Scrolling message log
â”‚   â”‚   â”œâ”€â”€ CharacterSheet.cs                  # Full stat display
â”‚   â”‚   â”œâ”€â”€ MainMenu.cs                        # Title screen, new game, load, quit
â”‚   â”‚   â”œâ”€â”€ PauseMenu.cs                       # Pause overlay with save/load/quit
â”‚   â”‚   â”œâ”€â”€ GameOverScreen.cs                  # Death screen with stats
â”‚   â”‚   â”œâ”€â”€ Minimap.cs                         # Top-right minimap overlay
â”‚   â”‚   â””â”€â”€ Tooltip.cs                         # Hover tooltip for items/enemies
â”‚   â”‚
â”‚   â””â”€â”€ Tools/                                 # Agent 7
â”‚       â”œâ”€â”€ DebugConsole.cs                    # In-game command console
â”‚       â”œâ”€â”€ DebugOverlay.cs                    # FPS, entity count, pathfinding viz
â”‚       â”œâ”€â”€ MapEditorPlugin.cs                 # Godot editor plugin for map painting
â”‚       â””â”€â”€ ItemEditor.cs                      # Godot editor plugin for item JSON editing
â”‚
â”œâ”€â”€ Scenes/
â”‚   â”œâ”€â”€ Main.tscn                              # Root scene (GameManager autoload entry)
â”‚   â”œâ”€â”€ World/
â”‚   â”‚   â”œâ”€â”€ WorldView.tscn                     # TileMapLayers + EntityLayer + Camera2D
â”‚   â”‚   â””â”€â”€ EntitySprite.tscn                  # Reusable Sprite2D for entities
â”‚   â”œâ”€â”€ UI/
â”‚   â”‚   â”œâ”€â”€ HUD.tscn                           # HUD layout
â”‚   â”‚   â”œâ”€â”€ InventoryUI.tscn                   # Inventory panel
â”‚   â”‚   â”œâ”€â”€ CombatLog.tscn                     # Combat log panel
â”‚   â”‚   â”œâ”€â”€ CharacterSheet.tscn                # Character sheet panel
â”‚   â”‚   â”œâ”€â”€ MainMenu.tscn                      # Main menu scene
â”‚   â”‚   â”œâ”€â”€ PauseMenu.tscn                     # Pause menu overlay
â”‚   â”‚   â”œâ”€â”€ GameOverScreen.tscn                # Game over screen
â”‚   â”‚   â”œâ”€â”€ Minimap.tscn                       # Minimap widget
â”‚   â”‚   â”œâ”€â”€ Tooltip.tscn                       # Tooltip popup
â”‚   â”‚   â””â”€â”€ DamagePopup.tscn                   # Floating damage number
â”‚   â””â”€â”€ Tools/
â”‚       â”œâ”€â”€ DebugConsole.tscn                  # Debug console overlay
â”‚       â””â”€â”€ DebugOverlay.tscn                  # Debug stats overlay
â”‚
â”œâ”€â”€ Content/
â”‚   â”œâ”€â”€ items.json                             # All item templates
â”‚   â”œâ”€â”€ enemies.json                           # All enemy templates
â”‚   â”œâ”€â”€ abilities.json                         # Ability definitions (future)
â”‚   â”œâ”€â”€ status_effects.json                    # Status effect definitions
â”‚   â”œâ”€â”€ room_prefabs.json                      # Hand-designed room templates
â”‚   â””â”€â”€ loot_tables.json                       # Weighted loot drop tables
â”‚
â”œâ”€â”€ Assets/
â”‚   â”œâ”€â”€ Tilesets/
â”‚   â”‚   â””â”€â”€ dungeon_tileset.tres               # TileSet resource (16Ã—16 tiles)
â”‚   â”œâ”€â”€ Sprites/
â”‚   â”‚   â”œâ”€â”€ player.png                         # Player sprite (16Ã—16)
â”‚   â”‚   â”œâ”€â”€ enemies/                           # Enemy sprites
â”‚   â”‚   â”‚   â”œâ”€â”€ goblin.png
â”‚   â”‚   â”‚   â”œâ”€â”€ skeleton.png
â”‚   â”‚   â”‚   â”œâ”€â”€ orc.png
â”‚   â”‚   â”‚   â”œâ”€â”€ slime.png
â”‚   â”‚   â”‚   â”œâ”€â”€ bat.png
â”‚   â”‚   â”‚   â”œâ”€â”€ spider.png
â”‚   â”‚   â”‚   â”œâ”€â”€ wraith.png
â”‚   â”‚   â”‚   â”œâ”€â”€ troll.png
â”‚   â”‚   â”‚   â”œâ”€â”€ necromancer.png
â”‚   â”‚   â”‚   â””â”€â”€ dragon.png
â”‚   â”‚   â””â”€â”€ items/
â”‚   â”‚       â”œâ”€â”€ potion_red.png
â”‚   â”‚       â”œâ”€â”€ potion_blue.png
â”‚   â”‚       â”œâ”€â”€ potion_green.png
â”‚   â”‚       â”œâ”€â”€ sword.png
â”‚   â”‚       â”œâ”€â”€ shield.png
â”‚   â”‚       â”œâ”€â”€ scroll.png
â”‚   â”‚       â”œâ”€â”€ ring.png
â”‚   â”‚       â”œâ”€â”€ armor.png
â”‚   â”‚       â”œâ”€â”€ bow.png
â”‚   â”‚       â””â”€â”€ key.png
â”‚   â”œâ”€â”€ Fonts/
â”‚   â”‚   â””â”€â”€ default_font.tres                  # UI font resource
â”‚   â””â”€â”€ Audio/
â”‚       â”œâ”€â”€ sfx/
â”‚       â”‚   â”œâ”€â”€ hit.wav
â”‚       â”‚   â”œâ”€â”€ miss.wav
â”‚       â”‚   â”œâ”€â”€ death.wav
â”‚       â”‚   â”œâ”€â”€ pickup.wav
â”‚       â”‚   â”œâ”€â”€ stairs.wav
â”‚       â”‚   â”œâ”€â”€ door.wav
â”‚       â”‚   â””â”€â”€ potion.wav
â”‚       â””â”€â”€ music/
â”‚           â”œâ”€â”€ dungeon_ambient.ogg
â”‚           â””â”€â”€ menu_theme.ogg
â”‚
â”œâ”€â”€ Addons/
â”‚   â””â”€â”€ roguelike_tools/
â”‚       â”œâ”€â”€ plugin.cfg                         # Godot editor plugin manifest
â”‚       â””â”€â”€ RoguelikeToolsPlugin.cs            # Plugin entry point
â”‚
â””â”€â”€ Tests/
    â”œâ”€â”€ Stubs/
    â”‚   â”œâ”€â”€ StubWorldState.cs                  # Minimal WorldState for testing
    â”‚   â”œâ”€â”€ StubEntity.cs                      # Minimal IEntity for testing
    â”‚   â”œâ”€â”€ StubBrain.cs                       # Always-wait brain
    â”‚   â”œâ”€â”€ StubGenerator.cs                   # 10Ã—10 open room generator
    â”‚   â”œâ”€â”€ StubPathfinder.cs                  # Direct-line pathfinder
    â”‚   â”œâ”€â”€ StubContentDatabase.cs             # In-memory content
    â”‚   â””â”€â”€ StubSaveManager.cs                 # In-memory save/load
    â”œâ”€â”€ SimulationTests/
    â”‚   â”œâ”€â”€ WorldStateTests.cs                 # Grid, entity add/remove, spatial queries
    â”‚   â”œâ”€â”€ TurnSchedulerTests.cs              # Energy accumulation, ordering, speed variants
    â”‚   â”œâ”€â”€ CombatResolverTests.cs             # Hit/miss/crit/damage/kill
    â”‚   â”œâ”€â”€ ActionTests.cs                     # Each action type: validate + execute
    â”‚   â””â”€â”€ StatusEffectTests.cs               # Tick, expire, stack, cure
    â”œâ”€â”€ GenerationTests/
    â”‚   â”œâ”€â”€ DungeonGeneratorTests.cs           # Determinism, connectivity, room count
    â”‚   â””â”€â”€ LevelValidatorTests.cs             # Validation error detection
    â”œâ”€â”€ AITests/
    â”‚   â”œâ”€â”€ PathfinderTests.cs                 # A* correctness, unreachable, maxLength
    â”‚   â””â”€â”€ BrainTests.cs                      # Each brain: correct action selection
    â”œâ”€â”€ PersistenceTests/
    â”‚   â”œâ”€â”€ SaveLoadTests.cs                   # Round-trip save â†’ load â†’ compare
    â”‚   â””â”€â”€ MigrationTests.cs                  # Version migration
    â”œâ”€â”€ ContentTests/
    â”‚   â”œâ”€â”€ ContentValidationTests.cs          # All templates valid, no broken refs
    â”‚   â””â”€â”€ BalanceTests.cs                    # Stat sanity checks
    â””â”€â”€ IntegrationTests/
        â”œâ”€â”€ FullGameLoopTests.cs               # Simulate N turns end-to-end
        â””â”€â”€ DeterminismTests.cs                # Same seed â†’ same world state after N turns
```

---

## 4. Shared Contracts â€” C# Interfaces & Types

All contracts live in `Core/Contracts/`. Every agent references these. **No agent modifies these files** â€” they are owned by Architecture (Agent 1) and frozen before parallel work begins. All types use `namespace Roguelike.Core;`.

---

### 4.1 Position

```csharp
// Core/Contracts/Types/Position.cs
using System;

namespace Roguelike.Core;

/// <summary>
/// Immutable grid coordinate. All positions are integer tile coordinates.
/// (0,0) is top-left of the map. X increases right, Y increases down.
/// </summary>
public readonly struct Position : IEquatable<Position>
{
    public readonly int X;
    public readonly int Y;

    public Position(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Manhattan distance (|dx| + |dy|) to another position.</summary>
    public int DistanceTo(Position other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>Chebyshev (king-move) distance: max(|dx|, |dy|).</summary>
    public int ChebyshevTo(Position other) =>
        Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    /// <summary>Create a new position offset by (dx, dy).</summary>
    public Position Offset(int dx, int dy) => new(X + dx, Y + dy);

    public static Position operator +(Position a, Position b) => new(a.X + b.X, a.Y + b.Y);
    public static Position operator -(Position a, Position b) => new(a.X - b.X, a.Y - b.Y);
    public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Position a, Position b) => !(a == b);

    public bool Equals(Position other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Position p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X},{Y})";

    /// <summary>Origin position (0,0).</summary>
    public static readonly Position Zero = new(0, 0);

    /// <summary>Sentinel for "no position" / invalid.</summary>
    public static readonly Position Invalid = new(-1, -1);

    /// <summary>The 4 cardinal directions: N, S, E, W.</summary>
    public static readonly Position[] Cardinals =
    {
        new(0, -1), new(0, 1), new(1, 0), new(-1, 0)
    };

    /// <summary>All 8 directions including diagonals: N, S, E, W, NW, NE, SW, SE.</summary>
    public static readonly Position[] AllDirections =
    {
        new(0, -1), new(0, 1), new(1, 0), new(-1, 0),
        new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)
    };
}
```

---

### 4.2 EntityId

```csharp
// Core/Contracts/Types/EntityId.cs
using System;

namespace Roguelike.Core;

/// <summary>
/// Strongly-typed entity identifier. Wraps a GUID to prevent accidental
/// int/string confusion. Use <see cref="New"/> for runtime spawns,
/// <see cref="From"/> for deserialization.
/// </summary>
public readonly struct EntityId : IEquatable<EntityId>
{
    public readonly Guid Value;

    public EntityId(Guid value) => Value = value;

    /// <summary>Create a new unique entity ID.</summary>
    public static EntityId New() => new(Guid.NewGuid());

    /// <summary>Parse entity ID from string (for deserialization).</summary>
    public static EntityId From(string s) => new(Guid.Parse(s));

    /// <summary>Whether this ID has been assigned (not empty).</summary>
    public bool IsValid => Value != Guid.Empty;

    /// <summary>Sentinel value representing no entity.</summary>
    public static readonly EntityId Invalid = new(Guid.Empty);

    public bool Equals(EntityId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is EntityId id && Equals(id);
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>Short form (first 8 hex chars) for debug display.</summary>
    public override string ToString() => Value.ToString()[..8];

    public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;
    public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;
}
```

---

### 4.3 Enums

```csharp
// Core/Contracts/Types/Enums.cs
namespace Roguelike.Core;

/// <summary>What type of terrain occupies a grid cell.</summary>
public enum TileType : byte
{
    /// <summary>Impassable void â€” out of bounds or ungenerated.</summary>
    Void = 0,
    /// <summary>Solid wall. Blocks movement and line of sight.</summary>
    Wall = 1,
    /// <summary>Open floor. Passable, does not block sight.</summary>
    Floor = 2,
    /// <summary>Door (may be open or closed). Blocks sight when closed.</summary>
    Door = 3,
    /// <summary>Stairs descending to next depth.</summary>
    StairsDown = 4,
    /// <summary>Stairs ascending to previous depth.</summary>
    StairsUp = 5,
    /// <summary>Water. Passable but costs extra movement energy.</summary>
    Water = 6,
}

/// <summary>Classification of actions for animation dispatch, logging, and energy cost lookup.</summary>
public enum ActionType : byte
{
    /// <summary>Skip turn, regain nothing special.</summary>
    Wait,
    /// <summary>Move to an adjacent tile.</summary>
    Move,
    /// <summary>Melee attack an adjacent entity.</summary>
    MeleeAttack,
    /// <summary>Ranged attack a distant entity.</summary>
    RangedAttack,
    /// <summary>Use a consumable item from inventory.</summary>
    UseItem,
    /// <summary>Pick up an item from the ground.</summary>
    PickupItem,
    /// <summary>Drop an item from inventory to the ground.</summary>
    DropItem,
    /// <summary>Open an adjacent closed door.</summary>
    OpenDoor,
    /// <summary>Close an adjacent open door.</summary>
    CloseDoor,
    /// <summary>Descend stairs to next depth.</summary>
    Descend,
    /// <summary>Ascend stairs to previous depth.</summary>
    Ascend,
    /// <summary>Cast an ability (future expansion).</summary>
    CastAbility,
}

/// <summary>Result of attempting an action.</summary>
public enum ActionResult : byte
{
    /// <summary>Action succeeded and was fully applied.</summary>
    Success,
    /// <summary>Action failed validation â€” preconditions not met. No energy spent.</summary>
    Invalid,
    /// <summary>Action was blocked by game rules (e.g., tile occupied). No energy spent.</summary>
    Blocked,
}

/// <summary>Damage element types for resistance/weakness calculations.</summary>
public enum DamageType : byte
{
    Physical,
    Fire,
    Cold,
    Poison,
    Lightning,
    Holy,
    Dark,
}

/// <summary>Entity allegiance for AI targeting decisions.</summary>
public enum Faction : byte
{
    /// <summary>Player and player allies.</summary>
    Player,
    /// <summary>Hostile monsters.</summary>
    Enemy,
    /// <summary>Non-hostile, non-allied NPCs.</summary>
    Neutral,
}

/// <summary>Status effect identifiers. Each maps to specific per-turn behavior.</summary>
public enum StatusEffectType : byte
{
    /// <summary>No effect (sentinel).</summary>
    None,
    /// <summary>Take poison damage each turn.</summary>
    Poisoned,
    /// <summary>Take fire damage each turn, can spread.</summary>
    Burning,
    /// <summary>Movement costs doubled.</summary>
    Frozen,
    /// <summary>Skip turn entirely.</summary>
    Stunned,
    /// <summary>Speed increased by 50%.</summary>
    Hasted,
    /// <summary>Not visible to enemies (breaks on attack).</summary>
    Invisible,
    /// <summary>Heal HP each turn.</summary>
    Regenerating,
    /// <summary>Attack reduced by magnitude.</summary>
    Weakened,
    /// <summary>Absorb next N damage.</summary>
    Shielded,
}

/// <summary>Equipment slot for worn/wielded items.</summary>
public enum EquipSlot : byte
{
    /// <summary>Not equippable.</summary>
    None,
    /// <summary>Primary weapon hand.</summary>
    MainHand,
    /// <summary>Shield or secondary weapon.</summary>
    OffHand,
    /// <summary>Helmet.</summary>
    Head,
    /// <summary>Body armor.</summary>
    Body,
    /// <summary>Boots.</summary>
    Feet,
    /// <summary>Finger ring (max 2).</summary>
    Ring,
    /// <summary>Neck amulet.</summary>
    Amulet,
}

/// <summary>High-level item classification for inventory sorting and UI icons.</summary>
public enum ItemCategory : byte
{
    Weapon,
    Armor,
    Consumable,
    Scroll,
    Key,
    Misc,
}
```

---

### 4.4 Stats

```csharp
// Core/Contracts/Types/Stats.cs
using System;

namespace Roguelike.Core;

/// <summary>
/// Mutable stat block for any combat-capable entity. All combat math reads from this.
/// Base values are set from content data (EnemyTemplate / player defaults);
/// runtime modifiers (equipment, buffs) are applied on top.
/// </summary>
public sealed class Stats
{
    // â”€â”€ Vitals â”€â”€

    /// <summary>Current hit points. Entity dies when this reaches 0.</summary>
    public int HP { get; set; }

    /// <summary>Maximum hit points. Healing cannot exceed this.</summary>
    public int MaxHP { get; set; }

    // â”€â”€ Offense â”€â”€

    /// <summary>
    /// Base melee/ranged attack power. 
    /// Damage formula: Attack + weapon bonus - target Defense (min 1).
    /// </summary>
    public int Attack { get; set; }

    /// <summary>
    /// Hit chance modifier in percent (0â€“100). 
    /// Base hit chance = 80 + Accuracy - target Evasion, clamped to [5, 95].
    /// </summary>
    public int Accuracy { get; set; }

    // â”€â”€ Defense â”€â”€

    /// <summary>Flat damage reduction applied after hit. Damage = max(1, raw - Defense).</summary>
    public int Defense { get; set; }

    /// <summary>Dodge chance modifier in percent. Subtracted from attacker's hit chance.</summary>
    public int Evasion { get; set; }

    // â”€â”€ Turn Economy â”€â”€

    /// <summary>
    /// Speed factor. Normal = 100. Higher = acts more often.
    /// Energy granted per round = Speed Ã— 10.
    /// Speed 100 â†’ 1000 energy/round â†’ 1 standard action.
    /// Speed 150 â†’ 1500 energy/round â†’ sometimes 2 actions.
    /// Speed 50 â†’ 500 energy/round â†’ 1 action every 2 rounds.
    /// </summary>
    public int Speed { get; set; } = 100;

    // â”€â”€ Vision â”€â”€

    /// <summary>FOV radius in tiles. Player default = 8.</summary>
    public int ViewRadius { get; set; } = 8;

    /// <summary>
    /// Current accumulated energy. Gains Speed Ã— 10 each round.
    /// Entity acts when Energy >= 1000 (the energy threshold).
    /// </summary>
    public int Energy { get; set; }

    /// <summary>Is this entity still alive?</summary>
    public bool IsAlive => HP > 0;

    /// <summary>Deep copy of this stat block.</summary>
    public Stats Clone() => (Stats)MemberwiseClone();
}
```

---

### 4.5 DamageResult

```csharp
// Core/Contracts/Types/DamageResult.cs
namespace Roguelike.Core;

/// <summary>
/// Immutable result of a single damage calculation. Returned by CombatResolver,
/// consumed by rendering (floating numbers), logging (combat log), and simulation
/// (apply HP change).
/// </summary>
public sealed record DamageResult(
    /// <summary>The entity dealing damage.</summary>
    EntityId AttackerId,

    /// <summary>The entity receiving damage.</summary>
    EntityId DefenderId,

    /// <summary>Raw damage before defense/resistance reduction.</summary>
    int RawDamage,

    /// <summary>Actual damage applied to defender HP after all reductions.</summary>
    int FinalDamage,

    /// <summary>Element type of the damage dealt.</summary>
    DamageType DamageType,

    /// <summary>Whether the attack was a critical hit (2Ã— base damage before defense).</summary>
    bool IsCritical,

    /// <summary>Whether the attack missed entirely (0 damage).</summary>
    bool IsMiss,

    /// <summary>Whether the defender died from this damage.</summary>
    bool IsKill
);
```

---

### 4.6 CombatEvent

```csharp
// Core/Contracts/Types/CombatEvent.cs
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// Full record of a combat exchange. One action may produce multiple damage results
/// (e.g., cleave hitting 3 enemies) and apply status effects. Consumed by
/// combat log, replay system, and UI.
/// </summary>
public sealed record CombatEvent(
    /// <summary>Turn number when this combat occurred.</summary>
    int TurnNumber,

    /// <summary>Type of action that initiated this combat.</summary>
    ActionType ActionType,

    /// <summary>All damage results from this action (one per hit/target).</summary>
    IReadOnlyList<DamageResult> DamageResults,

    /// <summary>Status effects applied as a result of this combat.</summary>
    IReadOnlyList<StatusEffectInstance> StatusEffectsApplied
);
```

---

### 4.7 StatusEffectInstance

```csharp
// Core/Contracts/Types/StatusEffectInstance.cs
namespace Roguelike.Core;

/// <summary>
/// An active status effect on an entity. Tracks remaining duration and magnitude.
/// Ticked each round by StatusEffectProcessor.
/// </summary>
public sealed record StatusEffectInstance(
    /// <summary>Which status effect this is.</summary>
    StatusEffectType Type,

    /// <summary>
    /// Remaining turns before expiry. -1 = permanent (until cured or dispelled).
    /// Decremented by 1 each round. Removed when reaching 0.
    /// </summary>
    int RemainingTurns,

    /// <summary>
    /// Effect magnitude. Interpretation depends on Type:
    /// - Poisoned: damage per turn
    /// - Burning: damage per turn
    /// - Weakened: attack reduction
    /// - Shielded: damage absorption remaining
    /// - Regenerating: HP healed per turn
    /// </summary>
    int Magnitude
);
```

---

### 4.8 ItemTemplate

```csharp
// Core/Contracts/Types/ItemTemplate.cs
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// Static item definition loaded from content JSON. Immutable after loading.
/// Runtime instances (<see cref="ItemInstance"/>) reference a template by
/// <see cref="TemplateId"/> and carry per-instance state.
/// </summary>
public sealed record ItemTemplate(
    /// <summary>Unique template ID from content JSON (e.g., "sword_iron", "potion_health").</summary>
    string TemplateId,

    /// <summary>Display name shown in inventory and tooltips.</summary>
    string DisplayName,

    /// <summary>Flavor text / description.</summary>
    string Description,

    /// <summary>High-level category for sorting and UI icons.</summary>
    ItemCategory Category,

    /// <summary>Which equipment slot this item occupies. None for non-equippable items.</summary>
    EquipSlot Slot,

    /// <summary>
    /// Stat modifiers applied when equipped. Key = stat property name
    /// (e.g., "Attack", "Defense", "MaxHP"), Value = additive modifier.
    /// </summary>
    IReadOnlyDictionary<string, int> StatModifiers,

    /// <summary>
    /// For consumables: effect identifier to trigger on use (e.g., "heal", "fireball").
    /// Null for non-consumable items.
    /// </summary>
    string? UseEffect,

    /// <summary>
    /// For consumables with limited uses. -1 = single-use (destroyed on use).
    /// 0 = unlimited. Positive = limited charges.
    /// </summary>
    int MaxCharges,

    /// <summary>
    /// Maximum stack size. 1 = not stackable (weapons, armor).
    /// Greater than 1 = stackable (potions, scrolls).
    /// </summary>
    int MaxStack
);
```

---

### 4.9 ItemInstance

```csharp
// Core/Contracts/Types/ItemInstance.cs
namespace Roguelike.Core;

/// <summary>
/// Runtime item instance in an inventory or on the ground. References an
/// <see cref="ItemTemplate"/> by ID and carries mutable per-instance state.
/// </summary>
public sealed class ItemInstance
{
    /// <summary>Unique instance ID for this specific item stack/copy.</summary>
    public EntityId InstanceId { get; init; } = EntityId.New();

    /// <summary>Template ID linking to the immutable <see cref="ItemTemplate"/>.</summary>
    public required string TemplateId { get; init; }

    /// <summary>Remaining charges for rechargeable items. -1 if not applicable.</summary>
    public int CurrentCharges { get; set; }

    /// <summary>Current stack count. Always >= 1 while item exists.</summary>
    public int StackCount { get; set; } = 1;

    /// <summary>
    /// Whether the player has identified this item. Unidentified items show
    /// generic names (e.g., "Blue Potion" instead of "Potion of Healing").
    /// </summary>
    public bool IsIdentified { get; set; }
}
```

---

### 4.10 EnemyTemplate

```csharp
// Core/Contracts/Types/EnemyTemplate.cs
namespace Roguelike.Core;

/// <summary>
/// Static enemy definition loaded from content JSON. Used by Simulation to spawn
/// entities with correct stats and by AI to assign the correct brain type.
/// </summary>
public sealed record EnemyTemplate(
    /// <summary>Unique template ID (e.g., "goblin", "dragon", "skeleton_archer").</summary>
    string TemplateId,

    /// <summary>Display name for UI and combat log.</summary>
    string DisplayName,

    /// <summary>Flavor text / bestiary description.</summary>
    string Description,

    /// <summary>
    /// Base stats for this enemy type. Cloned per-instance at spawn time
    /// so modifications don't affect the template.
    /// </summary>
    Stats BaseStats,

    /// <summary>
    /// AI brain type identifier. Resolved by BrainFactory to an IBrain instance.
    /// Examples: "melee_rusher", "ranged_kiter", "patrol_guard", "fleeing".
    /// </summary>
    string BrainType,

    /// <summary>Faction this enemy belongs to (usually Faction.Enemy).</summary>
    Faction Faction,

    /// <summary>Minimum dungeon depth where this enemy can spawn (inclusive).</summary>
    int MinDepth,

    /// <summary>Maximum dungeon depth where this enemy can spawn (inclusive). -1 = no limit.</summary>
    int MaxDepth,

    /// <summary>
    /// Relative spawn weight for weighted random selection. Higher = more common.
    /// A goblin might be 100, a dragon might be 5.
    /// </summary>
    int SpawnWeight,

    /// <summary>
    /// Loot table ID for drops on death. Null = no drops.
    /// Resolved by LootTableResolver.
    /// </summary>
    string? LootTableId
);
```

---

### 4.11 LevelData

```csharp
// Core/Contracts/Types/LevelData.cs
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// Metadata returned by <see cref="IGenerator.GenerateLevel"/>. Contains spawn
/// positions and room layout â€” the generator writes tiles into WorldState directly,
/// then returns this so Simulation can place entities.
/// </summary>
public sealed record LevelData(
    /// <summary>Where the player should spawn on this level.</summary>
    Position PlayerSpawn,

    /// <summary>Position of the stairs down to the next depth.</summary>
    Position StairsDown,

    /// <summary>
    /// Suggested enemy spawn positions. Simulation picks which enemies
    /// to place based on depth and content data.
    /// </summary>
    IReadOnlyList<Position> EnemySpawns,

    /// <summary>
    /// Suggested item spawn positions. Simulation picks which items
    /// to place based on depth and loot tables.
    /// </summary>
    IReadOnlyList<Position> ItemSpawns,

    /// <summary>
    /// Room rectangles for minimap overlay, debug visualization,
    /// and corridor connectivity validation.
    /// </summary>
    IReadOnlyList<RoomData> Rooms
);
```

---

### 4.12 RoomData

```csharp
// Core/Contracts/Types/RoomData.cs
namespace Roguelike.Core;

/// <summary>
/// Axis-aligned room rectangle within the dungeon grid. Used by generation
/// for corridor connection, by minimap for room outlines, and by debug overlay.
/// </summary>
public sealed record RoomData(
    /// <summary>Top-left X coordinate of the room.</summary>
    int X,

    /// <summary>Top-left Y coordinate of the room.</summary>
    int Y,

    /// <summary>Room width in tiles (including walls).</summary>
    int Width,

    /// <summary>Room height in tiles (including walls).</summary>
    int Height,

    /// <summary>Center position of the room (used for corridor endpoint connections).</summary>
    Position Center
);
```

---

### 4.13 SaveMetadata

```csharp
// Core/Contracts/Types/SaveMetadata.cs
using System;

namespace Roguelike.Core;

/// <summary>
/// Lightweight metadata for a save slot, displayed in the load/save UI
/// without deserializing the full world state.
/// </summary>
public sealed record SaveMetadata(
    /// <summary>Save slot index (0â€“2).</summary>
    int SlotIndex,

    /// <summary>Dungeon depth at time of save.</summary>
    int Depth,

    /// <summary>Turn number at time of save.</summary>
    int TurnNumber,

    /// <summary>Player character name for display.</summary>
    string PlayerName,

    /// <summary>When the save was created (UTC).</summary>
    DateTime SavedAt,

    /// <summary>Save format version for migration compatibility.</summary>
    int Version
);
```

---

### 4.14 IEntity

```csharp
// Core/Contracts/IEntity.cs
namespace Roguelike.Core;

/// <summary>
/// A game entity: player, monster, NPC, or interactable object. All mutable game
/// objects implement this interface.
///
/// <para>
/// Core properties (position, stats, faction) are first-class to avoid boxing
/// overhead on hot paths. Optional behaviors (inventory, equipment, AI brain)
/// use the lightweight component system via <see cref="GetComponent{T}"/>.
/// </para>
/// </summary>
public interface IEntity
{
    /// <summary>Globally unique identifier. Stable across save/load.</summary>
    EntityId Id { get; }

    /// <summary>Display name for UI and combat log (e.g., "Goblin", "Iron Sword").</summary>
    string Name { get; }

    /// <summary>Current grid position. Updated when the entity moves.</summary>
    Position Position { get; set; }

    /// <summary>Combat and turn economy stats. Never null for alive entities.</summary>
    Stats Stats { get; }

    /// <summary>Allegiance for AI targeting decisions.</summary>
    Faction Faction { get; }

    /// <summary>Whether this entity prevents other entities from entering its tile.</summary>
    bool BlocksMovement { get; }

    /// <summary>Whether this entity blocks line of sight through its tile.</summary>
    bool BlocksSight { get; }

    /// <summary>Is this entity alive and active in the world?</summary>
    bool IsAlive { get; }

    // â”€â”€ Lightweight Component System â”€â”€

    /// <summary>Check if this entity has a component of type <typeparamref name="T"/>.</summary>
    bool HasComponent<T>() where T : class;

    /// <summary>Get component of type <typeparamref name="T"/>, or null if not present.</summary>
    T? GetComponent<T>() where T : class;

    /// <summary>Add or replace a component of type <typeparamref name="T"/>.</summary>
    void SetComponent<T>(T component) where T : class;

    /// <summary>Remove the component of type <typeparamref name="T"/> if present.</summary>
    void RemoveComponent<T>() where T : class;
}
```

---

### 4.15 IAction

```csharp
// Core/Contracts/IAction.cs
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// An atomic game action. ALL state changes flow through actions â€” player input,
/// AI decisions, and scripted events all produce <see cref="IAction"/> instances
/// that are validated then executed against <see cref="WorldState"/>.
///
/// <para><b>Lifecycle:</b></para>
/// <list type="number">
///   <item>Creator builds the action with actor, target, and parameters.</item>
///   <item><see cref="Validate"/> checks legality against current state (pure, no side effects).</item>
///   <item>If valid, <see cref="Execute"/> mutates WorldState and returns an <see cref="ActionOutcome"/>.</item>
///   <item>TurnScheduler deducts <see cref="GetEnergyCost"/> from the actor's energy.</item>
/// </list>
///
/// <para>Actions MUST be deterministic given the same WorldState. This enables replay and testing.</para>
/// </summary>
public interface IAction
{
    /// <summary>The entity performing this action.</summary>
    EntityId ActorId { get; }

    /// <summary>What kind of action this is (for animation/logging dispatch).</summary>
    ActionType Type { get; }

    /// <summary>
    /// Check if this action is legal in the current world state.
    /// MUST NOT mutate world state. MUST be pure and side-effect-free.
    /// </summary>
    /// <param name="world">Current world state (read-only access).</param>
    /// <returns>Success if action can proceed, or reason for failure.</returns>
    ActionResult Validate(IWorldState world);

    /// <summary>
    /// Execute the action, mutating world state. Only called after
    /// <see cref="Validate"/> returns <see cref="ActionResult.Success"/>.
    /// </summary>
    /// <param name="world">Mutable world state to modify.</param>
    /// <returns>Outcome events for broadcast to rendering/UI/logging.</returns>
    ActionOutcome Execute(WorldState world);

    /// <summary>
    /// Energy cost of this action. Deducted from actor after Execute().
    /// Standard move/attack = 1000. Wait = 1000. Item use = 500.
    /// </summary>
    int GetEnergyCost();
}

/// <summary>
/// The result of executing an action. Contains all side-effect events for
/// broadcast via EventBus.
/// </summary>
public sealed class ActionOutcome
{
    /// <summary>Whether the action succeeded.</summary>
    public ActionResult Result { get; init; }

    /// <summary>Combat events generated (hits, misses, kills, crits).</summary>
    public List<CombatEvent> CombatEvents { get; init; } = new();

    /// <summary>Human-readable log messages for the combat log UI.</summary>
    public List<string> LogMessages { get; init; } = new();

    /// <summary>
    /// Grid positions that need visual refresh. Rendering agent uses this
    /// to minimize TileMap redraws.
    /// </summary>
    public List<Position> DirtyPositions { get; init; } = new();

    /// <summary>Create a failed outcome with the specified reason.</summary>
    public static ActionOutcome Fail(ActionResult reason) => new() { Result = reason };

    /// <summary>Create a successful outcome (caller adds events as needed).</summary>
    public static ActionOutcome Ok() => new() { Result = ActionResult.Success };
}
```

---

### 4.16 IWorldState

```csharp
// Core/Contracts/IWorldState.cs
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// Read-only view of the game world. Used by action validation, AI queries,
/// FOV calculation, and rendering. The mutable <see cref="WorldState"/> implements
/// this interface; agents that only need to read state accept <c>IWorldState</c>
/// to enforce immutability at the API boundary.
/// </summary>
public interface IWorldState
{
    // â”€â”€ Map â”€â”€

    /// <summary>Width of the current level in tiles.</summary>
    int Width { get; }

    /// <summary>Height of the current level in tiles.</summary>
    int Height { get; }

    /// <summary>Get tile type at position. Returns <see cref="TileType.Void"/> for out-of-bounds.</summary>
    TileType GetTile(Position pos);

    /// <summary>Is this position within map bounds?</summary>
    bool InBounds(Position pos);

    /// <summary>
    /// Can an entity walk onto this tile? Checks tile type (floor/door/stairs/water)
    /// AND that no blocking entity occupies it.
    /// </summary>
    bool IsWalkable(Position pos);

    /// <summary>Does this position block line of sight? (walls, void, closed doors)</summary>
    bool BlocksSight(Position pos);

    // â”€â”€ Entities â”€â”€

    /// <summary>Get entity by ID. Returns null if not found or dead.</summary>
    IEntity? GetEntity(EntityId id);

    /// <summary>Get the first blocking entity at position, or null.</summary>
    IEntity? GetEntityAt(Position pos);

    /// <summary>All living entities on this level.</summary>
    IReadOnlyList<IEntity> Entities { get; }

    /// <summary>The player entity. Never null during active gameplay.</summary>
    IEntity Player { get; }

    // â”€â”€ Turn â”€â”€

    /// <summary>Current turn number (incremented each full round).</summary>
    int TurnNumber { get; }

    /// <summary>Current dungeon depth (0 = first floor).</summary>
    int Depth { get; }

    // â”€â”€ FOV â”€â”€

    /// <summary>Is this tile currently visible to the player?</summary>
    bool IsVisible(Position pos);

    /// <summary>Has this tile ever been seen by the player? (fog-of-war memory)</summary>
    bool IsExplored(Position pos);
}
```

---

### 4.17 WorldState

```csharp
// Core/Contracts/WorldState.cs
using System;
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// Mutable world state. The single source of truth for the game simulation.
/// Owned by the Simulation agent; other agents receive <see cref="IWorldState"/>
/// (read-only) for queries.
///
/// <para><b>Grid layout:</b> Flat array indexed as <c>grid[y * Width + x]</c>
/// for cache-friendly row-major access.</para>
///
/// <para><b>Entity spatial index:</b> Dictionary keyed by <see cref="Position"/>
/// for O(1) "who is at this tile?" queries.</para>
/// </summary>
public sealed class WorldState : IWorldState
{
    // â”€â”€ Map data â”€â”€
    private TileType[] _grid = Array.Empty<TileType>();
    private bool[] _visible = Array.Empty<bool>();
    private bool[] _explored = Array.Empty<bool>();

    /// <summary>Width of the current level in tiles.</summary>
    public int Width { get; private set; }

    /// <summary>Height of the current level in tiles.</summary>
    public int Height { get; private set; }

    // â”€â”€ Entity storage â”€â”€
    private readonly List<IEntity> _entities = new();
    private readonly Dictionary<EntityId, IEntity> _entityById = new();
    private readonly Dictionary<Position, IEntity> _blockingByPos = new();

    /// <summary>All living entities on this level.</summary>
    public IReadOnlyList<IEntity> Entities => _entities;

    /// <summary>The player entity. Set during level initialization.</summary>
    public IEntity Player { get; set; } = null!;

    // â”€â”€ Turn tracking â”€â”€

    /// <summary>Current turn number (incremented each full round).</summary>
    public int TurnNumber { get; set; }

    /// <summary>Current dungeon depth (0 = first floor).</summary>
    public int Depth { get; set; }

    // â”€â”€ Determinism â”€â”€

    /// <summary>Random seed for this game. All randomness derives from this.</summary>
    public int Seed { get; set; }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  GRID MANAGEMENT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Initialize or resize the grid. Called by the generator at the start
    /// of each level. Clears all tile, visibility, and exploration data.
    /// </summary>
    public void InitGrid(int width, int height)
    {
        Width = width;
        Height = height;
        int size = width * height;
        _grid = new TileType[size];
        _visible = new bool[size];
        _explored = new bool[size];
    }

    /// <summary>Get tile type at position. Returns Void for out-of-bounds.</summary>
    public TileType GetTile(Position pos) =>
        InBounds(pos) ? _grid[pos.Y * Width + pos.X] : TileType.Void;

    /// <summary>Set tile type at position. No-op for out-of-bounds.</summary>
    public void SetTile(Position pos, TileType type)
    {
        if (InBounds(pos))
            _grid[pos.Y * Width + pos.X] = type;
    }

    /// <summary>Is this position within map bounds?</summary>
    public bool InBounds(Position pos) =>
        pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

    /// <summary>Can an entity walk onto this tile?</summary>
    public bool IsWalkable(Position pos) =>
        InBounds(pos) &&
        GetTile(pos) is TileType.Floor or TileType.Door or TileType.StairsDown
            or TileType.StairsUp or TileType.Water &&
        !_blockingByPos.ContainsKey(pos);

    /// <summary>Does this tile block line of sight?</summary>
    public bool BlocksSight(Position pos) =>
        !InBounds(pos) || GetTile(pos) is TileType.Wall or TileType.Void;

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  ENTITY MANAGEMENT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Add an entity to the world. Registers it in the ID lookup and
    /// spatial index (if it blocks movement).
    /// </summary>
    public void AddEntity(IEntity entity)
    {
        _entities.Add(entity);
        _entityById[entity.Id] = entity;
        if (entity.BlocksMovement)
            _blockingByPos[entity.Position] = entity;
    }

    /// <summary>Remove an entity from the world by ID.</summary>
    public void RemoveEntity(EntityId id)
    {
        if (_entityById.TryGetValue(id, out var entity))
        {
            _entities.Remove(entity);
            _entityById.Remove(id);
            _blockingByPos.Remove(entity.Position);
        }
    }

    /// <summary>
    /// Update spatial index when an entity moves. Must be called after
    /// changing the entity's Position property.
    /// </summary>
    public void UpdateEntityPosition(EntityId id, Position oldPos, Position newPos)
    {
        if (_entityById.TryGetValue(id, out var entity))
        {
            if (entity.BlocksMovement)
            {
                _blockingByPos.Remove(oldPos);
                _blockingByPos[newPos] = entity;
            }
        }
    }

    /// <summary>Get entity by ID, or null if not found.</summary>
    public IEntity? GetEntity(EntityId id) =>
        _entityById.TryGetValue(id, out var e) ? e : null;

    /// <summary>Get the blocking entity at a position, or null.</summary>
    public IEntity? GetEntityAt(Position pos) =>
        _blockingByPos.TryGetValue(pos, out var e) ? e : null;

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FOV / VISIBILITY
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>Is this tile currently visible to the player?</summary>
    public bool IsVisible(Position pos) =>
        InBounds(pos) && _visible[pos.Y * Width + pos.X];

    /// <summary>Has this tile ever been seen?</summary>
    public bool IsExplored(Position pos) =>
        InBounds(pos) && _explored[pos.Y * Width + pos.X];

    /// <summary>
    /// Set visibility for a tile. Marking visible also marks explored.
    /// Called by the FOV calculator.
    /// </summary>
    public void SetVisible(Position pos, bool visible)
    {
        if (!InBounds(pos)) return;
        int idx = pos.Y * Width + pos.X;
        _visible[idx] = visible;
        if (visible) _explored[idx] = true;
    }

    /// <summary>Clear all visibility flags (called before FOV recalculation).</summary>
    public void ClearVisibility()
    {
        Array.Clear(_visible, 0, _visible.Length);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  SERIALIZATION ACCESS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>Access raw grid array for serialization. Do not mutate directly.</summary>
    public TileType[] GetRawGrid() => _grid;

    /// <summary>Access raw explored array for serialization.</summary>
    public bool[] GetRawExplored() => _explored;
}
```

---

### 4.18 ITurnScheduler

```csharp
// Core/Contracts/ITurnScheduler.cs
namespace Roguelike.Core;

/// <summary>
/// Energy-based turn scheduler. Each entity accumulates energy proportional to its
/// <see cref="Stats.Speed"/>. When an entity's energy reaches the threshold, it acts.
///
/// <para><b>Energy math:</b></para>
/// <list type="bullet">
///   <item>Energy threshold = 1000 (one standard action).</item>
///   <item>Energy granted per round = <c>Speed Ã— 10</c>.</item>
///   <item>Speed 100 â†’ 1000 energy/round â†’ 1 action/round.</item>
///   <item>Speed 150 â†’ 1500 energy/round â†’ sometimes 2 actions/round.</item>
///   <item>Speed 50 â†’ 500 energy/round â†’ 1 action every 2 rounds.</item>
/// </list>
///
/// <para><b>Usage pattern:</b></para>
/// <code>
/// scheduler.BeginRound(world);
/// while (scheduler.HasNextActor())
/// {
///     var actor = scheduler.GetNextActor();
///     // get action from player input or AI brain
///     var action = GetAction(actor);
///     if (action.Validate(world) == ActionResult.Success)
///     {
///         action.Execute(world);
///         scheduler.ConsumeEnergy(actor.Id, action.GetEnergyCost());
///     }
/// }
/// scheduler.EndRound(world);
/// </code>
/// </summary>
public interface ITurnScheduler
{
    /// <summary>
    /// Energy required to take an action. Standard value = 1000.
    /// </summary>
    int EnergyThreshold { get; }

    /// <summary>
    /// Begin a new round: grant energy to all registered entities.
    /// Energy granted = <c>entity.Stats.Speed Ã— 10</c>.
    /// </summary>
    void BeginRound(WorldState world);

    /// <summary>Are there entities with enough energy to act this round?</summary>
    bool HasNextActor();

    /// <summary>
    /// Get the next entity that should act (highest energy first).
    /// Player wins ties. Returns null if no actor has enough energy.
    /// </summary>
    IEntity? GetNextActor();

    /// <summary>Deduct energy from an entity after its action executes.</summary>
    void ConsumeEnergy(EntityId actorId, int cost);

    /// <summary>
    /// End-of-round bookkeeping: tick status effects, regeneration,
    /// increment turn counter.
    /// </summary>
    void EndRound(WorldState world);

    /// <summary>Register a new entity with the scheduler (called on spawn).</summary>
    void Register(IEntity entity);

    /// <summary>Remove an entity from the scheduler (called on death/despawn).</summary>
    void Unregister(EntityId id);
}
```

---

### 4.19 IGenerator

```csharp
// Core/Contracts/IGenerator.cs
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// Dungeon level generator. Produces a complete level (tile grid + spawn metadata)
/// from a seed and depth. MUST be deterministic: same seed + depth = same level.
///
/// <para><b>Pipeline:</b></para>
/// <list type="number">
///   <item><see cref="GenerateLevel"/> writes tiles into WorldState and returns spawn metadata.</item>
///   <item>Simulation uses the returned <see cref="LevelData"/> to place entities at spawn positions.</item>
///   <item><see cref="ValidateLevel"/> checks solvability (connectivity, reachability).</item>
/// </list>
///
/// <para>The generator does NOT add entities to WorldState â€” it only returns positions.
/// Entity creation is Simulation's responsibility.</para>
/// </summary>
public interface IGenerator
{
    /// <summary>
    /// Generate a complete dungeon level. Writes tile data into the world's grid
    /// via <see cref="WorldState.InitGrid"/> and <see cref="WorldState.SetTile"/>.
    /// </summary>
    /// <param name="world">WorldState to write grid data into.</param>
    /// <param name="seed">Random seed for deterministic generation.</param>
    /// <param name="depth">Dungeon depth (affects room count, enemy density, etc.).</param>
    /// <returns>Metadata with spawn positions and room layout.</returns>
    LevelData GenerateLevel(WorldState world, int seed, int depth);

    /// <summary>
    /// Validate that a generated level is solvable:
    /// <list type="bullet">
    ///   <item>Player start is reachable to stairs down.</item>
    ///   <item>No isolated rooms (all rooms connected).</item>
    ///   <item>Minimum/maximum room count satisfied.</item>
    ///   <item>All spawn positions are on walkable tiles.</item>
    /// </list>
    /// </summary>
    /// <returns>List of validation errors. Empty = level is valid.</returns>
    IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data);
}
```

---

### 4.20 IPathfinder

```csharp
// Core/Contracts/IPathfinder.cs
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// A* pathfinder on the tile grid. Used by AI for movement planning,
/// by rendering for path preview, and by generation for connectivity validation.
///
/// <para>All pathfinding is synchronous and bounded by <c>maxLength</c> to
/// prevent frame spikes on large maps.</para>
/// </summary>
public interface IPathfinder
{
    /// <summary>
    /// Find the shortest path from start to goal using A*.
    /// </summary>
    /// <param name="start">Starting position.</param>
    /// <param name="goal">Target position.</param>
    /// <param name="world">World state for walkability checks.</param>
    /// <param name="maxLength">Maximum path length to search (performance bound). Default 50.</param>
    /// <returns>
    /// Ordered list of positions from start to goal (inclusive of both endpoints).
    /// Empty list if no path exists within maxLength.
    /// </returns>
    IReadOnlyList<Position> FindPath(Position start, Position goal, IWorldState world, int maxLength = 50);

    /// <summary>
    /// Quick check if a path exists without computing the full path.
    /// Faster than FindPath when you only need a boolean answer.
    /// </summary>
    bool HasPath(Position start, Position goal, IWorldState world, int maxLength = 50);

    /// <summary>
    /// Get all positions reachable from origin within a given movement range.
    /// Used for ability targeting circles and AI movement planning.
    /// </summary>
    /// <param name="origin">Starting position.</param>
    /// <param name="range">Maximum movement distance (Manhattan).</param>
    /// <param name="world">World state for walkability checks.</param>
    /// <returns>Dictionary mapping reachable positions to their distance from origin.</returns>
    IReadOnlyDictionary<Position, int> GetReachable(Position origin, int range, IWorldState world);
}
```

---

### 4.21 IBrain

```csharp
// Core/Contracts/IBrain.cs
namespace Roguelike.Core;

/// <summary>
/// AI decision-maker for a single entity. Each enemy type can have a different
/// IBrain implementation (melee rusher, ranged kiter, patrol guard, etc.).
///
/// <para>Called once per turn when the entity has enough energy to act.
/// Returns an <see cref="IAction"/> that the game loop validates and executes.</para>
///
/// <para><b>Stateless contract:</b> Brains MUST be stateless between turns.
/// All persistent AI state (last seen player position, patrol waypoints, etc.)
/// must be stored as components on the entity via <see cref="IEntity.SetComponent{T}"/>.
/// This ensures deterministic replay â€” given the same WorldState, the same
/// action is always produced.</para>
/// </summary>
public interface IBrain
{
    /// <summary>
    /// Decide what action this entity should take this turn.
    /// </summary>
    /// <param name="self">The entity making the decision.</param>
    /// <param name="world">Read-only world state for perception queries.</param>
    /// <param name="pathfinder">Pathfinding service for movement planning.</param>
    /// <returns>
    /// The action to attempt. Must never return null â€” return a WaitAction if
    /// the brain cannot decide or is stuck.
    /// </returns>
    IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder);
}
```

---

### 4.22 ISaveManager

```csharp
// Core/Contracts/ISaveManager.cs
using System.Threading.Tasks;

namespace Roguelike.Core;

/// <summary>
/// Serialization interface for mid-run save/load. Saves the complete game state
/// (grid, entities, inventory, turn number, depth) to JSON files.
///
/// <para><b>Save format:</b> JSON with a version header. Forward-compatible:
/// new fields get defaults, removed fields are ignored. Versioned migration
/// handles breaking changes.</para>
///
/// <para><b>File naming:</b> <c>user://saves/save_{slotIndex}.json</c></para>
///
/// <para><b>Slots:</b> 0â€“2 (3 save slots).</para>
/// </summary>
public interface ISaveManager
{
    /// <summary>
    /// Save complete game state to a slot. Serializes WorldState, all entities,
    /// their components, and global state (turn, depth, seed).
    /// </summary>
    /// <param name="world">Current world state to serialize.</param>
    /// <param name="slotIndex">Save slot (0â€“2).</param>
    /// <returns>True if save succeeded, false on I/O error.</returns>
    Task<bool> SaveGame(WorldState world, int slotIndex);

    /// <summary>
    /// Load game state from a slot. Returns a fully reconstructed WorldState
    /// with all entities, components, and spatial indices rebuilt.
    /// </summary>
    /// <param name="slotIndex">Save slot to load from.</param>
    /// <returns>Loaded world state, or null if slot is empty or data is corrupt.</returns>
    Task<WorldState?> LoadGame(int slotIndex);

    /// <summary>Check if a save slot has valid data.</summary>
    bool HasSave(int slotIndex);

    /// <summary>Delete a save slot's file.</summary>
    void DeleteSave(int slotIndex);

    /// <summary>
    /// Get metadata about a save without loading the full world state.
    /// Used by the save/load UI to display slot information.
    /// </summary>
    SaveMetadata? GetSaveMetadata(int slotIndex);
}
```

---

### 4.23 IFOV

```csharp
// Core/Contracts/IFOV.cs
using System;

namespace Roguelike.Core;

/// <summary>
/// Field-of-view calculator using symmetric shadowcasting. Computes which tiles
/// are visible from a given origin point within a radius.
///
/// <para><b>Symmetric shadowcasting</b> guarantees: if tile A can see tile B,
/// then tile B can also see tile A. This prevents the "I can see you but you
/// can't see me" artifacts of older FOV algorithms.</para>
///
/// <para>Called once per player turn after movement. Results are written into
/// WorldState via the <paramref name="markVisible"/> callback.</para>
/// </summary>
public interface IFOV
{
    /// <summary>
    /// Compute visible tiles from origin within radius.
    /// </summary>
    /// <param name="origin">The viewer's position.</param>
    /// <param name="radius">Maximum view distance in tiles.</param>
    /// <param name="blocksLight">
    /// Callback returning true if the tile at the given position blocks line of sight.
    /// Typically: <c>pos => world.BlocksSight(pos)</c>.
    /// </param>
    /// <param name="markVisible">
    /// Callback invoked for each tile that is visible from origin.
    /// Typically: <c>pos => world.SetVisible(pos, true)</c>.
    /// </param>
    void Compute(
        Position origin,
        int radius,
        Func<Position, bool> blocksLight,
        Action<Position> markVisible
    );
}
```

---

### 4.24 IContentDatabase

```csharp
// Core/Contracts/IContentDatabase.cs
using System.Collections.Generic;

namespace Roguelike.Core;

/// <summary>
/// Read-only access to game content data loaded from JSON files at startup.
/// Provides item templates, enemy templates, and depth-filtered queries.
///
/// <para>Owned by the Content agent (Agent 9). Consumed by Simulation (spawning),
/// AI (evaluating items/threats), and UI (displaying item/enemy info).</para>
///
/// <para>Content is loaded once at startup and is immutable during gameplay.
/// Hot-reload is supported in debug builds only.</para>
/// </summary>
public interface IContentDatabase
{
    /// <summary>Get item template by template ID. Returns null if not found.</summary>
    ItemTemplate? GetItem(string templateId);

    /// <summary>All loaded item templates.</summary>
    IReadOnlyList<ItemTemplate> AllItems { get; }

    /// <summary>Get enemy template by template ID. Returns null if not found.</summary>
    EnemyTemplate? GetEnemy(string templateId);

    /// <summary>All loaded enemy templates.</summary>
    IReadOnlyList<EnemyTemplate> AllEnemies { get; }

    /// <summary>
    /// Get enemy templates whose depth range includes the specified depth.
    /// Used by Simulation to select enemies for spawning on a given floor.
    /// </summary>
    IReadOnlyList<EnemyTemplate> GetEnemiesForDepth(int depth);

    /// <summary>
    /// Get item templates appropriate for the specified depth.
    /// Used for loot generation and ground item placement.
    /// </summary>
    IReadOnlyList<ItemTemplate> GetItemsForDepth(int depth);
}
```


---

## 5. Signal / Event Bus Contract

The EventBus is a Godot autoload singleton registered in `project.godot` at `res://scripts/autoloads/EventBus.cs`. ALL game events flow through it. No direct node-to-node signals. Agents emit via `EventBus.Instance.EmitSignal(SignalName.Xxx, ...)` and subscribe via `EventBus.Instance.Xxx += handler`.

### 5.1 Complete EventBus.cs

```csharp
// Scripts/Autoloads/EventBus.cs
using Godot;

namespace Roguelike.Godot;

/// <summary>
/// Global event bus. Autoload singleton. All game events flow through here.
///
/// Emitters call: EventBus.Instance.EmitSignal(SignalName.EntityMoved, ...);
/// Listeners call: EventBus.Instance.EntityMoved += OnEntityMoved;
///
/// WHY a bus instead of direct signals:
///   - Agents don't need references to each other's nodes
///   - Easy to add/remove listeners without coupling
///   - All events go through one place for replay/logging
///   - Deterministic event ordering for replay systems
///
/// RULES:
///   - All string entityId params are EntityId.ToString() (first 8 chars of GUID)
///   - Enums are passed as int because Godot signals cannot marshal C# enums
///   - Signal names use PascalCase matching the delegate name minus "EventHandler"
/// </summary>
public partial class EventBus : Node
{
    /// <summary>Singleton instance. Set in _Ready. Never null after autoload init.</summary>
    public static EventBus Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  ENTITY MOVEMENT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Emitted after an entity successfully moves to a new tile.
    /// Emitter: Simulation (MoveAction.Execute)
    /// Consumers:
    ///   - Rendering: animate sprite lerp from (fromX,fromY) to (toX,toY)
    ///   - FOV: recalculate if entityId is the player
    ///   - UI: update minimap blip position
    /// </summary>
    [Signal]
    public delegate void EntityMovedEventHandler(
        string entityId,
        int fromX, int fromY,
        int toX, int toY
    );

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  COMBAT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Emitted when an entity attacks another (melee or ranged).
    /// Emitter: Simulation (AttackAction.Execute / CombatResolver)
    /// Consumers:
    ///   - Rendering: play attack animation, show damage number, screen shake on crit
    ///   - UI: append line to combat log
    ///   - Audio: play hit/miss/crit SFX
    /// </summary>
    [Signal]
    public delegate void EntityAttackedEventHandler(
        string attackerId,
        string defenderId,
        int damage,
        bool isCritical,
        bool isMiss
    );

    /// <summary>
    /// Emitted when an entity's HP changes (damage, healing, regen tick).
    /// Emitter: Simulation (CombatResolver, StatusEffectProcessor, UseItemAction)
    /// Consumers:
    ///   - Rendering: floating damage/heal number, health bar update
    ///   - UI: HUD health display, combat log
    /// </summary>
    [Signal]
    public delegate void EntityHealthChangedEventHandler(
        string entityId,
        int oldHP,
        int newHP,
        int maxHP
    );

    /// <summary>
    /// Emitted when an entity dies (HP reaches 0).
    /// Emitter: Simulation (CombatResolver after lethal damage)
    /// Consumers:
    ///   - Rendering: death animation, corpse sprite swap
    ///   - AI: remove from target lists, unregister brain
    ///   - UI: kill log entry, XP gain display
    /// </summary>
    [Signal]
    public delegate void EntityDiedEventHandler(
        string entityId,
        string killerEntityId
    );

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  TURN SYSTEM
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Emitted at the start of a new round (all entities receive energy).
    /// Emitter: Simulation (TurnScheduler.BeginRound)
    /// Consumers:
    ///   - UI: update turn counter display
    ///   - Rendering: tick status effect visual timers
    /// </summary>
    [Signal]
    public delegate void TurnStartedEventHandler(int turnNumber);

    /// <summary>
    /// Emitted after all entities have acted in a round.
    /// Emitter: Simulation (TurnScheduler.EndRound)
    /// Consumers:
    ///   - UI: end-of-round summary, status duration decrement display
    /// </summary>
    [Signal]
    public delegate void TurnEndedEventHandler(int turnNumber);

    /// <summary>
    /// Emitted when a specific entity's turn begins (it has enough energy to act).
    /// Emitter: Simulation (GameLoop, after GetNextActor)
    /// Consumers:
    ///   - UI: highlight current actor in entity list
    ///   - AI: trigger brain.DecideAction for non-player entities
    /// </summary>
    [Signal]
    public delegate void EntityTurnStartedEventHandler(string entityId);

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  LEVEL / GENERATION
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Emitted when a new level has been generated and entities placed.
    /// Emitter: Simulation (after IGenerator.GenerateLevel + entity spawning)
    /// Consumers:
    ///   - Rendering: rebuild TileMapLayer from WorldState grid
    ///   - UI: update depth display, reset minimap
    ///   - FOV: perform initial visibility calculation
    /// </summary>
    [Signal]
    public delegate void LevelGeneratedEventHandler(int depth, int width, int height);

    /// <summary>
    /// Emitted when the player uses stairs to change levels.
    /// Emitter: Simulation (DescendAction.Execute)
    /// Consumers:
    ///   - Rendering: play transition animation (fade out/in)
    ///   - UI: show loading indicator, update depth label
    /// </summary>
    [Signal]
    public delegate void LevelTransitionEventHandler(int fromDepth, int toDepth);

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  ITEMS / INVENTORY
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Emitted when an entity picks up an item from the ground.
    /// Emitter: Simulation (PickupAction.Execute)
    /// Consumers:
    ///   - UI: add to inventory panel, show pickup message
    ///   - Rendering: remove ground item sprite
    /// </summary>
    [Signal]
    public delegate void ItemPickedUpEventHandler(string entityId, string itemTemplateId);

    /// <summary>
    /// Emitted when an entity uses a consumable item.
    /// Emitter: Simulation (UseItemAction.Execute)
    /// Consumers:
    ///   - UI: update inventory (decrement stack/remove), show effect message
    ///   - Rendering: play use effect VFX (heal glow, speed lines, etc.)
    /// </summary>
    [Signal]
    public delegate void ItemUsedEventHandler(
        string entityId,
        string itemTemplateId,
        string effectDescription
    );

    /// <summary>
    /// Emitted when an entity drops an item onto the ground.
    /// Emitter: Simulation (DropAction.Execute)
    /// Consumers:
    ///   - UI: remove from inventory panel, show drop message
    ///   - Rendering: add ground item sprite at (posX, posY)
    /// </summary>
    [Signal]
    public delegate void ItemDroppedEventHandler(
        string entityId,
        string itemTemplateId,
        int posX, int posY
    );

    /// <summary>
    /// Emitted when an entity equips or unequips an item.
    /// Emitter: Simulation (equip/unequip logic in Inventory)
    /// Consumers:
    ///   - UI: update character sheet, refresh stat display
    /// slot is EquipSlot cast to int; itemTemplateId is "" for unequip.
    /// </summary>
    [Signal]
    public delegate void EquipmentChangedEventHandler(
        string entityId,
        int slot,
        string itemTemplateId
    );

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FOV / VISIBILITY
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Emitted after the FOV has been recalculated (every player move or turn start).
    /// Emitter: Rendering (ShadowcastFOV after compute pass)
    /// Consumers:
    ///   - Rendering: update tile tint (visible/explored/unseen)
    ///   - AI: check if player is within enemy perception range
    /// </summary>
    [Signal]
    public delegate void FOVUpdatedEventHandler();

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  STATUS EFFECTS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Emitted when a status effect is applied to an entity.
    /// Emitter: Simulation (StatusEffectProcessor, CombatResolver on_hit effects)
    /// Consumers:
    ///   - Rendering: show status icon on entity sprite, apply color tint
    ///   - UI: combat log entry ("Goblin is Poisoned for 5 turns")
    /// effectType is StatusEffectType cast to int (Godot signal limitation).
    /// </summary>
    [Signal]
    public delegate void StatusEffectAppliedEventHandler(
        string entityId,
        int effectType,
        int duration
    );

    /// <summary>
    /// Emitted when a status effect expires or is removed.
    /// Emitter: Simulation (StatusEffectProcessor on tick or cure)
    /// Consumers:
    ///   - Rendering: remove status icon, clear color tint
    ///   - UI: combat log entry ("Poison wears off")
    /// </summary>
    [Signal]
    public delegate void StatusEffectRemovedEventHandler(
        string entityId,
        int effectType
    );

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  GAME FLOW
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Emitted when the player dies â€” game over.
    /// Emitter: Simulation (after player EntityDied)
    /// Consumers:
    ///   - UI: show game over screen with stats
    /// </summary>
    [Signal]
    public delegate void GameOverEventHandler(int finalDepth, int turnsSurvived);

    /// <summary>
    /// Emitted when the player requests a save from the UI.
    /// Emitter: UI (pause menu or quicksave keybind)
    /// Consumers:
    ///   - Persistence: serialize WorldState to slot
    /// </summary>
    [Signal]
    public delegate void SaveRequestedEventHandler(int slotIndex);

    /// <summary>
    /// Emitted after a save operation finishes.
    /// Emitter: Persistence (SaveManager)
    /// Consumers:
    ///   - UI: show "Game Saved" toast or error message
    /// </summary>
    [Signal]
    public delegate void SaveCompletedEventHandler(bool success);

    /// <summary>
    /// Emitted when the player requests a load from the UI.
    /// Emitter: UI (main menu or quickload keybind)
    /// Consumers:
    ///   - Persistence: deserialize WorldState from slot
    /// </summary>
    [Signal]
    public delegate void LoadRequestedEventHandler(int slotIndex);

    /// <summary>
    /// Emitted after a load operation finishes.
    /// Emitter: Persistence (SaveManager)
    /// Consumers:
    ///   - UI: close load menu, refresh all panels
    ///   - Simulation: replace active WorldState with loaded state
    /// </summary>
    [Signal]
    public delegate void LoadCompletedEventHandler(bool success);
}
```

### 5.2 Signal Wiring Summary Table

| Signal | Emitter | Consumers | Payload |
|---|---|---|---|
| `EntityMoved` | Simulation | Rendering, FOV, UI | entityId, fromX, fromY, toX, toY |
| `EntityAttacked` | Simulation | Rendering, UI, Audio | attackerId, defenderId, damage, isCritical, isMiss |
| `EntityHealthChanged` | Simulation | Rendering, UI | entityId, oldHP, newHP, maxHP |
| `EntityDied` | Simulation | Rendering, AI, UI | entityId, killerEntityId |
| `TurnStarted` | Simulation | UI, Rendering | turnNumber |
| `TurnEnded` | Simulation | UI | turnNumber |
| `EntityTurnStarted` | Simulation | UI, AI | entityId |
| `LevelGenerated` | Simulation | Rendering, UI, FOV | depth, width, height |
| `LevelTransition` | Simulation | Rendering, UI | fromDepth, toDepth |
| `ItemPickedUp` | Simulation | UI, Rendering | entityId, itemTemplateId |
| `ItemUsed` | Simulation | UI, Rendering | entityId, itemTemplateId, effectDescription |
| `ItemDropped` | Simulation | UI, Rendering | entityId, itemTemplateId, posX, posY |
| `EquipmentChanged` | Simulation | UI | entityId, slot, itemTemplateId |
| `FOVUpdated` | Rendering | Rendering, AI | _(none)_ |
| `StatusEffectApplied` | Simulation | Rendering, UI | entityId, effectType, duration |
| `StatusEffectRemoved` | Simulation | Rendering, UI | entityId, effectType |
| `GameOver` | Simulation | UI | finalDepth, turnsSurvived |
| `SaveRequested` | UI | Persistence | slotIndex |
| `SaveCompleted` | Persistence | UI | success |
| `LoadRequested` | UI | Persistence | slotIndex |
| `LoadCompleted` | Persistence | UI, Simulation | success |

### 5.3 Signal Emission Pattern

Every agent MUST follow this exact emit pattern:

```csharp
// In any simulation action's Execute() method:
EventBus.Instance.EmitSignal(EventBus.SignalName.EntityMoved,
    actor.Id.ToString(), oldPos.X, oldPos.Y, newPos.X, newPos.Y);
```

Every agent MUST follow this exact subscription pattern:

```csharp
// In any consumer's _Ready() method:
public override void _Ready()
{
    EventBus.Instance.EntityMoved += OnEntityMoved;
}

private void OnEntityMoved(string entityId, int fromX, int fromY, int toX, int toY)
{
    // Handle the event
}

// ALWAYS unsubscribe in _ExitTree to prevent leaks:
public override void _ExitTree()
{
    EventBus.Instance.EntityMoved -= OnEntityMoved;
}
```

---

## 6. File Ownership Matrix

Legend:
- **O** = Owner (creates and maintains this file)
- **R** = Reader (consumes, never modifies)
- **â€”** = No interaction

Columns: **Arch** = Architecture, **Sim** = Simulation, **Rend** = Rendering, **Gen** = Generation, **AI** = AI, **UI** = UI, **Tools** = Tools, **Pers** = Persistence, **Cont** = Content

### 6.1 Project Root & Config

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `project.godot` | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `.editorconfig` | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `RoguelikeEngine.sln` | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `RoguelikeEngine.csproj` | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `ARCHITECTURE.md` | **O** | R | R | R | R | R | R | R | R |

### 6.2 Shared Contracts (`Core/Contracts/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `Types/Position.cs` | **O** | R | R | R | R | R | R | R | R |
| `Types/EntityId.cs` | **O** | R | R | R | R | R | R | R | R |
| `Types/Enums.cs` | **O** | R | R | R | R | R | R | R | R |
| `Types/Stats.cs` | **O** | R | R | â€” | R | R | â€” | R | R |
| `Types/DamageResult.cs` | **O** | R | R | â€” | â€” | R | â€” | R | â€” |
| `Types/CombatEvent.cs` | **O** | R | â€” | â€” | â€” | R | â€” | R | â€” |
| `Types/ItemData.cs` | **O** | R | R | â€” | â€” | R | R | R | R |
| `IEntity.cs` | **O** | R | R | â€” | R | R | â€” | R | â€” |
| `IAction.cs` | **O** | R | â€” | â€” | R | â€” | â€” | â€” | â€” |
| `IWorldState.cs` | **O** | R | R | R | R | R | R | R | â€” |
| `WorldState.cs` | **O** | R | R | R | R | R | â€” | R | â€” |
| `ITurnScheduler.cs` | **O** | R | â€” | â€” | â€” | R | â€” | â€” | â€” |
| `IGenerator.cs` | **O** | â€” | â€” | R | â€” | â€” | â€” | â€” | â€” |
| `IPathfinder.cs` | **O** | â€” | â€” | â€” | R | â€” | â€” | â€” | â€” |
| `IBrain.cs` | **O** | â€” | â€” | â€” | R | â€” | â€” | â€” | â€” |
| `ISaveManager.cs` | **O** | â€” | â€” | â€” | â€” | â€” | â€” | R | â€” |
| `IFOV.cs` | **O** | â€” | R | â€” | â€” | â€” | â€” | â€” | â€” |
| `IContentDatabase.cs` | **O** | R | â€” | â€” | R | R | R | â€” | R |

### 6.3 Simulation (`Core/Simulation/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `Entity.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `Actions/MoveAction.cs` | â€” | **O** | â€” | â€” | R | â€” | â€” | â€” | â€” |
| `Actions/AttackAction.cs` | â€” | **O** | â€” | â€” | R | â€” | â€” | â€” | â€” |
| `Actions/WaitAction.cs` | â€” | **O** | â€” | â€” | R | â€” | â€” | â€” | â€” |
| `Actions/PickupAction.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `Actions/UseItemAction.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `Actions/DropAction.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `Actions/DescendAction.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `CombatResolver.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `TurnScheduler.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `StatusEffectProcessor.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `GameLoop.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `Inventory.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |

### 6.4 Rendering (`Scripts/World/`, `Scenes/World/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `WorldView.cs` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |
| `WorldView.tscn` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |
| `EntityRenderer.cs` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |
| `FOVRenderer.cs` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |
| `ShadowcastFOV.cs` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |
| `DamageNumberPopup.cs` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |
| `DamageNumberPopup.tscn` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |
| `CameraController.cs` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |
| `AnimationManager.cs` | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” |

### 6.5 Generation (`Core/Generation/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `DungeonGenerator.cs` | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” |
| `RoomPlacer.cs` | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” |
| `CorridorBuilder.cs` | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” |
| `Prefabs/` (room templates) | â€” | â€” | â€” | **O** | â€” | â€” | R | â€” | â€” |
| `LevelValidator.cs` | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” | â€” |

### 6.6 AI (`Core/AI/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `Pathfinder.cs` | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” |
| `Brains/MeleeRusher.cs` | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” |
| `Brains/RangedKiter.cs` | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” |
| `Brains/AmbushBrain.cs` | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” |
| `Brains/PatrolGuard.cs` | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” |
| `BrainFactory.cs` | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” | â€” |

### 6.7 UI (`Scripts/UI/`, `Scenes/UI/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `HUD.cs` / `HUD.tscn` | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” |
| `InventoryUI.cs` / `.tscn` | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” |
| `CombatLog.cs` / `.tscn` | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” |
| `CharacterSheet.cs` / `.tscn` | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” |
| `MainMenu.cs` / `.tscn` | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” |
| `GameOverScreen.cs` / `.tscn` | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” |
| `Minimap.cs` / `.tscn` | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” | â€” |

### 6.8 Tools (`Scripts/Tools/`, `Addons/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `MapEditorPlugin.cs` | â€” | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” |
| `ItemEditor.cs` | â€” | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” |
| `DebugConsole.cs` / `.tscn` | â€” | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” |
| `DebugOverlay.cs` / `.tscn` | â€” | â€” | â€” | â€” | â€” | â€” | **O** | â€” | â€” |

### 6.9 Persistence (`Core/Persistence/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `SaveManager.cs` | â€” | â€” | â€” | â€” | â€” | â€” | â€” | **O** | â€” |
| `SaveFileSchema.cs` | â€” | â€” | â€” | â€” | â€” | â€” | â€” | **O** | â€” |
| `MigrationRunner.cs` | â€” | â€” | â€” | â€” | â€” | â€” | â€” | **O** | â€” |

### 6.10 Content (`Content/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `items.json` | â€” | R | R | â€” | â€” | R | R | R | **O** |
| `enemies.json` | â€” | R | â€” | R | R | â€” | R | R | **O** |
| `abilities.json` | â€” | R | â€” | â€” | R | R | R | â€” | **O** |
| `status_effects.json` | â€” | R | R | â€” | â€” | R | R | â€” | **O** |
| `room_prefabs.json` | â€” | â€” | â€” | R | â€” | â€” | R | â€” | **O** |
| `loot_tables.json` | â€” | R | â€” | â€” | â€” | â€” | R | R | **O** |
| `ContentDatabase.cs` | â€” | R | â€” | â€” | R | R | R | â€” | **O** |

### 6.11 Autoloads (`Scripts/Autoloads/`)

| File | Arch | Sim | Rend | Gen | AI | UI | Tools | Pers | Cont |
|---|---|---|---|---|---|---|---|---|---|
| `EventBus.cs` | **O** | R | R | â€” | R | R | R | R | â€” |
| `GameManager.cs` | â€” | **O** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |
| `Main.tscn` | **O** | â€” | R | â€” | â€” | R | â€” | â€” | â€” |

### 6.12 Tests

| Directory | Owner | Stubs Used |
|---|---|---|
| `Tests/Simulation/` | Simulation | StubGenerator, StubBrain, StubContentDatabase |
| `Tests/Generation/` | Generation | StubWorldFactory |
| `Tests/AI/` | AI | StubWorldFactory, StubPathfinder |
| `Tests/Rendering/` | Rendering | StubWorldFactory |
| `Tests/UI/` | UI | StubWorldFactory, StubSaveManager, StubContentDatabase |
| `Tests/Persistence/` | Persistence | StubWorldFactory |
| `Tests/Content/` | Content | _(none â€” validates own JSON)_ |
| `Tests/Integration/` | Architecture | All stubs replaced with real impls |
| `Tests/Stubs/` | Architecture | _(delivers all stubs)_ |

---

## 7. Dependency DAG & Execution Phases

### 7.1 Dependency Graph (ASCII)

```
                    â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
                    â”‚  ARCHITECTURE   â”‚  â† PHASE 0 (start immediately)
                    â”‚  contracts +    â”‚
                    â”‚  project setup  â”‚
                    â”‚  stubs + bus    â”‚
                    â””â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                             â”‚
              â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
              â”‚              â”‚              â”‚
              â–¼              â–¼              â–¼
       â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
       â”‚ SIMULATION â”‚ â”‚ GENERATION â”‚ â”‚ CONTENT  â”‚  â† PHASE 1
       â”‚  Entity    â”‚ â”‚  Dungeon   â”‚ â”‚  JSON +  â”‚     (after contracts)
       â”‚  Actions   â”‚ â”‚  Generator â”‚ â”‚  DB      â”‚
       â”‚  Turns     â”‚ â”‚  Rooms     â”‚ â”‚  Loader  â”‚
       â””â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”˜
              â”‚              â”‚             â”‚
       â”Œâ”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜             â”‚
       â”‚      â”‚                            â”‚
       â–¼      â–¼              â–¼             â”‚
    â”Œâ”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”   â”‚
    â”‚  AI  â”‚ â”‚ RENDERING â”‚ â”‚PERSISTENCEâ”‚   â”‚  â† PHASE 2
    â”‚ Path â”‚ â”‚ TileMap   â”‚ â”‚ Save/Load â”‚   â”‚     (after WorldState)
    â”‚ Brainâ”‚ â”‚ FOV       â”‚ â”‚ Schema    â”‚   â”‚
    â””â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜   â”‚
                   â”‚                       â”‚
                   â–¼                       â”‚
                â”Œâ”€â”€â”€â”€â”€â”€â”                   â”‚
                â”‚  UI  â”‚  â† PHASE 3        â”‚
                â”‚ HUD  â”‚     (after rendering + signals)
                â”‚ Menusâ”‚                   â”‚
                â””â”€â”€â”¬â”€â”€â”€â”˜                   â”‚
                   â”‚                       â”‚
                   â–¼                       â”‚
               â”Œâ”€â”€â”€â”€â”€â”€â”€â”                   â”‚
               â”‚ TOOLS â”‚  â† PHASE 4        â”‚
               â”‚ Debug â”‚     (after everything stable)
               â”‚ Editorâ”‚                   â”‚
               â””â”€â”€â”€â”€â”€â”€â”€â”˜
```

### 7.2 Phase Breakdown

#### PHASE 0 â€” Immediately (no dependencies)

| Agent | Delivers | Est. Duration |
|---|---|---|
| **Architecture** | `project.godot`, `.sln`, `.csproj`, all contracts in `Core/Contracts/`, `EventBus.cs`, `Main.tscn` skeleton, all stubs in `Tests/Stubs/`, `ARCHITECTURE.md` | 2-4 hours |

#### PHASE 1 â€” After contracts exist (depends on: Architecture)

| Agent | Depends On | Delivers | Parallel With |
|---|---|---|---|
| **Simulation** | All contracts frozen | Entity, Actions, TurnScheduler, CombatResolver, StatusEffectProcessor, GameLoop, Inventory | Generation, Content |
| **Generation** | `IGenerator`, `WorldState`, `Position`, `TileType`, `RoomData`, `LevelData` | DungeonGenerator, RoomPlacer, CorridorBuilder, LevelValidator | Simulation, Content |
| **Content** | `ItemTemplate`, `EnemyTemplate`, `IContentDatabase`, enums | All JSON files, ContentDatabase loader | Simulation, Generation |

#### PHASE 2 â€” After WorldState is functional (depends on: Phase 1)

| Agent | Depends On | Delivers |
|---|---|---|
| **AI** | `IAction`, `IEntity`, `IWorldState`, `IPathfinder`, `IBrain`, action classes (MoveAction, AttackAction, WaitAction) | Pathfinder (A*), MeleeRusher, RangedKiter, AmbushBrain, PatrolGuard, BrainFactory |
| **Rendering** | `IWorldState`, `EventBus` signals, `Position`, `TileType`, `IFOV` | WorldView, EntityRenderer, ShadowcastFOV, FOVRenderer, DamageNumberPopup, CameraController, AnimationManager |
| **Persistence** | `WorldState` (serializable), `IEntity`, `ISaveManager`, `SaveMetadata` | SaveManager, SaveFileSchema, MigrationRunner |

#### PHASE 3 â€” After signals are flowing (depends on: Phase 2)

| Agent | Depends On | Delivers |
|---|---|---|
| **UI** | `EventBus` signals, `IWorldState` (read-only), Rendering scenes exist, `IContentDatabase` | HUD, InventoryUI, CombatLog, CharacterSheet, MainMenu, GameOverScreen, Minimap |

#### PHASE 4 â€” After core gameplay works (depends on: Phase 3)

| Agent | Depends On | Delivers |
|---|---|---|
| **Tools** | Everything stable, `IWorldState`, `IGenerator`, `IContentDatabase`, UI framework patterns | MapEditorPlugin, ItemEditor, DebugConsole, DebugOverlay |

### 7.3 Blocking Analysis

| If THIS agent is late... | ...THESE agents are BLOCKED | Mitigation |
|---|---|---|
| **Architecture** | **ALL** (nothing can start) | Deliver contracts first, polish docs later |
| **Simulation** | AI, Rendering, Persistence (partially blocked), UI (transitively) | Ship Entity + MoveAction + WaitAction first, CombatResolver can come later |
| **Generation** | Nobody fully blocked | Simulation uses StubGenerator; Rendering uses StubWorldFactory |
| **Content** | Nobody fully blocked | All agents use StubContentDatabase |
| **Rendering** | UI (needs scene tree references), Tools (needs visual feedback) | UI can develop against signal mocks without scene tree |
| **AI** | Nobody | Player controls work without AI; enemies just wait |
| **UI** | Tools (needs UI framework patterns) | Tools can stub UI interactions |
| **Persistence** | Nobody | Save is a late feature; core loop doesn't need it |

### 7.4 Critical Path

```
Architecture(4h) â†’ Simulation(16h) â†’ Rendering(12h) â†’ UI(10h) â†’ Integration(4h)
                                                                   â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
                                                                   Total: ~46 hours
```

**Why Simulation is the bottleneck:** AI, Rendering, and Persistence all need a working `WorldState` with real entities. The moment Simulation ships `Entity.cs` + `MoveAction.cs` + `WaitAction.cs`, Phase 2 agents can unblock. Full combat can follow.

**Parallel pipeline saves ~28 hours:** Without parallelism, sequential execution would be ~74 hours. The DAG allows Generation, Content, AI, Rendering, and Persistence to overlap with Simulation.

---

## 8. Stub / Mock Strategy

All stubs live in `Tests/Stubs/` under namespace `Roguelike.Tests.Stubs`. Architecture delivers these alongside contracts. Every interface in `Core/Contracts/` gets a stub so agents can develop and test independently.

### 8.1 StubWorldFactory

```csharp
// Tests/Stubs/StubWorldFactory.cs
using Roguelike.Core;
using System.Collections.Generic;

namespace Roguelike.Tests.Stubs;

/// <summary>
/// Creates pre-configured WorldState instances for testing.
/// All agents use this to get a known-good world without depending on Generator.
/// </summary>
public static class StubWorldFactory
{
    /// <summary>
    /// Creates a 10x10 room: walls on border, floor inside.
    /// No entities placed â€” call CreateWithEntities() for that.
    /// </summary>
    public static WorldState CreateSmallRoom(int width = 10, int height = 10)
    {
        var world = new WorldState();
        world.InitGrid(width, height);
        world.Seed = 12345;
        world.TurnNumber = 1;
        world.Depth = 0;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
            world.SetTile(new Position(x, y), isBorder ? TileType.Wall : TileType.Floor);
        }

        return world;
    }

    /// <summary>
    /// Creates a 10x10 room with a player at (5,5) and an enemy at (7,5).
    /// Returns all three for test assertions.
    /// </summary>
    public static (WorldState world, IEntity player, IEntity enemy) CreateWithEntities()
    {
        var world = CreateSmallRoom();

        var player = new StubEntity
        {
            Id = EntityId.New(),
            Name = "Player",
            Position = new Position(5, 5),
            Stats = new Stats
            {
                HP = 100, MaxHP = 100,
                Attack = 10, Defense = 5,
                Accuracy = 80, Evasion = 10,
                Speed = 100, ViewRadius = 8
            },
            Faction = Faction.Player,
            BlocksMovement = true,
        };

        var enemy = new StubEntity
        {
            Id = EntityId.New(),
            Name = "Test Goblin",
            Position = new Position(7, 5),
            Stats = new Stats
            {
                HP = 30, MaxHP = 30,
                Attack = 5, Defense = 2,
                Accuracy = 70, Evasion = 5,
                Speed = 80, ViewRadius = 6
            },
            Faction = Faction.Enemy,
            BlocksMovement = true,
        };

        world.AddEntity(player);
        world.AddEntity(enemy);
        world.Player = player;

        return (world, player, enemy);
    }
}
```

### 8.2 StubEntity

```csharp
// Tests/Stubs/StubEntity.cs
using System;
using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

/// <summary>
/// Minimal IEntity implementation backed by a Dictionary for components.
/// All agents use this in their unit tests.
/// </summary>
public sealed class StubEntity : IEntity
{
    public EntityId Id { get; init; }
    public string Name { get; set; } = "Stub";
    public Position Position { get; set; }
    public Stats Stats { get; init; } = new();
    public Faction Faction { get; init; }
    public bool BlocksMovement { get; init; } = true;
    public bool BlocksSight { get; init; } = false;
    public bool IsAlive => Stats.IsAlive;

    private readonly Dictionary<Type, object> _components = new();

    public bool HasComponent<T>() where T : class =>
        _components.ContainsKey(typeof(T));

    public T? GetComponent<T>() where T : class =>
        _components.TryGetValue(typeof(T), out var c) ? (T)c : null;

    public void SetComponent<T>(T component) where T : class =>
        _components[typeof(T)] = component;

    public void RemoveComponent<T>() where T : class =>
        _components.Remove(typeof(T));
}
```

### 8.3 StubGenerator

```csharp
// Tests/Stubs/StubGenerator.cs
using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

/// <summary>
/// Generates a fixed 10x10 room for deterministic testing.
/// Walls on border, floor inside, stairs down at (8,8).
/// Player spawn at (5,5), two enemy spawns, one item spawn.
/// </summary>
public sealed class StubGenerator : IGenerator
{
    public LevelData GenerateLevel(WorldState world, int seed, int depth)
    {
        world.InitGrid(10, 10);

        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
        {
            bool isBorder = x == 0 || y == 0 || x == 9 || y == 9;
            world.SetTile(new Position(x, y), isBorder ? TileType.Wall : TileType.Floor);
        }

        world.SetTile(new Position(8, 8), TileType.StairsDown);

        return new LevelData(
            PlayerSpawn: new Position(5, 5),
            StairsDown: new Position(8, 8),
            EnemySpawns: new List<Position> { new(2, 2), new(6, 6) },
            ItemSpawns: new List<Position> { new(3, 3) },
            Rooms: new List<RoomData> { new(1, 1, 8, 8, new Position(4, 4)) }
        );
    }

    public IReadOnlyList<string> ValidateLevel(IWorldState world, LevelData data) => [];
}
```

### 8.4 StubPathfinder

```csharp
// Tests/Stubs/StubPathfinder.cs
using System.Collections.Generic;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

/// <summary>
/// Returns a straight-line path from start to goal, ignoring walls.
/// Moves diagonally toward goal one step at a time.
/// Good enough for testing AI brain logic without real A*.
/// </summary>
public sealed class StubPathfinder : IPathfinder
{
    public IReadOnlyList<Position> FindPath(
        Position start, Position goal, IWorldState world, int maxLength = 50)
    {
        var path = new List<Position> { start };
        var current = start;
        int steps = 0;

        while (current != goal && steps < maxLength)
        {
            int dx = goal.X.CompareTo(current.X);
            int dy = goal.Y.CompareTo(current.Y);
            current = new Position(current.X + dx, current.Y + dy);
            path.Add(current);
            steps++;
        }

        return path;
    }

    public bool HasPath(Position start, Position goal, IWorldState world, int maxLength = 50)
        => true;

    public IReadOnlyDictionary<Position, int> GetReachable(
        Position origin, int range, IWorldState world)
    {
        var result = new Dictionary<Position, int>();
        for (int dy = -range; dy <= range; dy++)
        for (int dx = -range; dx <= range; dx++)
        {
            var pos = origin.Offset(dx, dy);
            int dist = origin.DistanceTo(pos);
            if (dist <= range && world.InBounds(pos))
                result[pos] = dist;
        }
        return result;
    }
}
```

### 8.5 StubBrain

```csharp
// Tests/Stubs/StubBrain.cs
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

/// <summary>
/// AI brain that always returns WaitAction. Used by Simulation to advance turns
/// without depending on real AI brain implementations.
/// </summary>
public sealed class StubBrain : IBrain
{
    public IAction DecideAction(IEntity self, IWorldState world, IPathfinder pathfinder)
    {
        return new StubWaitAction(self.Id);
    }
}

/// <summary>
/// Minimal WaitAction that always succeeds. Energy cost = 1000 (standard action).
/// </summary>
public sealed class StubWaitAction : IAction
{
    public EntityId ActorId { get; }
    public ActionType Type => ActionType.Wait;

    public StubWaitAction(EntityId actorId) => ActorId = actorId;

    public ActionResult Validate(IWorldState world) => ActionResult.Success;
    public ActionOutcome Execute(WorldState world) => ActionOutcome.Ok();
    public int GetEnergyCost() => 1000;
}
```

### 8.6 StubSaveManager

```csharp
// Tests/Stubs/StubSaveManager.cs
using System;
using System.Threading.Tasks;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

/// <summary>
/// In-memory save manager with 3 slots. Never touches the filesystem.
/// Used by UI agent for testing save/load UI flows.
/// </summary>
public sealed class StubSaveManager : ISaveManager
{
    private readonly WorldState?[] _slots = new WorldState?[3];

    public Task<bool> SaveGame(WorldState world, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length)
            return Task.FromResult(false);
        _slots[slotIndex] = world;
        return Task.FromResult(true);
    }

    public Task<WorldState?> LoadGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length)
            return Task.FromResult<WorldState?>(null);
        return Task.FromResult(_slots[slotIndex]);
    }

    public bool HasSave(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _slots.Length && _slots[slotIndex] != null;

    public void DeleteSave(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _slots.Length)
            _slots[slotIndex] = null;
    }

    public SaveMetadata? GetSaveMetadata(int slotIndex) =>
        _slots[slotIndex] is { } w
            ? new SaveMetadata(slotIndex, w.Depth, w.TurnNumber,
                "Test Player", DateTime.UtcNow, 1)
            : null;
}
```

### 8.7 StubContentDatabase

```csharp
// Tests/Stubs/StubContentDatabase.cs
using System.Collections.Generic;
using System.Linq;
using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

/// <summary>
/// Hardcoded content database with 3 items and 2 enemies.
/// Enough for all agents to test item/enemy-related logic without JSON files.
/// </summary>
public sealed class StubContentDatabase : IContentDatabase
{
    private readonly List<ItemTemplate> _items = new()
    {
        new ItemTemplate(
            TemplateId: "health_potion",
            DisplayName: "Health Potion",
            Description: "Restores 25 HP",
            Category: ItemCategory.Consumable,
            Slot: EquipSlot.None,
            StatModifiers: new Dictionary<string, int>(),
            UseEffect: "heal:25",
            MaxCharges: 1,
            MaxStack: 5
        ),
        new ItemTemplate(
            TemplateId: "sword_iron",
            DisplayName: "Iron Sword",
            Description: "A sturdy blade",
            Category: ItemCategory.Weapon,
            Slot: EquipSlot.MainHand,
            StatModifiers: new Dictionary<string, int> { ["Attack"] = 5 },
            UseEffect: null,
            MaxCharges: -1,
            MaxStack: 1
        ),
        new ItemTemplate(
            TemplateId: "shield_wood",
            DisplayName: "Wooden Shield",
            Description: "Basic protection",
            Category: ItemCategory.Armor,
            Slot: EquipSlot.OffHand,
            StatModifiers: new Dictionary<string, int> { ["Defense"] = 3 },
            UseEffect: null,
            MaxCharges: -1,
            MaxStack: 1
        ),
    };

    private readonly List<EnemyTemplate> _enemies = new()
    {
        new EnemyTemplate(
            TemplateId: "goblin",
            DisplayName: "Goblin",
            Description: "A sneaky green creature",
            BaseStats: new Stats
            {
                HP = 20, MaxHP = 20,
                Attack = 4, Defense = 1,
                Accuracy = 75, Evasion = 10,
                Speed = 90, ViewRadius = 6
            },
            BrainType: "melee_rusher",
            Faction: Faction.Enemy,
            MinDepth: 0, MaxDepth: 5,
            SpawnWeight: 10,
            LootTableId: null
        ),
        new EnemyTemplate(
            TemplateId: "skeleton",
            DisplayName: "Skeleton",
            Description: "Rattling bones",
            BaseStats: new Stats
            {
                HP = 35, MaxHP = 35,
                Attack = 6, Defense = 3,
                Accuracy = 70, Evasion = 5,
                Speed = 70, ViewRadius = 8
            },
            BrainType: "patrol_guard",
            Faction: Faction.Enemy,
            MinDepth: 1, MaxDepth: 8,
            SpawnWeight: 8,
            LootTableId: null
        ),
    };

    public ItemTemplate? GetItem(string templateId) =>
        _items.FirstOrDefault(i => i.TemplateId == templateId);

    public IReadOnlyList<ItemTemplate> AllItems => _items;

    public EnemyTemplate? GetEnemy(string templateId) =>
        _enemies.FirstOrDefault(e => e.TemplateId == templateId);

    public IReadOnlyList<EnemyTemplate> AllEnemies => _enemies;

    public IReadOnlyList<EnemyTemplate> GetEnemiesForDepth(int depth) =>
        _enemies.Where(e => depth >= e.MinDepth && depth <= e.MaxDepth).ToList();

    public IReadOnlyList<ItemTemplate> GetItemsForDepth(int depth) =>
        _items; // All items available at all depths for testing
}
```

### 8.8 Per-Agent Stub Usage Matrix

| Agent | StubWorldFactory | StubEntity | StubGenerator | StubPathfinder | StubBrain | StubSaveManager | StubContentDB |
|---|---|---|---|---|---|---|---|
| **Simulation** | âœ“ | âœ“ | âœ“ | â€” | âœ“ | â€” | âœ“ |
| **Rendering** | âœ“ | âœ“ | â€” | â€” | â€” | â€” | â€” |
| **Generation** | âœ“ | â€” | â€” | â€” | â€” | â€” | â€” |
| **AI** | âœ“ | âœ“ | â€” | âœ“ | â€” | â€” | â€” |
| **UI** | âœ“ | âœ“ | â€” | â€” | â€” | âœ“ | âœ“ |
| **Tools** | âœ“ | âœ“ | âœ“ | â€” | â€” | â€” | âœ“ |
| **Persistence** | âœ“ | âœ“ | â€” | â€” | â€” | â€” | â€” |
| **Content** | â€” | â€” | â€” | â€” | â€” | â€” | â€” |

### 8.9 Integration Handoff Sequence

Replace stubs with real implementations in this order. Each step should pass all existing tests before proceeding to the next.

```
Step  Milestone                              Replaces Stub              Validates
â”€â”€â”€â”€  â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€  â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
  1   Architecture delivers contracts+stubs  (nothing â€” stubs ARE       All agents start
                                              the initial delivery)     development

  2   Simulation delivers Entity + Actions   StubEntity (partially)     Entities move, attack,
      + TurnScheduler + CombatResolver       StubWaitAction             wait. Turns advance.

  3   Generation delivers DungeonGenerator   StubGenerator              Random levels generated
                                                                        and validated.

  4   Content delivers JSON + Database       StubContentDatabase        Items and enemies load
                                                                        from JSON. Templates
                                                                        match contract shapes.

  5   AI delivers Pathfinder                 StubPathfinder             A* returns valid paths,
                                                                        avoids walls.

  6   AI delivers Brain implementations      StubBrain                  Enemies chase, kite,
                                                                        patrol, and fight.

  7   Persistence delivers SaveManager       StubSaveManager            Full world serializes
                                                                        to disk and loads back
                                                                        identically.

  8   Integration test: full gameplay loop   (all stubs removed)        Playerâ†’AIâ†’Combatâ†’
                                                                        Renderâ†’Save round-trip
                                                                        passes.
```

---

## 9. Content Schemas

All content files live in `Content/` at the project root. The Content agent owns these files exclusively. All other agents read them via `IContentDatabase`.

### 9.1 `Content/items.json`

```json
{
  "$schema": "roguelike-items-v1",
  "version": 1,
  "items": [
    {
      "id": "sword_iron",
      "name": "Iron Sword",
      "description": "A sturdy iron blade. Reliable if unexciting.",
      "type": "weapon",
      "slot": "main_hand",
      "stats": {
        "damage_min": 3,
        "damage_max": 7,
        "accuracy": 85,
        "speed_modifier": 0,
        "crit_chance": 5
      },
      "effects": [],
      "rarity": "common",
      "value": 50,
      "weight": 3.0,
      "requirements": { "level": 1, "strength": 3 },
      "sprite_path": "res://assets/sprites/items/sword_iron.png",
      "sprite_atlas_coords": [0, 0]
    },
    {
      "id": "sword_flame",
      "name": "Flamebrand",
      "description": "Fire licks along the blade's edge, eager for flesh.",
      "type": "weapon",
      "slot": "main_hand",
      "stats": {
        "damage_min": 5,
        "damage_max": 12,
        "accuracy": 80,
        "speed_modifier": -100,
        "crit_chance": 8
      },
      "effects": [
        { "type": "on_hit", "status_effect": "burning", "chance": 30, "duration": 3 }
      ],
      "rarity": "rare",
      "value": 350,
      "weight": 3.5,
      "requirements": { "level": 5, "strength": 5 },
      "sprite_path": "res://assets/sprites/items/sword_flame.png",
      "sprite_atlas_coords": [1, 0]
    },
    {
      "id": "dagger_venom",
      "name": "Viper Fang",
      "description": "A curved dagger dripping with paralytic venom.",
      "type": "weapon",
      "slot": "main_hand",
      "stats": {
        "damage_min": 2,
        "damage_max": 5,
        "accuracy": 95,
        "speed_modifier": 200,
        "crit_chance": 15
      },
      "effects": [
        { "type": "on_hit", "status_effect": "poisoned", "chance": 40, "duration": 5 }
      ],
      "rarity": "uncommon",
      "value": 200,
      "weight": 1.0,
      "requirements": { "level": 3, "dexterity": 5 },
      "sprite_path": "res://assets/sprites/items/dagger_venom.png",
      "sprite_atlas_coords": [2, 0]
    },
    {
      "id": "shield_wooden",
      "name": "Wooden Shield",
      "description": "Splinters easily, but better than bare skin.",
      "type": "armor",
      "slot": "off_hand",
      "stats": {
        "defense": 3,
        "block_chance": 15,
        "speed_modifier": -50
      },
      "effects": [],
      "rarity": "common",
      "value": 30,
      "weight": 4.0,
      "requirements": { "level": 1, "strength": 2 },
      "sprite_path": "res://assets/sprites/items/shield_wooden.png",
      "sprite_atlas_coords": [0, 1]
    },
    {
      "id": "armor_chainmail",
      "name": "Chainmail",
      "description": "Interlocking iron rings ward off slashing blows.",
      "type": "armor",
      "slot": "body",
      "stats": {
        "defense": 6,
        "speed_modifier": -200,
        "magic_resist": 0
      },
      "effects": [],
      "rarity": "uncommon",
      "value": 180,
      "weight": 12.0,
      "requirements": { "level": 3, "strength": 6 },
      "sprite_path": "res://assets/sprites/items/armor_chainmail.png",
      "sprite_atlas_coords": [1, 1]
    },
    {
      "id": "helm_iron",
      "name": "Iron Helm",
      "description": "Protects the skull at the cost of peripheral vision.",
      "type": "armor",
      "slot": "head",
      "stats": {
        "defense": 2,
        "speed_modifier": -50
      },
      "effects": [],
      "rarity": "common",
      "value": 60,
      "weight": 3.0,
      "requirements": { "level": 2, "strength": 3 },
      "sprite_path": "res://assets/sprites/items/helm_iron.png",
      "sprite_atlas_coords": [2, 1]
    },
    {
      "id": "potion_health",
      "name": "Health Potion",
      "description": "A crimson draught. Restores 25 HP.",
      "type": "consumable",
      "slot": "none",
      "stats": {},
      "effects": [
        { "type": "on_use", "action": "heal", "value": 25, "target": "self" }
      ],
      "rarity": "common",
      "value": 25,
      "weight": 0.5,
      "requirements": {},
      "stackable": true,
      "max_stack": 10,
      "sprite_path": "res://assets/sprites/items/potion_health.png",
      "sprite_atlas_coords": [0, 2]
    },
    {
      "id": "potion_haste",
      "name": "Haste Potion",
      "description": "Time slows around you. +50% speed for 10 turns.",
      "type": "consumable",
      "slot": "none",
      "stats": {},
      "effects": [
        { "type": "on_use", "action": "apply_status", "status_effect": "haste", "duration": 10, "target": "self" }
      ],
      "rarity": "uncommon",
      "value": 75,
      "weight": 0.5,
      "requirements": {},
      "stackable": true,
      "max_stack": 5,
      "sprite_path": "res://assets/sprites/items/potion_haste.png",
      "sprite_atlas_coords": [1, 2]
    },
    {
      "id": "scroll_fireball",
      "name": "Scroll of Fireball",
      "description": "Unfurl to unleash a ball of flame (3-tile radius).",
      "type": "consumable",
      "slot": "none",
      "stats": {},
      "effects": [
        { "type": "on_use", "action": "cast_ability", "ability_id": "fireball", "target": "aimed" }
      ],
      "rarity": "rare",
      "value": 150,
      "weight": 0.1,
      "requirements": {},
      "stackable": true,
      "max_stack": 3,
      "sprite_path": "res://assets/sprites/items/scroll_fireball.png",
      "sprite_atlas_coords": [2, 2]
    },
    {
      "id": "scroll_blink",
      "name": "Scroll of Blink",
      "description": "Teleport to a visible tile within 8 spaces.",
      "type": "consumable",
      "slot": "none",
      "stats": {},
      "effects": [
        { "type": "on_use", "action": "cast_ability", "ability_id": "blink", "target": "aimed" }
      ],
      "rarity": "uncommon",
      "value": 100,
      "weight": 0.1,
      "requirements": {},
      "stackable": true,
      "max_stack": 5,
      "sprite_path": "res://assets/sprites/items/scroll_blink.png",
      "sprite_atlas_coords": [3, 2]
    },
    {
      "id": "ring_regen",
      "name": "Ring of Regeneration",
      "description": "Flesh knits shut between heartbeats.",
      "type": "accessory",
      "slot": "ring",
      "stats": {
        "hp_regen_per_turn": 1
      },
      "effects": [
        { "type": "passive", "action": "regen_hp", "value": 1, "per": "turn" }
      ],
      "rarity": "rare",
      "value": 400,
      "weight": 0.1,
      "requirements": { "level": 4 },
      "sprite_path": "res://assets/sprites/items/ring_regen.png",
      "sprite_atlas_coords": [0, 3]
    },
    {
      "id": "amulet_farsight",
      "name": "Amulet of Farsight",
      "description": "The darkness pulls back at your approach.",
      "type": "accessory",
      "slot": "amulet",
      "stats": {
        "fov_bonus": 3
      },
      "effects": [
        { "type": "passive", "action": "modify_stat", "stat": "fov_range", "value": 3 }
      ],
      "rarity": "uncommon",
      "value": 200,
      "weight": 0.2,
      "requirements": { "level": 2 },
      "sprite_path": "res://assets/sprites/items/amulet_farsight.png",
      "sprite_atlas_coords": [1, 3]
    }
  ]
}
```

#### Item Schema Field Reference

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | âœ… | Unique identifier (snake_case). Maps to `ItemTemplate.TemplateId`. |
| `name` | string | âœ… | Display name shown in UI. |
| `description` | string | âœ… | Flavor text for tooltips. |
| `type` | enum | âœ… | `weapon`, `armor`, `consumable`, `accessory` |
| `slot` | enum | âœ… | `main_hand`, `off_hand`, `body`, `head`, `ring`, `amulet`, `none` |
| `stats` | object | âœ… | Type-specific stat block (can be empty `{}`). Keys are stat names. |
| `effects` | array | âœ… | Array of effect objects (can be empty `[]`). See effect types below. |
| `rarity` | enum | âœ… | `common`, `uncommon`, `rare`, `epic`, `legendary` |
| `value` | int | âœ… | Gold value for shops/loot weighting. |
| `weight` | float | âœ… | Inventory weight in arbitrary units. |
| `requirements` | object | âœ… | Level/stat requirements (can be empty `{}`). |
| `sprite_path` | string | âœ… | Godot resource path to sprite texture. |
| `sprite_atlas_coords` | [int,int] | âœ… | Column, row in sprite atlas. |
| `stackable` | bool | âŒ | Default `false`. Set `true` for consumables. |
| `max_stack` | int | âŒ | Max stack size. Only if `stackable: true`. |

**Effect Types:**

| `type` | Trigger | Fields |
|---|---|---|
| `on_hit` | When this weapon hits a target | `status_effect`, `chance` (%), `duration` (turns) |
| `on_use` | When consumable is used | `action` (heal/apply_status/cast_ability), `value`/`ability_id`, `target` (self/aimed) |
| `passive` | While equipped | `action` (regen_hp/modify_stat), `value`, `stat`/`per` |

---

### 9.2 `Content/enemies.json`

```json
{
  "$schema": "roguelike-enemies-v1",
  "version": 1,
  "enemies": [
    {
      "id": "rat",
      "name": "Giant Rat",
      "description": "Mangy and desperate. Attacks in packs.",
      "stats": {
        "hp": 8,
        "attack": 2,
        "defense": 0,
        "accuracy": 70,
        "evasion": 15,
        "speed": 1200,
        "fov_range": 6,
        "xp_value": 5
      },
      "ai_type": "melee_rush",
      "ai_params": {
        "flee_hp_pct": 0,
        "aggro_range": 6,
        "wander_when_idle": true
      },
      "faction": "Enemy",
      "min_depth": 0,
      "max_depth": 3,
      "spawn_weight": 15,
      "abilities": [],
      "loot_table_id": "rat_loot",
      "tags": ["beast", "early_game"],
      "sprite_path": "res://assets/sprites/enemies/rat.png",
      "sprite_atlas_coords": [0, 0]
    },
    {
      "id": "skeleton",
      "name": "Skeleton Warrior",
      "description": "Bones held together by malice alone.",
      "stats": {
        "hp": 20,
        "attack": 5,
        "defense": 2,
        "accuracy": 75,
        "evasion": 5,
        "speed": 1000,
        "fov_range": 8,
        "xp_value": 15
      },
      "ai_type": "melee_rush",
      "ai_params": {
        "flee_hp_pct": 0,
        "aggro_range": 8,
        "wander_when_idle": true
      },
      "faction": "Enemy",
      "min_depth": 1,
      "max_depth": 6,
      "spawn_weight": 10,
      "abilities": [],
      "loot_table_id": "skeleton_loot",
      "tags": ["undead", "mid_game"],
      "sprite_path": "res://assets/sprites/enemies/skeleton.png",
      "sprite_atlas_coords": [1, 0]
    },
    {
      "id": "goblin_archer",
      "name": "Goblin Archer",
      "description": "Small, green, and worryingly accurate.",
      "stats": {
        "hp": 12,
        "attack": 4,
        "defense": 1,
        "accuracy": 85,
        "evasion": 20,
        "speed": 1100,
        "fov_range": 10,
        "xp_value": 12
      },
      "ai_type": "ranged_kite",
      "ai_params": {
        "preferred_range": 5,
        "flee_hp_pct": 20,
        "min_range": 3,
        "aggro_range": 10,
        "wander_when_idle": true
      },
      "faction": "Enemy",
      "min_depth": 1,
      "max_depth": 5,
      "spawn_weight": 8,
      "abilities": [
        { "ability_id": "arrow_shot", "cooldown": 0, "priority": 1 }
      ],
      "loot_table_id": "goblin_loot",
      "tags": ["goblinoid", "ranged", "early_game"],
      "sprite_path": "res://assets/sprites/enemies/goblin_archer.png",
      "sprite_atlas_coords": [2, 0]
    },
    {
      "id": "orc_brute",
      "name": "Orc Brute",
      "description": "Walls of muscle and fury. Slow but devastating.",
      "stats": {
        "hp": 35,
        "attack": 8,
        "defense": 4,
        "accuracy": 65,
        "evasion": 0,
        "speed": 800,
        "fov_range": 7,
        "xp_value": 25
      },
      "ai_type": "melee_rush",
      "ai_params": {
        "flee_hp_pct": 0,
        "aggro_range": 7,
        "wander_when_idle": false
      },
      "faction": "Enemy",
      "min_depth": 3,
      "max_depth": 8,
      "spawn_weight": 6,
      "abilities": [
        { "ability_id": "war_cry", "cooldown": 5, "priority": 2 },
        { "ability_id": "heavy_slam", "cooldown": 3, "priority": 1 }
      ],
      "loot_table_id": "orc_loot",
      "tags": ["orc", "mid_game", "tank"],
      "sprite_path": "res://assets/sprites/enemies/orc_brute.png",
      "sprite_atlas_coords": [3, 0]
    },
    {
      "id": "wraith",
      "name": "Spectral Wraith",
      "description": "Passes through stone. Drains life with a touch.",
      "stats": {
        "hp": 25,
        "attack": 6,
        "defense": 1,
        "accuracy": 80,
        "evasion": 30,
        "speed": 1300,
        "fov_range": 12,
        "xp_value": 30
      },
      "ai_type": "ambush",
      "ai_params": {
        "flee_hp_pct": 30,
        "aggro_range": 12,
        "phase_through_walls": true,
        "wander_when_idle": true
      },
      "faction": "Enemy",
      "min_depth": 4,
      "max_depth": 99,
      "spawn_weight": 4,
      "abilities": [
        { "ability_id": "life_drain", "cooldown": 2, "priority": 1 },
        { "ability_id": "phase_shift", "cooldown": 4, "priority": 2 }
      ],
      "loot_table_id": "wraith_loot",
      "tags": ["undead", "incorporeal", "late_game"],
      "sprite_path": "res://assets/sprites/enemies/wraith.png",
      "sprite_atlas_coords": [4, 0]
    },
    {
      "id": "slime_acid",
      "name": "Acid Slime",
      "description": "Dissolves gear on contact. Splits when killed.",
      "stats": {
        "hp": 15,
        "attack": 3,
        "defense": 0,
        "accuracy": 60,
        "evasion": 0,
        "speed": 600,
        "fov_range": 4,
        "xp_value": 10
      },
      "ai_type": "melee_rush",
      "ai_params": {
        "flee_hp_pct": 0,
        "aggro_range": 4,
        "wander_when_idle": true,
        "split_on_death": true,
        "split_into": "slime_acid_small"
      },
      "faction": "Enemy",
      "min_depth": 2,
      "max_depth": 7,
      "spawn_weight": 7,
      "abilities": [
        { "ability_id": "acid_splash", "cooldown": 3, "priority": 1 }
      ],
      "loot_table_id": "slime_loot",
      "tags": ["slime", "early_game"],
      "sprite_path": "res://assets/sprites/enemies/slime_acid.png",
      "sprite_atlas_coords": [5, 0]
    }
  ]
}
```

#### AI Type Reference

| `ai_type` | Behavior | Key `ai_params` |
|---|---|---|
| `melee_rush` | Move toward closest enemy-faction target. Attack when adjacent. Never retreats (unless `flee_hp_pct > 0`). | `aggro_range`, `flee_hp_pct`, `wander_when_idle` |
| `ranged_kite` | Maintain `preferred_range` distance. Attack with ranged abilities. Flee if target closer than `min_range`. | `preferred_range`, `min_range`, `flee_hp_pct`, `aggro_range` |
| `ambush` | Stay hidden/idle until target enters `aggro_range`. Alpha-strike with highest-priority ability. Flee when HP below `flee_hp_pct`. | `aggro_range`, `flee_hp_pct`, `phase_through_walls` |
| `support` | Stay near allies. Buff/heal allies when possible. Avoid entering melee range. | `heal_range`, `buff_range`, `flee_hp_pct` |
| `patrol` | Follow waypoints until target enters `aggro_range`, then switch to `melee_rush` behavior. | `patrol_points`, `aggro_range` |

---

### 9.3 `Content/abilities.json`

```json
{
  "$schema": "roguelike-abilities-v1",
  "version": 1,
  "abilities": [
    {
      "id": "arrow_shot",
      "name": "Arrow Shot",
      "description": "Fire an arrow at a target within range.",
      "targeting": {
        "type": "single",
        "range": 8,
        "requires_los": true
      },
      "costs": {
        "energy": 1000
      },
      "effects": [
        {
          "type": "damage",
          "damage_type": "physical",
          "base_value": 4,
          "stat_scaling": { "stat": "attack", "factor": 0.8 }
        }
      ],
      "animation": "projectile_arrow",
      "sfx": "arrow_fire"
    },
    {
      "id": "fireball",
      "name": "Fireball",
      "description": "Hurl a ball of fire that explodes in a 3-tile radius.",
      "targeting": {
        "type": "aoe_circle",
        "range": 8,
        "radius": 3,
        "requires_los": true,
        "hits_allies": true
      },
      "costs": {
        "energy": 1200,
        "mp": 8
      },
      "effects": [
        {
          "type": "damage",
          "damage_type": "fire",
          "base_value": 12,
          "stat_scaling": { "stat": "attack", "factor": 0.5 }
        },
        {
          "type": "apply_status",
          "status_effect": "burning",
          "chance": 40,
          "duration": 3
        }
      ],
      "animation": "explosion_fire",
      "sfx": "fireball_explode"
    },
    {
      "id": "heavy_slam",
      "name": "Heavy Slam",
      "description": "A brutal overhead strike that stuns.",
      "targeting": {
        "type": "single",
        "range": 1,
        "requires_los": true
      },
      "costs": {
        "energy": 1500
      },
      "effects": [
        {
          "type": "damage",
          "damage_type": "physical",
          "base_value": 10,
          "stat_scaling": { "stat": "attack", "factor": 1.2 }
        },
        {
          "type": "apply_status",
          "status_effect": "stunned",
          "chance": 60,
          "duration": 1
        }
      ],
      "animation": "melee_slam",
      "sfx": "heavy_impact"
    },
    {
      "id": "war_cry",
      "name": "War Cry",
      "description": "Bolster nearby allies, weakening nearby enemies.",
      "targeting": {
        "type": "aoe_circle",
        "range": 0,
        "radius": 4,
        "requires_los": false,
        "hits_allies": false,
        "center": "self"
      },
      "costs": {
        "energy": 800
      },
      "effects": [
        {
          "type": "apply_status",
          "status_effect": "weakened",
          "chance": 100,
          "duration": 3,
          "filter": "enemies"
        },
        {
          "type": "apply_status",
          "status_effect": "empowered",
          "chance": 100,
          "duration": 3,
          "filter": "allies"
        }
      ],
      "animation": "shout_ring",
      "sfx": "war_cry"
    },
    {
      "id": "life_drain",
      "name": "Life Drain",
      "description": "Siphon life from a target, healing self.",
      "targeting": {
        "type": "single",
        "range": 1,
        "requires_los": true
      },
      "costs": {
        "energy": 1100
      },
      "effects": [
        {
          "type": "damage",
          "damage_type": "dark",
          "base_value": 6,
          "stat_scaling": { "stat": "attack", "factor": 0.6 }
        },
        {
          "type": "heal_self",
          "value_source": "damage_dealt",
          "factor": 0.5
        }
      ],
      "animation": "drain_dark",
      "sfx": "life_drain"
    },
    {
      "id": "blink",
      "name": "Blink",
      "description": "Teleport to a visible tile within range.",
      "targeting": {
        "type": "tile",
        "range": 8,
        "requires_los": true,
        "requires_walkable": true
      },
      "costs": {
        "energy": 800
      },
      "effects": [
        {
          "type": "teleport",
          "destination": "target_tile"
        }
      ],
      "animation": "teleport_flash",
      "sfx": "blink_pop"
    },
    {
      "id": "phase_shift",
      "name": "Phase Shift",
      "description": "Become incorporeal for 2 turns, phasing through walls.",
      "targeting": {
        "type": "self",
        "range": 0,
        "requires_los": false
      },
      "costs": {
        "energy": 600
      },
      "effects": [
        {
          "type": "apply_status",
          "status_effect": "phased",
          "chance": 100,
          "duration": 2
        }
      ],
      "animation": "ghost_shimmer",
      "sfx": "phase_shift"
    },
    {
      "id": "acid_splash",
      "name": "Acid Splash",
      "description": "Splash acid on adjacent tiles, corroding armor.",
      "targeting": {
        "type": "aoe_circle",
        "range": 0,
        "radius": 1,
        "requires_los": false,
        "center": "self"
      },
      "costs": {
        "energy": 1000
      },
      "effects": [
        {
          "type": "damage",
          "damage_type": "poison",
          "base_value": 3,
          "stat_scaling": { "stat": "attack", "factor": 0.4 }
        },
        {
          "type": "apply_status",
          "status_effect": "corroded",
          "chance": 50,
          "duration": 5
        }
      ],
      "animation": "acid_burst",
      "sfx": "acid_sizzle"
    }
  ]
}
```

#### Targeting Type Reference

| `type` | Description | Key Fields |
|---|---|---|
| `self` | Targets caster only. No aiming required. | `range`: always 0 |
| `single` | One entity within range + line of sight. | `range`, `requires_los` |
| `tile` | Target a specific walkable tile (for movement abilities). | `range`, `requires_los`, `requires_walkable` |
| `aoe_circle` | Circle AoE. If `center` is `"self"`, originates at caster. Otherwise, aimed at a tile. | `range`, `radius`, `hits_allies`, `center` |
| `aoe_line` | Line from caster in a cardinal/diagonal direction. | `range` (= line length), `width` |
| `aoe_cone` | Cone in a direction from caster. | `range`, `arc` (degrees) |

#### Effect Type Reference

| `type` | Description | Key Fields |
|---|---|---|
| `damage` | Deal damage to targets. | `damage_type`, `base_value`, `stat_scaling.stat`, `stat_scaling.factor` |
| `apply_status` | Apply a status effect. | `status_effect` (id), `chance` (%), `duration` (turns), `filter` (enemies/allies/all) |
| `heal_self` | Heal the caster. | `value_source` (flat/damage_dealt), `factor` |
| `teleport` | Move caster to target tile. | `destination` (target_tile) |

**Damage formula:** `finalDamage = base_value + (caster.Stats[stat] * factor)`

---

### 9.4 `Content/status_effects.json`

```json
{
  "$schema": "roguelike-status-effects-v1",
  "version": 1,
  "status_effects": [
    {
      "id": "poisoned",
      "name": "Poisoned",
      "description": "Taking damage each turn from toxins.",
      "icon_path": "res://assets/sprites/ui/status_poison.png",
      "duration_type": "turns",
      "default_duration": 5,
      "stackable": false,
      "refreshable": true,
      "tick_timing": "start_of_turn",
      "tick_effects": [
        { "type": "damage", "damage_type": "poison", "value": 2 }
      ],
      "stat_modifiers": [],
      "flags": [],
      "on_apply_effects": [],
      "on_expire_effects": [],
      "color_tint": "#44FF4488"
    },
    {
      "id": "burning",
      "name": "Burning",
      "description": "On fire! Taking fire damage each turn.",
      "icon_path": "res://assets/sprites/ui/status_burn.png",
      "duration_type": "turns",
      "default_duration": 3,
      "stackable": false,
      "refreshable": true,
      "tick_timing": "end_of_turn",
      "tick_effects": [
        { "type": "damage", "damage_type": "fire", "value": 3 }
      ],
      "stat_modifiers": [],
      "flags": [],
      "on_apply_effects": [
        { "type": "remove_status", "status_id": "frozen" }
      ],
      "on_expire_effects": [],
      "color_tint": "#FF660088"
    },
    {
      "id": "stunned",
      "name": "Stunned",
      "description": "Cannot act this turn.",
      "icon_path": "res://assets/sprites/ui/status_stun.png",
      "duration_type": "turns",
      "default_duration": 1,
      "stackable": false,
      "refreshable": false,
      "tick_timing": "none",
      "tick_effects": [],
      "stat_modifiers": [],
      "flags": ["skip_turn"],
      "on_apply_effects": [],
      "on_expire_effects": [],
      "color_tint": "#FFFF0088"
    },
    {
      "id": "haste",
      "name": "Haste",
      "description": "Moving faster. +50% speed.",
      "icon_path": "res://assets/sprites/ui/status_haste.png",
      "duration_type": "turns",
      "default_duration": 10,
      "stackable": false,
      "refreshable": true,
      "tick_timing": "none",
      "tick_effects": [],
      "stat_modifiers": [
        { "stat": "speed", "operation": "multiply", "value": 1.5 }
      ],
      "flags": [],
      "on_apply_effects": [],
      "on_expire_effects": [],
      "color_tint": "#00CCFF88"
    },
    {
      "id": "weakened",
      "name": "Weakened",
      "description": "Attack power reduced by 30%.",
      "icon_path": "res://assets/sprites/ui/status_weak.png",
      "duration_type": "turns",
      "default_duration": 3,
      "stackable": false,
      "refreshable": true,
      "tick_timing": "none",
      "tick_effects": [],
      "stat_modifiers": [
        { "stat": "attack", "operation": "multiply", "value": 0.7 }
      ],
      "flags": [],
      "on_apply_effects": [],
      "on_expire_effects": [],
      "color_tint": "#88444488"
    },
    {
      "id": "empowered",
      "name": "Empowered",
      "description": "Attack power increased by 30%.",
      "icon_path": "res://assets/sprites/ui/status_empower.png",
      "duration_type": "turns",
      "default_duration": 3,
      "stackable": false,
      "refreshable": true,
      "tick_timing": "none",
      "tick_effects": [],
      "stat_modifiers": [
        { "stat": "attack", "operation": "multiply", "value": 1.3 }
      ],
      "flags": [],
      "on_apply_effects": [],
      "on_expire_effects": [],
      "color_tint": "#FFAA0088"
    },
    {
      "id": "corroded",
      "name": "Corroded",
      "description": "Armor integrity compromised. Defense reduced.",
      "icon_path": "res://assets/sprites/ui/status_corrode.png",
      "duration_type": "turns",
      "default_duration": 5,
      "stackable": true,
      "max_stacks": 3,
      "refreshable": true,
      "tick_timing": "none",
      "tick_effects": [],
      "stat_modifiers": [
        { "stat": "defense", "operation": "add", "value": -2 }
      ],
      "flags": [],
      "on_apply_effects": [],
      "on_expire_effects": [],
      "color_tint": "#66CC0088"
    },
    {
      "id": "frozen",
      "name": "Frozen",
      "description": "Encased in ice. Cannot move but defense is boosted.",
      "icon_path": "res://assets/sprites/ui/status_frozen.png",
      "duration_type": "turns",
      "default_duration": 2,
      "stackable": false,
      "refreshable": false,
      "tick_timing": "none",
      "tick_effects": [],
      "stat_modifiers": [
        { "stat": "defense", "operation": "add", "value": 5 },
        { "stat": "speed", "operation": "set", "value": 0 }
      ],
      "flags": ["skip_turn"],
      "on_apply_effects": [
        { "type": "remove_status", "status_id": "burning" }
      ],
      "on_expire_effects": [],
      "color_tint": "#88CCFF88"
    },
    {
      "id": "phased",
      "name": "Phased",
      "description": "Incorporeal. Can move through walls.",
      "icon_path": "res://assets/sprites/ui/status_phased.png",
      "duration_type": "turns",
      "default_duration": 2,
      "stackable": false,
      "refreshable": false,
      "tick_timing": "none",
      "tick_effects": [],
      "stat_modifiers": [],
      "flags": ["phase_through_walls", "immune_physical"],
      "on_apply_effects": [],
      "on_expire_effects": [],
      "color_tint": "#AAAAFF44"
    }
  ]
}
```

#### Stat Modifier Operation Table

| `operation` | Description | Example | Application Order |
|---|---|---|---|
| `add` | Add value to base stat. Stacks additively if multiple sources. | `defense + (-2) = defense - 2` | 1st (applied to base) |
| `multiply` | Multiply stat by value after all adds. | `speed * 1.5` | 2nd (applied after adds) |
| `set` | Override stat to an absolute value. Wins over add/multiply. | `speed = 0` | 3rd (final override) |

**Resolution order:** `base + sum(adds)` â†’ `result * product(multiplies)` â†’ `set` overrides (if any).

#### Status Effect Flags

| Flag | Effect |
|---|---|
| `skip_turn` | Entity loses its turn (energy is still consumed). |
| `phase_through_walls` | Entity can move through Wall tiles. |
| `immune_physical` | Physical damage type is reduced to 0. |

---

### 9.5 `Content/room_prefabs.json`

```json
{
  "$schema": "roguelike-room-prefabs-v1",
  "version": 1,
  "tile_legend": {
    "#": "wall",
    ".": "floor",
    "+": "door",
    ">": "stairs_down",
    "<": "stairs_up",
    "~": "water",
    "^": "trap",
    "S": "spawn_enemy",
    "I": "spawn_item",
    "P": "spawn_player",
    "C": "chest",
    " ": "void"
  },
  "rooms": [
    {
      "id": "start_room",
      "name": "Starting Chamber",
      "tags": ["start", "guaranteed"],
      "width": 9,
      "height": 9,
      "min_depth": 1,
      "max_depth": 1,
      "layout": [
        "#########",
        "#.......#",
        "#.......#",
        "#...P...#",
        "#.......#",
        "#.......#",
        "#.......#",
        "#.......#",
        "####+####"
      ],
      "doors": [
        { "x": 4, "y": 8, "direction": "south" }
      ],
      "spawn_points": [
        { "x": 4, "y": 3, "type": "player" }
      ],
      "fixed_entities": []
    },
    {
      "id": "corridor_h",
      "name": "Horizontal Corridor",
      "tags": ["corridor"],
      "width": 7,
      "height": 3,
      "min_depth": 1,
      "max_depth": 99,
      "layout": [
        "#######",
        "+.....+",
        "#######"
      ],
      "doors": [
        { "x": 0, "y": 1, "direction": "west" },
        { "x": 6, "y": 1, "direction": "east" }
      ],
      "spawn_points": [],
      "fixed_entities": []
    },
    {
      "id": "treasure_room",
      "name": "Treasure Vault",
      "tags": ["loot", "rare"],
      "width": 7,
      "height": 7,
      "min_depth": 2,
      "max_depth": 99,
      "layout": [
        "#######",
        "#..I..#",
        "#.....#",
        "+..C..+",
        "#.....#",
        "#..I..#",
        "#######"
      ],
      "doors": [
        { "x": 0, "y": 3, "direction": "west" },
        { "x": 6, "y": 3, "direction": "east" }
      ],
      "spawn_points": [
        { "x": 3, "y": 1, "type": "item" },
        { "x": 3, "y": 5, "type": "item" },
        { "x": 3, "y": 3, "type": "chest" }
      ],
      "fixed_entities": [],
      "item_quality_bonus": 2
    },
    {
      "id": "ambush_room",
      "name": "Shadowed Hall",
      "tags": ["combat", "ambush"],
      "width": 11,
      "height": 9,
      "min_depth": 2,
      "max_depth": 99,
      "layout": [
        "###########",
        "#....S....#",
        "#.#.....#.#",
        "#.#.....#.#",
        "+....I....+",
        "#.#.....#.#",
        "#.#.....#.#",
        "#....S....#",
        "###########"
      ],
      "doors": [
        { "x": 0, "y": 4, "direction": "west" },
        { "x": 10, "y": 4, "direction": "east" }
      ],
      "spawn_points": [
        { "x": 5, "y": 1, "type": "enemy" },
        { "x": 5, "y": 7, "type": "enemy" },
        { "x": 5, "y": 4, "type": "item" }
      ],
      "fixed_entities": [],
      "enemy_count_bonus": 1
    },
    {
      "id": "boss_arena",
      "name": "Boss Arena",
      "tags": ["boss", "guaranteed"],
      "width": 15,
      "height": 13,
      "min_depth": 5,
      "max_depth": 99,
      "layout": [
        "###############",
        "#.............#",
        "#.###.....###.#",
        "#.#.........#.#",
        "#.#.........#.#",
        "#.....S.S.....#",
        "+......S......+",
        "#.....S.S.....#",
        "#.#.........#.#",
        "#.#.........#.#",
        "#.###.....###.#",
        "#......>......#",
        "###############"
      ],
      "doors": [
        { "x": 0, "y": 6, "direction": "west" },
        { "x": 14, "y": 6, "direction": "east" }
      ],
      "spawn_points": [
        { "x": 6, "y": 5, "type": "enemy" },
        { "x": 8, "y": 5, "type": "enemy" },
        { "x": 7, "y": 6, "type": "enemy_boss" },
        { "x": 6, "y": 7, "type": "enemy" },
        { "x": 8, "y": 7, "type": "enemy" },
        { "x": 7, "y": 11, "type": "stairs_down" }
      ],
      "fixed_entities": [],
      "lock_doors_on_enter": true
    },
    {
      "id": "flooded_passage",
      "name": "Flooded Passage",
      "tags": ["hazard", "mid_game"],
      "width": 9,
      "height": 7,
      "min_depth": 3,
      "max_depth": 99,
      "layout": [
        "#########",
        "#...#...#",
        "#.~.+.~.#",
        "+.~...~.+",
        "#.~.^.~.#",
        "#...#...#",
        "#########"
      ],
      "doors": [
        { "x": 0, "y": 3, "direction": "west" },
        { "x": 8, "y": 3, "direction": "east" },
        { "x": 4, "y": 2, "direction": "internal" }
      ],
      "spawn_points": [
        { "x": 4, "y": 4, "type": "trap", "trap_id": "spike_trap" }
      ],
      "fixed_entities": []
    }
  ]
}
```

#### Tile Legend

| Char | Tile Type | Walkable | Blocks Sight |
|---|---|---|---|
| `#` | Wall | No | Yes |
| `.` | Floor | Yes | No |
| `+` | Door | Yes | No (when open) |
| `>` | Stairs Down | Yes | No |
| `<` | Stairs Up | Yes | No |
| `~` | Water | Yes (slowed) | No |
| `^` | Trap | Yes | No |
| `S` | Floor + enemy spawn | Yes | No |
| `I` | Floor + item spawn | Yes | No |
| `P` | Floor + player spawn | Yes | No |
| `C` | Floor + chest | Yes | No |
| ` ` | Void | No | Yes |

---

### 9.6 `Content/loot_tables.json`

```json
{
  "$schema": "roguelike-loot-tables-v1",
  "version": 1,
  "loot_tables": [
    {
      "id": "rat_loot",
      "description": "Loot dropped by Giant Rats.",
      "rolls": 1,
      "entries": [
        { "item_id": "potion_health", "weight": 15, "count_min": 1, "count_max": 1 },
        { "item_id": null, "weight": 85, "count_min": 0, "count_max": 0 }
      ]
    },
    {
      "id": "skeleton_loot",
      "description": "Loot dropped by Skeleton Warriors.",
      "rolls": 1,
      "entries": [
        { "item_id": "sword_iron", "weight": 10, "count_min": 1, "count_max": 1 },
        { "item_id": "helm_iron", "weight": 8, "count_min": 1, "count_max": 1 },
        { "item_id": "potion_health", "weight": 25, "count_min": 1, "count_max": 1 },
        { "item_id": null, "weight": 57, "count_min": 0, "count_max": 0 }
      ]
    },
    {
      "id": "goblin_loot",
      "description": "Loot dropped by Goblin Archers.",
      "rolls": 1,
      "entries": [
        { "item_id": "potion_health", "weight": 20, "count_min": 1, "count_max": 1 },
        { "item_id": "dagger_venom", "weight": 5, "count_min": 1, "count_max": 1 },
        { "item_id": "potion_haste", "weight": 8, "count_min": 1, "count_max": 1 },
        { "item_id": null, "weight": 67, "count_min": 0, "count_max": 0 }
      ]
    },
    {
      "id": "orc_loot",
      "description": "Loot dropped by Orc Brutes.",
      "rolls": 2,
      "entries": [
        { "item_id": "armor_chainmail", "weight": 12, "count_min": 1, "count_max": 1 },
        { "item_id": "potion_health", "weight": 30, "count_min": 1, "count_max": 2 },
        { "item_id": "shield_wooden", "weight": 10, "count_min": 1, "count_max": 1 },
        { "item_id": "scroll_fireball", "weight": 5, "count_min": 1, "count_max": 1 },
        { "item_id": null, "weight": 43, "count_min": 0, "count_max": 0 }
      ]
    },
    {
      "id": "wraith_loot",
      "description": "Loot dropped by Spectral Wraiths.",
      "rolls": 1,
      "entries": [
        { "item_id": "ring_regen", "weight": 5, "count_min": 1, "count_max": 1 },
        { "item_id": "scroll_blink", "weight": 15, "count_min": 1, "count_max": 1 },
        { "item_id": "amulet_farsight", "weight": 8, "count_min": 1, "count_max": 1 },
        { "item_id": null, "weight": 72, "count_min": 0, "count_max": 0 }
      ]
    },
    {
      "id": "slime_loot",
      "description": "Loot dropped by Acid Slimes.",
      "rolls": 1,
      "entries": [
        { "item_id": "potion_health", "weight": 10, "count_min": 1, "count_max": 1 },
        { "item_id": "scroll_blink", "weight": 5, "count_min": 1, "count_max": 1 },
        { "item_id": null, "weight": 85, "count_min": 0, "count_max": 0 }
      ]
    },
    {
      "id": "floor_loot",
      "description": "Random items found on dungeon floor tiles. Used by Generator for item spawn points.",
      "rolls": 1,
      "entries": [
        { "item_id": "potion_health", "weight": 25, "count_min": 1, "count_max": 2 },
        { "item_id": "potion_haste", "weight": 10, "count_min": 1, "count_max": 1 },
        { "item_id": "scroll_fireball", "weight": 8, "count_min": 1, "count_max": 1 },
        { "item_id": "scroll_blink", "weight": 12, "count_min": 1, "count_max": 1 },
        { "item_id": "sword_iron", "weight": 6, "count_min": 1, "count_max": 1 },
        { "item_id": "shield_wooden", "weight": 6, "count_min": 1, "count_max": 1 },
        { "item_id": "helm_iron", "weight": 5, "count_min": 1, "count_max": 1 },
        { "item_id": "dagger_venom", "weight": 4, "count_min": 1, "count_max": 1 },
        { "item_id": "armor_chainmail", "weight": 3, "count_min": 1, "count_max": 1 },
        { "item_id": "sword_flame", "weight": 2, "count_min": 1, "count_max": 1 },
        { "item_id": "ring_regen", "weight": 2, "count_min": 1, "count_max": 1 },
        { "item_id": "amulet_farsight", "weight": 3, "count_min": 1, "count_max": 1 },
        { "item_id": null, "weight": 14, "count_min": 0, "count_max": 0 }
      ]
    }
  ]
}
```

#### Loot Table Field Reference

| Field | Type | Description |
|---|---|---|
| `id` | string | Unique table ID. Referenced by `enemies.json` `loot_table_id` and Generator. |
| `rolls` | int | Number of independent rolls against this table. Each roll picks one entry. |
| `entries[].item_id` | string? | Item template ID, or `null` for "nothing drops". |
| `entries[].weight` | int | Relative weight. Probability = `weight / sum(all weights)`. |
| `entries[].count_min` | int | Min stack count if selected. |
| `entries[].count_max` | int | Max stack count if selected. Actual count = random in [min, max]. |

#### Loot Resolution Algorithm

```
for i in range(table.rolls):
    totalWeight = sum(entry.weight for entry in table.entries)
    roll = rng.Next(0, totalWeight)
    cumulative = 0
    for entry in table.entries:
        cumulative += entry.weight
        if roll < cumulative:
            if entry.item_id is not null:
                count = rng.Next(entry.count_min, entry.count_max + 1)
                drop(entry.item_id, count)
            break
```

**Note:** The RNG used MUST be the seeded `System.Random` tied to the game seed for deterministic loot. Never use `new Random()` without a seed.


---

## 10. Godot Project Configuration

### 10.1 project.godot key settings (human-readable summary)

| Setting | Value | Rationale |
|---|---|---|
| `config_version` | `5` | Godot 4.4 project format |
| `application/config/name` | `"godotussy"` | Project identifier, matches C# assembly |
| `display/window/size/viewport_width` | `1280` | Scales cleanly to 1080p/1440p/4K |
| `display/window/size/viewport_height` | `720` | 16:9 baseline |
| `display/window/stretch/mode` | `"viewport"` | Pixel-perfect rendering |
| `display/window/stretch/aspect` | `"keep"` | Maintain aspect ratio, letterbox excess |
| `display/window/stretch/scale_mode` | `"integer"` | No sub-pixel artifacts on pixel art |
| `rendering/textures/canvas_textures/default_texture_filter` | `0` (Nearest) | Crisp pixel art, no bilinear blurring |
| `rendering/environment/defaults/default_clear_color` | `Color(0.05, 0.05, 0.08, 1)` | Dark dungeon atmosphere |
| `display/window/vsync/vsync_mode` | `1` (Enabled) | Prevent screen tearing |
| `dotnet/project/assembly_name` | `"godotussy"` | C# assembly name |
| `dotnet/project/solution_directory` | `"."` | Solution at project root |
| `application/run/main_scene` | `"res://Scenes/Main.tscn"` | Entry point scene |

### 10.2 Autoload Registration Order

Autoloads initialize in registration order. Order matters â€” later autoloads may depend on earlier ones.

| Priority | Name | Path | Purpose |
|---|---|---|---|
| 1 | `GameManager` | `res://Scripts/Autoloads/GameManager.cs` | Master game loop, state machine, turn orchestration |
| 2 | `EventBus` | `res://Scripts/Autoloads/EventBus.cs` | Centralized signal hub â€” all cross-system communication |
| 3 | `ContentDatabase` | `res://Scripts/Autoloads/ContentDatabase.cs` | Loaded JSON content registry (items, enemies, effects) |

Access pattern from any node:

```csharp
var eventBus = GetNode<EventBus>("/root/EventBus");
var content = GetNode<ContentDatabase>("/root/ContentDatabase");
var game = GetNode<GameManager>("/root/GameManager");
```

### 10.3 Render Layers (Z-Index)

| Z-Index | Layer | Content |
|---|---|---|
| 0 | Terrain | Floor tiles, stairs, water, lava |
| 1 | Walls | Wall tiles, doors |
| 2 | Objects | Ground items, decorations |
| 3 | Entities | Player sprite, enemy sprites |
| 4 | Effects | Damage numbers, ability VFX |
| 10 | FOV | Fog-of-war overlay (hidden = opaque black, seen = 50% black) |

UI exists on separate `CanvasLayer` nodes (layer 10 = HUD, layer 20 = Menus, layer 30 = Debug).

---

## 11. Input Map Reference

All input actions registered in `project.godot` under `[input]`. Actions use `InputEventKey` with physical keycodes.

| Action | Key 1 | Key 2 | Key 3 | Purpose |
|---|---|---|---|---|
| `move_up` | W | Up Arrow | Numpad 8 | Move north |
| `move_down` | S | Down Arrow | Numpad 2 | Move south |
| `move_left` | A | Left Arrow | Numpad 4 | Move west |
| `move_right` | D | Right Arrow | Numpad 6 | Move east |
| `move_up_left` | â€” | â€” | Numpad 7 | Diagonal NW |
| `move_up_right` | â€” | â€” | Numpad 9 | Diagonal NE |
| `move_down_left` | â€” | â€” | Numpad 1 | Diagonal SW |
| `move_down_right` | â€” | â€” | Numpad 3 | Diagonal SE |
| `wait_turn` | Period (`.`) | â€” | Numpad 5 | Skip turn (costs standard energy) |
| `interact` | E | â€” | â€” | Open/close doors, use objects |
| `inventory_toggle` | I | â€” | â€” | Open/close inventory panel |
| `character_sheet` | C | â€” | â€” | Open/close character sheet |
| `pickup_item` | G | â€” | â€” | Pick up item on current tile |
| `use_stairs` | Shift+Period (`>`) | â€” | â€” | Descend/ascend stairs |
| `look_mode` | L | â€” | â€” | Enter examine mode (cursor inspection) |
| `fire_ability` | F | â€” | â€” | Enter ability targeting mode |
| `cancel` | Escape | â€” | â€” | Cancel current action / close topmost UI |
| `confirm` | Enter | â€” | â€” | Confirm selection / dialogue advance |
| `quicksave` | F5 | â€” | â€” | Save to quicksave slot |
| `quickload` | F9 | â€” | â€” | Load from quicksave slot |
| `debug_console` | Backtick (`` ` ``) | â€” | â€” | Toggle debug console overlay |
| `minimap_toggle` | M | â€” | â€” | Toggle minimap visibility |
| `message_log` | P | â€” | â€” | Toggle full message log panel |

**Input handling rule**: UI agent's `InputHandler.cs` converts Godot `InputEvent` â†’ `IAction`. During UI focus (inventory open, menu open), movement inputs are suppressed. `cancel` always closes the topmost UI layer first.

---

## 12. Scene Trees

### 12.1 Main.tscn

Root scene. Autoloads (`GameManager`, `EventBus`, `ContentDatabase`) are injected by Godot above `/root` â€” they are NOT children of Main.

```
Main (Node2D)
â”œâ”€â”€ WorldViewContainer (SubViewportContainer) [stretch: true]
â”‚   â””â”€â”€ SubViewport [size: 1280x720]
â”‚       â”œâ”€â”€ Camera2D [script: Scripts/World/CameraController.cs, zoom: Vector2(2,2)]
â”‚       â”œâ”€â”€ TileMapFloor (TileMapLayer) [z_index: 0, tile_set: dungeon_tileset.tres]
â”‚       â”œâ”€â”€ TileMapWalls (TileMapLayer) [z_index: 1, tile_set: dungeon_tileset.tres]
â”‚       â”œâ”€â”€ TileMapObjects (TileMapLayer) [z_index: 2, tile_set: dungeon_tileset.tres]
â”‚       â”œâ”€â”€ EntityLayer (Node2D) [z_index: 3]
â”‚       â”‚   â””â”€â”€ (EntitySprite instances added/removed at runtime by EntityRenderer)
â”‚       â””â”€â”€ TileMapFog (TileMapLayer) [z_index: 10, tile_set: dungeon_tileset.tres]
â”œâ”€â”€ UILayer (CanvasLayer) [layer: 10]
â”‚   â”œâ”€â”€ HUD (MarginContainer) [scene: Scenes/UI/HUD.tscn]
â”‚   â”œâ”€â”€ InventoryUI (PanelContainer) [scene: Scenes/UI/InventoryUI.tscn, visible: false]
â”‚   â”œâ”€â”€ CharacterSheet (PanelContainer) [scene: Scenes/UI/CharacterSheet.tscn, visible: false]
â”‚   â”œâ”€â”€ CombatLog (PanelContainer) [scene: Scenes/UI/CombatLog.tscn]
â”‚   â”œâ”€â”€ Tooltip (PanelContainer) [scene: Scenes/UI/Tooltip.tscn, visible: false]
â”‚   â””â”€â”€ Minimap (SubViewportContainer) [scene: Scenes/UI/Minimap.tscn]
â”œâ”€â”€ MenuLayer (CanvasLayer) [layer: 20]
â”‚   â”œâ”€â”€ MainMenu (Control) [scene: Scenes/UI/MainMenu.tscn]
â”‚   â”œâ”€â”€ PauseMenu (Control) [scene: Scenes/UI/PauseMenu.tscn, visible: false]
â”‚   â””â”€â”€ GameOverScreen (Control) [scene: Scenes/UI/GameOverScreen.tscn, visible: false]
â””â”€â”€ DebugLayer (CanvasLayer) [layer: 30]
    â””â”€â”€ DebugConsole (Control) [scene: Scenes/Tools/DebugConsole.tscn, visible: false]
```

### 12.2 HUD.tscn

```
HUD (MarginContainer) [anchors: full_rect, mouse_filter: Ignore]
â”œâ”€â”€ TopBar (HBoxContainer) [anchors: top, h_size_flags: expand_fill]
â”‚   â”œâ”€â”€ HPBar (TextureProgressBar) [min_size: (200, 20)]
â”‚   â”œâ”€â”€ HPLabel (Label) ["HP: 100/100", font_size: 14]
â”‚   â”œâ”€â”€ Spacer (Control) [h_size_flags: expand_fill]
â”‚   â”œâ”€â”€ FloorLabel (Label) ["Floor: 1"]
â”‚   â””â”€â”€ TurnLabel (Label) ["Turn: 1"]
â””â”€â”€ BottomBar (VBoxContainer) [anchors: bottom-left, custom_min_size: (400, 120)]
    â””â”€â”€ CombatLogText (RichTextLabel) [scroll_following: true, bbcode_enabled: true, fit_content: false]
```

### 12.3 InventoryUI.tscn

```
InventoryUI (PanelContainer) [anchors: center, custom_min_size: (500, 400)]
â””â”€â”€ VBoxContainer
    â”œâ”€â”€ TitleBar (HBoxContainer)
    â”‚   â”œâ”€â”€ Title (Label) ["Inventory"]
    â”‚   â””â”€â”€ CloseButton (Button) ["X"]
    â”œâ”€â”€ HSeparator
    â”œâ”€â”€ ItemGrid (GridContainer) [columns: 5]
    â”‚   â””â”€â”€ (ItemSlot nodes added dynamically â€” each is a TextureButton)
    â”œâ”€â”€ HSeparator
    â”œâ”€â”€ ItemDescription (RichTextLabel) [custom_min_size: (0, 80), bbcode_enabled: true]
    â””â”€â”€ ButtonBar (HBoxContainer)
        â”œâ”€â”€ UseButton (Button) ["Use (U)"]
        â”œâ”€â”€ DropButton (Button) ["Drop (D)"]
        â””â”€â”€ EquipButton (Button) ["Equip (E)"]
```

### 12.4 EntitySprite.tscn

Instantiated per entity by `EntityRenderer`. Pooled â€” hidden when entity dies, recycled on next spawn.

```
EntitySprite (Node2D)
â”œâ”€â”€ Sprite2D [texture: placeholder 16x16, centered: true]
â”œâ”€â”€ HealthBar (TextureProgressBar) [visible: false, position: (0, -12), size: (16, 3)]
â””â”€â”€ StatusIcons (HBoxContainer) [position: (0, -20)]
    â””â”€â”€ (TextureRect children added dynamically per active status effect)
```

### 12.5 MainMenu.tscn

```
MainMenu (Control) [anchors: full_rect]
â”œâ”€â”€ Background (ColorRect) [color: (0.02, 0.02, 0.05), anchors: full_rect]
â”œâ”€â”€ CenterContainer [anchors: center]
â”‚   â””â”€â”€ VBoxContainer
â”‚       â”œâ”€â”€ TitleLabel (Label) ["GODOTUSSY", font_size: 48, h_align: center]
â”‚       â”œâ”€â”€ SubtitleLabel (Label) ["A Roguelike Engine", font_size: 16]
â”‚       â”œâ”€â”€ Spacer (Control) [custom_min_size: (0, 40)]
â”‚       â”œâ”€â”€ NewGameButton (Button) ["New Game", custom_min_size: (200, 40)]
â”‚       â”œâ”€â”€ LoadGameButton (Button) ["Load Game"]
â”‚       â”œâ”€â”€ SettingsButton (Button) ["Settings"]
â”‚       â””â”€â”€ QuitButton (Button) ["Quit"]
```

---

## 13. Agent Summaries

### Agent 1: Architecture

- **Mission**: Set up Godot 4.4 project skeleton, C# solution, directory structure, shared contracts (interfaces + DTOs + enums), autoload singletons, scene stubs, and test infrastructure so all other agents can work in parallel.
- **Owns**: `project.godot`, `godotussy.csproj`, `godotussy.sln`, `.editorconfig`, `.gitignore`, `Core/Interfaces/*`, `Core/DTOs/*`, `Core/Enums/*`, `Scripts/Autoloads/GameManager.cs`, `Scripts/Autoloads/EventBus.cs`, `Scripts/Autoloads/ContentDatabase.cs`, `Scenes/Main.tscn`, `Tests/Stubs/*`
- **Delivers**: Compilable project with all interfaces defined, autoloads running, scene tree skeleton, stub implementations for every interface
- **Done when**: `dotnet build` succeeds, Godot opens and runs the project without errors, `Main.tscn` loads with all scene layers visible, all stubs compile
- **Detailed spec**: `agents/agent-1-architecture.md`

### Agent 2: Simulation

- **Mission**: Implement WorldState (grid + entity registry), all 8 action types (Move, Attack, PickUp, UseItem, Wait, Drop, UseStairs, OpenDoor), energy-based turn scheduler, combat resolver with hit/damage/crit/miss formulas, and status effect processor.
- **Owns**: `Core/Simulation/WorldState.cs`, `Core/Simulation/TurnScheduler.cs`, `Core/Simulation/CombatResolver.cs`, `Core/Simulation/StatusEffectProcessor.cs`, `Core/Simulation/Actions/*.cs`, `Tests/SimulationTests/*`
- **Delivers**: Complete simulation engine with 37+ unit tests covering all actions, turn ordering, combat formulas, and status effect lifecycle
- **Done when**: All actions validate and execute correctly, turn scheduler advances entities by energy/speed, combat resolves with deterministic damage, status effects tick and expire, all tests pass
- **Detailed spec**: `agents/agent-2-simulation.md`

### Agent 3: Rendering

- **Mission**: Render dungeon from WorldState using TileMapLayers, implement symmetric shadowcasting FOV (visible/seen/hidden states), manage entity sprites with pooling, animate movement and attacks via tweens, and implement smooth camera follow.
- **Owns**: `Scripts/World/WorldView.cs`, `Scripts/World/FOVCalculator.cs`, `Scripts/World/EntityRenderer.cs`, `Scripts/World/AnimationController.cs`, `Scripts/World/CameraController.cs`, `Scenes/World/WorldView.tscn`, `Scenes/World/EntitySprite.tscn`, `Assets/Tilesets/dungeon_tileset.tres`, `Tests/RenderingTests/FOVTests.cs`
- **Delivers**: Visible dungeon with fog-of-war, animated entity sprites, smooth camera tracking player
- **Done when**: Map renders correctly from WorldState, FOV is symmetric (if A sees B then B sees A), entity sprites animate movement (0.15s tween) and attacks (0.1s lunge), camera follows player with smoothing
- **Detailed spec**: `agents/agent-3-rendering.md`

### Agent 4: Generation

- **Mission**: Procedurally generate dungeon levels using BSP partitioning, place rooms (prefab or rectangular), connect with L-shaped corridors, place stairs/player/enemies/items, and validate connectivity via flood fill. Fully deterministic given a seed.
- **Owns**: `Core/Generation/DungeonGenerator.cs`, `Core/Generation/BSPTree.cs`, `Core/Generation/RoomPlacer.cs`, `Core/Generation/CorridorBuilder.cs`, `Core/Generation/SpawnPlacer.cs`, `Core/Generation/RoomPrefab.cs`, `Core/Generation/FloodFillValidator.cs`, `Core/Generation/BiomeTheme.cs`, `Content/Rooms/prefabs.json`, `Tests/GenerationTests/*`
- **Delivers**: Deterministic level generator producing connected dungeons with 4+ rooms, 12+ unit tests
- **Done when**: Same seed produces identical level, all spawns reachable from stairs (flood fill validates), minimum 4 rooms per level, retries on connectivity failure (up to 10 attempts with seed+N)
- **Detailed spec**: `agents/agent-4-generation.md`

### Agent 5: AI

- **Mission**: Implement utility-based AI decision engine with 5 states (Idle, Patrol, Chase, Attack, Flee), A* pathfinding on the WorldState grid, and multiple AI profiles (aggressive, cautious, ranged, swarm, boss).
- **Owns**: `Core/AI/AIBrain.cs`, `Core/AI/UtilityScorer.cs`, `Core/AI/AIStateManager.cs`, `Core/AI/Pathfinding.cs`, `Core/AI/AIProfiles.cs`, `Tests/AITests/*`
- **Delivers**: Intelligent enemies with 5 profiles and 12+ unit tests
- **Done when**: Enemies chase visible player, attack when adjacent, flee when HP < 25%, pathfind around walls, respect profiles (ranged keeps distance, cautious waits for allies, swarm flanks)
- **Detailed spec**: `agents/agent-5-ai.md`

### Agent 6: UI

- **Mission**: Build complete game UI: HUD (HP bar, floor/turn display), inventory grid with keyboard navigation, combat log with color-coded BBCode, character sheet, tooltips, main menu, pause menu, game over screen. All UI driven by EventBus signals.
- **Owns**: `Scripts/UI/HUD.cs`, `Scripts/UI/InventoryUI.cs`, `Scripts/UI/CombatLog.cs`, `Scripts/UI/CharacterSheet.cs`, `Scripts/UI/Tooltip.cs`, `Scripts/UI/MainMenu.cs`, `Scripts/UI/PauseMenu.cs`, `Scripts/UI/GameOverScreen.cs`, `Scripts/UI/InputHandler.cs`, `Scenes/UI/*.tscn`
- **Delivers**: Complete game UI navigable entirely by keyboard
- **Done when**: Player can see HP/floor/turn, manage inventory (use/drop/equip), read color-coded combat log, view character stats, navigate all menus with keyboard, tooltips appear on hover/inspect
- **Detailed spec**: `agents/agent-6-ui.md`

### Agent 7: Tools

- **Mission**: Build developer tools: map editor for painting tiles and saving/loading room prefabs, item/enemy editor with form UI for editing JSON content, debug console with cheat commands. Map editor as Godot editor plugin, debug console as in-game overlay.
- **Owns**: `Addons/roguelike_tools/plugin.cfg`, `Addons/roguelike_tools/RoguelikeToolsPlugin.cs`, `Scripts/Tools/MapEditor.cs`, `Scripts/Tools/ItemEditor.cs`, `Scripts/Tools/DebugConsole.cs`, `Scenes/Tools/MapEditor.tscn`, `Scenes/Tools/ItemEditor.tscn`, `Scenes/Tools/DebugConsole.tscn`
- **Delivers**: Tile painting map editor with prefab save/load, JSON content editor, debug console with 12+ commands
- **Done when**: Can paint tiles and save/load room prefabs to JSON, can edit item/enemy stats via form UI, debug console supports: `teleport x y`, `spawn enemy_id`, `godmode`, `kill_all`, `give item_id`, `set_hp N`, `set_floor N`, `reveal_map`, `spawn_stairs`, `list_entities`, `dump_state`, `help`
- **Detailed spec**: `agents/agent-7-tools.md`

### Agent 8: Persistence

- **Mission**: Implement JSON save/load system with 4 slots (3 manual + autosave), save file versioning, integrity validation, and complete state restoration including grid, entities, inventories, status effects, energy, fog-of-war exploration, and ground items.
- **Owns**: `Core/Persistence/SaveManager.cs`, `Core/Persistence/SaveSerializer.cs`, `Core/Persistence/SaveMigrator.cs`, `Core/Persistence/SaveValidator.cs`, `Tests/PersistenceTests/*`
- **Delivers**: Mid-run save/restore with 15+ unit tests covering round-trip fidelity, migration, validation, and edge cases
- **Done when**: Saveâ†’Load round-trip preserves 100% of game state (grid, entities, inventories, effects, energy, explored tiles, ground items), autosaves trigger on floor transition, save version migration works, corrupted saves are rejected gracefully
- **Detailed spec**: `agents/agent-8-persistence.md`

### Agent 9: Content

- **Mission**: Define all game content in JSON: items (weapons, armor, consumables, scrolls), enemies (with AI profiles and spawn weights), status effects, room prefabs, loot tables, and difficulty scaling. Provide balanced stats and content validation.
- **Owns**: `Content/Items/items.json`, `Content/Enemies/enemies.json`, `Content/StatusEffects/effects.json`, `Content/Rooms/prefabs.json`, `Content/LootTables/loot.json`, `Core/Content/ContentLoader.cs`, `Core/Content/LootTableResolver.cs`, `Core/Content/DifficultyScaler.cs`, `Tests/ContentTests/*`
- **Delivers**: 15+ items, 6+ enemies, 8 abilities, 9 status effects, 6 room prefabs, loot tables, difficulty curve, all JSON validated
- **Done when**: ContentDatabase loads all JSON without errors, validation tests pass (no missing references, no impossible stats), content is floor-balanced (early floors survivable, later floors challenging)
- **Detailed spec**: `agents/agent-9-content.md`

---

## 14. Conventions & Rules for All Agents

1. **Namespaces**: `Roguelike.Core` for pure C# (contracts, simulation, generation, AI, persistence, content). `Roguelike.Godot` for Godot scene scripts (rendering, UI, tools, autoloads).
2. **File encoding**: UTF-8 without BOM, LF line endings, 4-space indentation, no trailing whitespace.
3. **Naming**: `PascalCase` for types, methods, properties, and constants. `camelCase` for local variables and parameters. `_camelCase` for private fields. No Hungarian notation.
4. **Determinism**: No `DateTime.Now` (use turn counter). No unseeded `Random()` â€” always `new Random(seed)`. No `Dictionary<K,V>` iteration order reliance â€” use `SortedDictionary` or `OrderBy()` before iterating. No `Task.Run` or threading.
5. **Events**: ALL cross-system state changes emit signals via `EventBus.Instance`. Never mutate world state silently. Rendering, UI, and logging react to events â€” they never poll.
6. **Actions**: ALL gameplay state changes (movement, combat, item use, doors, stairs) go through `IAction.Validate()` â†’ `IAction.Execute()`. No direct HP modification, no direct position assignment outside actions.
7. **Testing**: Every agent delivers tests in `Tests/{AgentName}Tests/`. Use stub implementations from `Tests/Stubs/` for cross-agent dependencies. Test file naming: `{ClassName}Tests.cs`.
8. **File ownership**: NEVER create or modify files in another agent's directory. If you need an interface change, document the need as a `// TODO(Agent-N): need X on IFoo` comment in your own code.
9. **Godot version**: 4.4 with .NET (C# 12). Use `[Signal]` delegate pattern for custom signals. Use `partial class` for all Godot node scripts. Use `[Export]` for inspector properties.
10. **Tile size**: 16Ã—16 pixels. All sprite assets must be 16Ã—16 or multiples thereof.
11. **Grid coordinates**: (0,0) is top-left. X increases rightward, Y increases downward. Flat array index: `y * Width + x`. Position validation: `x >= 0 && x < Width && y >= 0 && y < Height`.
12. **Energy system**: Action threshold = 1000. Standard action cost = 1000. Base speed = 100. Energy gained per round = Speed Ã— 10. An entity with Speed 100 gains 1000 energy/round = 1 action/round. Speed 150 = acts 1.5Ã— as often.
13. **RNG**: Per-level seed derived as `seed XOR (floor * 7919)`. All procedural decisions (generation, loot, combat rolls) use this seeded RNG. Replay = same seed â†’ identical game.
14. **JSON content**: All game data in `Content/` directory. No hardcoded stats in C# code. Content changes must not require recompilation. Use `System.Text.Json` with `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`.

---

## 15. Parallel Safety Rules

### Parallel-Safe (DO)

- **Work within your owned files only** â€” see Agent Summaries Â§13 for exact file lists
- **Read shared contracts** â€” `Core/Interfaces/*`, `Core/DTOs/*`, `Core/Enums/*` are read-only for all agents except Architecture
- **Read EventBus signal definitions** â€” subscribe to signals, never add new ones
- **Stub missing dependencies** â€” if you need Agent X's output, implement the interface with a minimal stub in `Tests/Stubs/` and code against that
- **Write tests against stubs** â€” your tests must pass with stub implementations, not real ones
- **Emit events through EventBus** â€” for any state change another system might care about
- **Use `IWorldState` (read-only)** for queries â€” only Simulation mutates `WorldState` directly

### NOT Parallel-Safe (DO NOT)

- **Edit another agent's code** â€” even "small fixes." Document the need in your own code instead
- **Add signals to EventBus** â€” Architecture owns the signal definitions. If you need a new signal, add a `// TODO(Agent-N): need signal XYZ` comment
- **Create hidden state channels** â€” no global statics, no shared files, no backdoor references between systems
- **Duplicate logic across systems** â€” if two agents need the same algorithm (e.g., distance calculation), it belongs in `Core/DTOs/` or `Core/Interfaces/` (Architecture-owned)
- **Bypass the action pipeline** â€” never `entity.Stats.HP -= 5` directly. Always create an action
- **Use non-deterministic operations** â€” no `DateTime.Now`, no `Guid.NewGuid()` in simulation logic (only in EntityId initialization which is separate from gameplay RNG)
- **Modify `project.godot`** â€” only Architecture writes this file. If you need a setting changed, document it

### Conflict Resolution

If two agents discover they need the same file or a contract change:
1. The discovering agent documents the need as a comment in their own code
2. At integration time, Architecture resolves conflicts
3. Interfaces are never broken â€” only extended (new methods get default implementations)

---

## 16. Acceptance Criteria (MVP)

The engine is complete when ALL of the following pass:

**Build & Run**
- [ ] `dotnet build` succeeds with zero errors and zero warnings-as-errors
- [ ] Godot 4.4 opens the project without import errors
- [ ] `Main.tscn` loads and displays the main menu

**Simulation (Agent 2)**
- [ ] All 8 action types validate and execute correctly (Move, Attack, PickUp, UseItem, Wait, Drop, UseStairs, OpenDoor)
- [ ] Turn scheduler advances entities in correct energy order (faster entities act first)
- [ ] Combat resolver produces deterministic damage with hit/miss/crit
- [ ] Status effects tick each turn and expire after duration
- [ ] 37+ simulation tests pass

**Rendering (Agent 3)**
- [ ] Dungeon renders from WorldState data (floor, walls, doors, stairs visible)
- [ ] FOV is symmetric â€” if tile A is visible from B, then B is visible from A
- [ ] Three visibility states render correctly: visible (full color), seen (50% dimmed), hidden (black)
- [ ] Entity sprites animate movement (0.15s tween) and attacks (0.1s lunge + return)
- [ ] Camera follows player with smoothing, clamps to map edges

**Generation (Agent 4)**
- [ ] Same seed + same floor = identical level layout
- [ ] Every generated level has 4+ rooms connected by corridors
- [ ] Flood fill from stairs confirms all entities and stairs are reachable
- [ ] 12+ generation tests pass

**AI (Agent 5)**
- [ ] Enemies chase player when visible and in range
- [ ] Enemies attack when adjacent to player
- [ ] Enemies flee when HP < 25% (move away from player)
- [ ] A* pathfinding navigates around walls and obstacles
- [ ] 5 AI profiles produce distinct behaviors
- [ ] 12+ AI tests pass

**UI (Agent 6)**
- [ ] HUD displays current HP, floor number, and turn count
- [ ] Inventory opens/closes with `I`, shows items, supports Use/Drop/Equip
- [ ] Combat log displays color-coded messages with auto-scroll
- [ ] Player can complete a full run using keyboard only (no mouse required)
- [ ] All menus (main, pause, game over) are navigable

**Tools (Agent 7)**
- [ ] Map editor paints tiles and saves/loads room prefabs to `Content/Rooms/prefabs.json`
- [ ] Debug console accepts and executes 12+ commands (teleport, spawn, godmode, kill_all, give, set_hp, set_floor, reveal_map, spawn_stairs, list_entities, dump_state, help)
- [ ] Item editor loads and saves item JSON

**Persistence (Agent 8)**
- [ ] Saveâ†’Load round-trip preserves complete state (grid, entities, inventories, effects, energy, explored tiles, ground items, turn number, floor, seed)
- [ ] Autosave triggers on floor transition
- [ ] 4 save slots work independently (3 manual + autosave)
- [ ] Corrupted/tampered save files are rejected with error message
- [ ] 15+ persistence tests pass

**Content (Agent 9)**
- [ ] 15+ items defined and loadable (weapons, armor, consumables, scrolls)
- [ ] 6+ enemy types defined with distinct stats and AI profiles
- [ ] 9 status effects defined with tick/expire behavior
- [ ] 6 room prefabs defined in valid JSON format
- [ ] ContentDatabase loads all JSON without errors
- [ ] Content validation tests pass (no dangling references, no impossible stats)

**Integration**
- [ ] Seed â†’ full deterministic run is reproducible (same inputs â†’ same outcome)
- [ ] All content is editable via JSON without code changes or recompilation
- [ ] All agent test suites pass simultaneously (`dotnet test`)
- [ ] No cross-agent file ownership violations

---

## 17. Agent Prompt Template

Copy this template, fill in the bracketed values from Â§13, and provide it as the system prompt to each agent.

````
You are the **[AGENT_NAME]** agent (Agent [N]) for the godotussy Godot 4.4 + C# roguelike engine.

## REQUIRED READING
1. Master spec: `spec.md` (sections 10-16: project config, inputs, scenes, conventions, safety rules, acceptance criteria)
2. Shared contracts: `contracts.md` (all C# interfaces, DTOs, enums â€” READ ONLY, never modify)
3. Your detailed spec: `agents/agent-[N]-[name].md`

## YOUR SCOPE
**Files you OWN (create and modify):**
[LIST FROM Â§13 â€” e.g., Core/Simulation/WorldState.cs, Core/Simulation/TurnScheduler.cs, ...]

**Files you READ (never modify):**
- Core/Interfaces/* (shared contracts)
- Core/DTOs/* (shared data types)
- Core/Enums/* (shared enumerations)
- Scripts/Autoloads/EventBus.cs (signal definitions)
- Tests/Stubs/* (stub implementations for dependencies)

## RULES
1. Work ONLY in files listed under "Files you OWN" above
2. ALL gameplay state changes go through IAction.Validate() â†’ IAction.Execute()
3. ALL cross-system events emitted via EventBus â€” never mutate state silently
4. Everything MUST be deterministic â€” seeded RNG only, no DateTime.Now, no Dictionary iteration
5. Write tests for every feature in Tests/[AgentName]Tests/ using stubs from Tests/Stubs/
6. If blocked on another agent's output, code against the interface using the stub implementation
7. Document any interface needs as // TODO(Agent-[N]): comments in your own code
8. Use namespace Roguelike.Core for pure C#, Roguelike.Godot for Godot scripts
9. 16Ã—16 pixel tiles, (0,0) top-left, y * Width + x flat indexing
10. Energy threshold 1000, standard cost 1000, speed 100 = 1 action/round

## DELIVERABLES
[LIST FROM Â§13 â€” e.g., "Complete simulation engine with 37+ unit tests covering all actions, turn ordering, combat formulas, and status effect lifecycle"]

## ACCEPTANCE CRITERIA (your section from Â§16)
[PASTE RELEVANT CHECKBOXES FROM Â§16]

## OUTPUT FORMAT
Produce all owned files with complete, compilable code. No placeholders, no TODOs for your own features.
Include every file path as a header comment: `// File: Core/Simulation/WorldState.cs`
````