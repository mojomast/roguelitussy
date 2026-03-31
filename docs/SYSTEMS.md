# Systems

This document summarizes the main engine subsystems and how they fit together.

## Turn Processing

The turn model is energy-based.

- `ITurnScheduler` tracks actor readiness.
- `Core/Simulation/GameLoop.cs` processes a round by repeatedly asking for the next actor, validating the chosen action, executing it, and consuming energy.
- Player actions are submitted through `GameManager.ProcessPlayerAction(...)`.
- Non-player actors fall back to brain-driven decisions when the loop requests an action for them.
- End-of-round processing now also ticks entity ability cooldowns through `CooldownComponent`.

Validation failures still consume action energy in the current loop implementation. That behavior is intentional and covered by tests.

## Actions And Outcomes

Gameplay mutations should be expressed as `IAction` implementations.

Each action is expected to:

1. Validate against the current `WorldState`.
2. Execute inside the simulation layer.
3. Return an `ActionOutcome` containing combat events, log messages, and dirty positions.

The Godot layer should react to emitted results, not perform the authoritative mutation itself.

Notable action families now in play:

- melee attacks with weapon-derived damage, crit, and on-hit effects
- item use with direct effects or delegated ability casts
- cast actions for self, single-target, tile-targeted, and area abilities
- equip toggles that validate item requirements before mutating inventory state

## Progression And Identity

Player-specific long-run state lives in explicit components instead of bloating `Stats`.

- `ProgressionComponent` tracks level, XP, next threshold, unspent stat points, and kills.
- `IdentityComponent` tracks race, gender, appearance, and sprite variant metadata.
- `XpValueComponent` tags enemies with deterministic XP rewards loaded from content.

XP is awarded on kill, level thresholds are deterministic, level-ups grant baseline stat growth plus unspent points, and progression/identity data are persisted with saves.

## AI

Enemy decision-making lives in `Core/AI/`.

- `BrainFactory` maps configured brain identifiers to concrete `IBrain` implementations.
- Utility/state helpers live alongside the brains.
- Brains use `IPathfinder` and world data to choose `IAction` instances.

Current built-in brain types include:

- `melee_rusher`
- `ranged_kiter`
- `patrol_guard`
- `fleeing`
- `ambush`
- `support`

Enemies can now carry `AbilitiesComponent` and `CooldownComponent`, allowing brains to consider `CastAbilityAction` candidates alongside movement and melee attacks.

## Combat And Abilities

Combat is still resolved inside `Core/Simulation/CombatResolver.cs`, but it is no longer limited to base attack variance.

- equipped weapons can contribute damage ranges, crit chance, accuracy, speed modifiers, and on-hit status effects
- empowered and corroded states now influence outgoing damage and effective armor
- `CastAbilityAction` and `AbilityResolver` execute ability effects from `abilities.json`
- supported ability effects currently include damage, apply_status, teleport, and heal_self

The ability pipeline is shared by item casts and AI casts so the runtime rules stay in one place.

## Generation

Dungeon generation lives in `Core/Generation/`.

The current flow in `DungeonGenerator` is:

1. Derive a repeatable attempt seed from the world seed, floor depth, and retry index.
2. Initialize the map as walls.
3. Build a BSP tree.
4. Place rooms.
5. Stitch them with corridors.
6. Place stairs, enemies, and items.
7. Validate the generated level with `LevelValidator`.

Generation retries up to a fixed limit if a connected, valid layout is not produced.

## Rendering And UI Flow

The rendering layer is event-driven.

- `GameManager` emits gameplay changes through `EventBus`.
- `Scripts/World/WorldView.cs` and related world scripts react to those events to move entities, refresh visuals, and spawn presentation effects.
- `Scripts/World/AnimationController.cs` owns lightweight world-side effects such as the damage popup.
- `Scripts/UI/UIRoot.cs` binds the HUD, menus, overlays, combat log, tooltip, debug console, and input handler to the current runtime services.

The important constraint is that rendering code mirrors simulation state; it should not become the source of truth.

## Persistence

Persistence lives in `Core/Persistence/`.

### Components

- `SaveSerializer` converts `WorldState` to and from the normalized JSON save shape.
- `SaveValidator` checks normalized save payloads before they are accepted.
- `SaveMigrator` upgrades legacy save payloads to the current schema on load.
- `SaveManager` reads and writes save files and performs atomic temp-file replacement on save.
- `SaveSlots` defines valid slot indexes and file names.

### Current Save Version

The current normalized save version is `4`.

Notable details:

- Explored and visible map flags are stored as packed bitfields.
- Version 1, version 2, and version 3 save payloads are migrated on load.
- Progression and identity components round-trip through the normalized save shape.
- Save validation checks dimensions, entity IDs, inventory/equipment integrity, status effects, and payload sizes.

### Save Slots

- `0` - autosave (`autosave.json`)
- `1` - manual slot 1 (`slot_1.json`)
- `2` - manual slot 2 (`slot_2.json`)
- `3` - manual slot 3 (`slot_3.json`)

### Default Save Location

By default, `SaveManager` writes to:

`%AppData%\godotussy\saves`

## Content Loading

`ContentLoader` loads the repository JSON files, validates them, builds lookup tables, and projects the subset needed by the simulation into runtime templates.

The runtime-facing templates now cover:

- items, including weapon combat fields, on-hit effects, and equipment requirements
- enemies, including XP values
- abilities, including targeting and effect definitions

The loader can locate the repository content directory automatically by walking upward until it finds the required JSON files.

## Compatibility Stubs

`Compat/Godot/GodotStubs.cs` exists to keep the solution buildable in plain .NET environments. When a script starts using additional Godot APIs, the stubs may need to be extended so non-editor builds still compile.