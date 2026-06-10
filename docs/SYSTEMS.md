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

Stacked consumables and scrolls consume one item from the stack per successful use; the remaining stack stays in inventory.
Core pickup actions resolve stack metadata from the world's content database when needed, so stack merging and capacity checks do not depend on the UI pre-supplying an item template. Inventory UI also exposes a runtime-only auto-equip upgrades toggle. Press `A` while inventory is open to enable or disable it; pickup with `G` honors the current toggle when deciding whether upgrades should be equipped automatically.

Neutral entities, including treasure chests and NPCs, are not valid melee targets. Enemy AI ignores neutral entities when acquiring targets, so mobs do not path to, attack, or destroy containers.
The player can swap places with adjacent neutral NPCs by moving into them; this keeps NPCs non-hostile while preventing them from permanently blocking tight corridors.

## Progression And Identity

Player-specific long-run state lives in explicit components instead of bloating `Stats`.

- `ProgressionComponent` tracks level, XP, next threshold, unspent stat points, and kills.
- `IdentityComponent` tracks race, gender, appearance, and sprite variant metadata.
- `XpValueComponent` tags enemies with deterministic XP rewards loaded from content.

XP is awarded on kill, level thresholds are deterministic, level-ups grant baseline stat growth plus unspent points, and progression/identity data are persisted with saves.
Loading a run with pending perk choices reopens the level-up overlay when choices are available, so saved progression decisions remain visible after resume.

The planned expansion path for progression is documented in `docs/PROGRESSION.md`.

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
- `CastAbilityAction` performs targeting validation for self, single-target, tile, and area ability shapes before mutating world state.
- Ability damage and status-chance rolls use the serialized combat RNG stream, so save/load continuation matches uninterrupted simulation for covered ability paths.
- Harmful area damage/status effects default to enemy-only when `hits_allies` is false and no explicit effect filter is authored; explicit filters and `hits_allies: true` keep their broader behavior.
- Kills from melee attacks, ability damage, and sourced poison/burning status ticks route through the shared `DeathResolver`, so XP, kill counts, level-up log messages, and entity removal stay aligned for those paths. Traps and future environmental deaths still need attribution work.

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
Generated chests use guaranteed chest loot tables, so chest rewards stay deterministic and content-authored instead of falling back to generic item placement.
Opening a chest is currently atomic: the chest rolls deterministic loot, stows anything that fits in the opener's inventory, spills overflow to the floor, logs an explicit `Loot found:` summary, and removes the opened chest entity.

## Rendering And UI Flow

The rendering layer is event-driven.

- `GameManager` emits gameplay changes through `EventBus`.
- `Scripts/World/WorldView.cs` and related world scripts react to those events to move entities, refresh visuals, and spawn presentation effects.
- `Scripts/World/AnimationController.cs` owns lightweight world-side effects such as the damage popup.
- `Scripts/UI/UIRoot.cs` binds the HUD, menus, overlays, combat log, tooltip, debug console, and input handler to the current runtime services.

Current presentation-specific behavior worth knowing:

- `WorldArtCatalog` now resolves world and entity art from the imported CC0 0x72 tileset subset under `Assets/Tilesets/0x72/` and `Assets/Sprites/0x72/`.
- `WorldView` hides the legacy tilemap visuals and scales the imported 16x16 art up to the runtime 40x40 cell size.
- `WorldView` only mirrors fog/FOV state from the active world; `GameManager`/`WorldState` own authoritative visibility and exploration mutation.
- `AnimationController` now advances short eased move animations over multiple `_Process(...)` frames instead of snapping movement immediately.
- `CameraController.DefaultZoom` is `2f`, which is the baseline zoomed-out framing used by `WorldView`.
- `HUD` presents HP as the primary status readout with a prominent label/bar, while non-HP stats are grouped separately for quick scanning.
- `MenuBase` now renders menus as separate title, summary, options, and footer regions, and the title-screen-to-workshop handoff temporarily dismisses the main menu so overlays do not stack visually. Shared UI chrome uses a dark gothic stone/iron palette with gold trim, parchment text, and blood-red HP accents across menus, HUD, inventory, tooltips, combat log, character sheet, and minimap.
- `MainMenu`, `PauseMenu`, `HelpOverlay`, and `CharacterSheet` now use clearer run/build/tool hierarchy, sectioned body text, and shared dungeon-console chrome so modal screens read as deliberate game surfaces instead of generic panels.
- `InventoryUI` remains text-driven for low-risk stub testing, but uses stable category glyphs, rarity-colored item tokens, gold selected-slot framing, explicit equipped markers, full stack counts, contextual footers, stack/charge details, and multiline equipment comparisons for faster scanning.
- `Minimap` remains a non-modal gameplay overlay, but uses a darker framed map treatment with ember/gold player and stair cues and subdued etched explored/visible tile colors.
- Long text-driven UI surfaces now window or clamp overflow: inventory pages beyond its visible grid, shop and dialog option lists keep the selected row visible with ellipses, and tooltip bodies cap long content instead of rendering beyond the panel.
- `HelpOverlay` keeps its gameplay and title-screen guidance condensed enough to fit inside the menu shell on short viewports, instead of relying on off-screen overflow.
- `DevToolsWorkbench` windows long mode summaries and action lists against the current viewport so the selected tool action, status line, and controls remain readable on shorter screens.

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

The current normalized save version is `8`.

Notable details:

- Explored and visible map flags are stored as packed bitfields.
- Legacy version 1 through version 7 payloads are migrated on load.
- Saves now persist the active floor plus cached inactive floors through a normalized floor list, while retaining active-floor root aliases for compatibility with metadata and existing tooling.
- New saves include optional content metadata (`contentVersion` and a deterministic content hash) so load flows can warn when the authored JSON set differs from the one that created the save.
- Multi-floor validation requires unique floor depths, an active floor payload, and exactly one player entity across all saved floors on the active floor.
- Persistence tests cover malformed v8 floor payloads for missing active floors, duplicate player entities across floors, and duplicate floor depths; `GameManager` save/load coverage also exercises travel back to a cached inactive floor.
- Progression, identity, inventory/equipment, wallet, NPC, merchant, chest, ability, cooldown, XP value, AI/template rehydration data, and status-effect source attribution round-trip through the normalized save shape where applicable.
- `CombatRandomState` is persisted so combat RNG continues deterministically after load instead of restarting from only seed and turn number.
- `ItemRandomState` is persisted so stack splitting and stack overflow clone IDs continue deterministically after save/load.
- Content metadata is warning-only: legacy or migrated saves with missing metadata still load, and hash/version mismatches emit a runtime log warning instead of failing validation.
- Save validation checks dimensions, entity IDs, inventory/equipment integrity, persisted component payloads, status effects, and payload sizes.

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

- items, including weapon combat fields, on-hit effects, equipment requirements, and consumable `on_use` behavior
- enemies, including XP values
- abilities, including targeting and effect definitions
- perks, NPCs, and dialogs used by progression/service content and room fixtures

Consumable item use supports authored healing, status application, and ability-cast delegation. `cast_ability` use effects must be paired with valid ability content and targeting data from the caller/UI so the same targeting validation path is used as direct ability casts.

The loader can locate the repository content directory automatically by walking upward until it finds the required JSON files.

## Compatibility Stubs

`Compat/Godot/GodotStubs.cs` exists to keep the solution buildable in plain .NET environments. When a script starts using additional Godot APIs, the stubs may need to be extended so non-editor builds still compile.
