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
When the player dies, `GameManager` finalizes a runtime `RunStats` snapshot and emits `EventBus.GameOverWithStats`. `UIRoot` opens the `GameOverScreen`, which presents floor reached, turns survived, kills, gold, item finds, damage taken, seed, cause of death, best item, and a short contextual epitaph.
Floor-specific runtime counters live in a separate `FloorStats` snapshot. `GameManager` resets them for each loaded floor and emits `EventBus.FloorSummaryReady` immediately before travel loads the destination floor, allowing the UI to show kills, loot, gold, damage, turns, opened chests, and triggered traps for the floor being left without mixing them into lifetime `RunStats`.

The planned expansion path for progression is documented in `docs/PROGRESSION.md`.

## AI

Enemy decision-making lives in `Core/AI/`.

- `BrainFactory` maps configured brain identifiers to concrete `IBrain` implementations.
- Utility/state helpers live alongside the brains.
- Brains use `IPathfinder` and world data to choose `IAction` instances.
- Enemy `ai_params` from content now override the base `AIProfile` per template; recognized keys are `flee_hp_pct`, `aggro_range`, `wander_when_idle`, `preferred_range`, `min_range`, `patrol_radius`, `support_range`, and `phase_through_walls`.
- Save/load rehydrates the brain from the persisted `EnemyComponent.TemplateId` through the content database, so authored `ai_params` survive after loading.

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
- Kills from melee attacks, ability damage, sourced poison/burning status ticks, and trap hazards route through the shared `DeathResolver`, so XP, kill counts, level-up log messages, deterministic loot drops, gold awards, and entity removal stay aligned for those paths. Trap kills are currently unattributed (no XP/kill credit), matching environmental hazard design.

The ability pipeline is shared by item casts and AI casts so the runtime rules stay in one place.

## Generation

Dungeon generation lives in `Core/Generation/`.

The current flow in `DungeonGenerator` is:

1. Derive a repeatable attempt seed from the world seed, floor depth, and retry index.
2. Initialize the map as walls.
3. Build a BSP tree.
4. Place rooms (prefab `^` tiles become `TileType.Trap`).
5. Stitch rooms with corridors.
6. Collect trap spawn details from `^` tiles and explicit `type: "trap"` spawn points.
7. Place stairs, enemies, and items (trap positions are excluded from spawn rolls).
8. Validate the generated level with `LevelValidator`, including trap reachability.

Traps are walkable but hazardous stationary features. Authored trap definitions live in `Content/traps.json`; each room `trap_id` must reference a known trap. `LevelData` exposes `TrapSpawnDetails` so `GameManager.PopulateWorld` can instantiate trap entities.

## Rendering And UI Flow

The rendering layer is event-driven.

- `GameManager` emits gameplay changes through `EventBus`.
- `Scripts/World/WorldView.cs` and related world scripts react to those events to move entities, refresh visuals, and spawn presentation effects.
- `Scripts/World/AnimationController.cs` owns lightweight world-side effects such as damage popups and the timed bright/white hit flash driven by `DamageDealt`.
- `Scripts/UI/UIRoot.cs` binds the HUD, menus, overlays, combat log, tooltip, debug console, and input handler to the current runtime services.
- `FloorSummaryUI` listens for `FloorSummaryReady`, opens as a modal summary during floor travel, blocks gameplay input while visible through `UIRoot`, and auto-dismisses after six seconds unless the player presses a key.
- `UIRoot` also owns modal interaction surfaces such as dialog, shop, inventory, targeting, and the chest loot panel. Pressing `F` near an NPC opens dialog; pressing `F` near a chest, or bumping into a chest, rolls its contents through `OpenChestAction` and then opens `ChestUI` so individual rolled items can be selected or taken all at once through `TakeChestLootAction`.
- `ExaminePanel` is a `MenuBase` modal opened with `X` during gameplay. It routes through `UIRoot` before normal gameplay input, moves a cursor with `WASD`/arrow keys, and closes with `X` or `Escape`. It reads only visible/explored nearby cells from the current `WorldState`; visible cells may show entities, items, chests, doors, and revealed traps, while explored-but-not-visible cells show remembered tile information only. It does not emit `PlayerActionSubmitted` or mutate simulation state.
- `HUD` derives a nearby interaction prompt from current world state on turn/UI refresh (`[F] Talk`, `[F] Open Chest`, `[Enter] Descend/Ascend`) and exposes it as a clickable shortcut without changing the underlying keyboard actions.
- `HUD` also derives a runtime-only quick-use hotbar from the first five usable non-equipment inventory entries. Number keys `1`-`5` are routed only during normal gameplay, submit regular `UseItemAction` instances for self/no-target consumables, and log a warning instead of consuming aimed items that require the inventory targeting overlay.
- Run movement uses a prefix key because the current input path routes only key codes, not modifier state. Press `R`, then a cardinal direction to repeatedly process normal `MoveAction` turns through `GameManager.ProcessPlayerAction`; `Escape` cancels the prefix. The run stops before blockers, doors, occupants, nearby points of interest, low HP, damage taken, game over, visible/adjacent hostiles, or a fixed safety cap.
- Autoexplore uses `O` to repeatedly recompute deterministic BFS from the current player position, preferring visible points of interest and then the nearest reachable unexplored frontier. Every step is submitted as a normal `MoveAction` through `GameManager.ProcessPlayerAction`; it stops for visible/adjacent hostiles, damage taken, low HP, reached points of interest, no reachable target, game over, invalid movement, or a fixed safety cap.
- Rest-until-healed uses `Z` to repeatedly process normal `WaitAction` turns through `GameManager.ProcessPlayerAction`. It preserves single waits on `Space` and `.`, stops immediately at full HP or visible/adjacent hostiles, also stops for low HP, poison/burning/corrosion, damage taken, game over, invalid waits, or a fixed safety cap, and does not add UI-side passive healing.

Current presentation-specific behavior worth knowing:

- `WorldArtCatalog` now resolves world and entity art from the imported CC0 0x72 tileset subset under `Assets/Tilesets/0x72/` and `Assets/Sprites/0x72/`.
- `WorldView` hides the legacy tilemap visuals and scales the imported 16x16 art up to the runtime 40x40 cell size.
- `WorldView` only mirrors fog/FOV state from the active world; `GameManager`/`WorldState` own authoritative visibility and exploration mutation.
- `AnimationController` now advances short eased move animations over multiple `_Process(...)` frames instead of snapping movement immediately. `WorldView` clears transient popups/flashes on full world rebinds and floor changes so damage numbers cannot leak between redraws.
- `DamageDealt` events trigger an immediate non-white defender flash. The flash fades back to exact white over about 0.12 seconds, and entity refreshes avoid clobbering the active tint.
- `CameraController.DefaultZoom` is `2f`, which is the baseline zoomed-out framing used by `WorldView`.
- `HUD` presents HP as the primary status readout with a prominent label/bar, while non-HP stats are grouped separately for quick scanning.
- `HUD` keeps HP and energy text authoritative and immediate, while the visible bar fills interpolate toward target values in `_Process(...)` and apply a short deterministic pulse tint on damage, healing, and energy changes. When current HP is below the existing danger threshold (`< 0.3`), the HP bar also shows a non-color stripe pattern; `HPBarDangerPatternVisible` exposes that state for tests. Tests assert the target/displayed/pattern state directly rather than depending on rendered frames.
- `MenuBase` now renders menus as separate title, BBCode-capable summary, options, and footer regions, and the title-screen-to-workshop handoff temporarily dismisses the main menu so overlays do not stack visually. Shared UI chrome uses a higher-contrast dark gothic stone/iron palette with brighter parchment/gold text and blood-red HP accents across menus, HUD, inventory, tooltips, combat log, character sheet, and minimap.
- `MainMenu`, `PauseMenu`, `HelpOverlay`, and `CharacterSheet` now use clearer run/build/tool hierarchy, sectioned body text, and shared dungeon-console chrome so modal screens read as deliberate game surfaces instead of generic panels.
- `InventoryUI` remains text-driven for low-risk stub testing, but uses stable category glyphs, non-color rarity abbreviations such as `[R]`, rarity-colored item tokens, gold selected-slot framing, explicit equipped markers, full stack counts, contextual footers, stack/charge details, and multiline equipment comparisons for faster scanning. The bottom-right equipment tooltip is taller and uses smaller rich text so comparison lines remain visible. Tooltip and combat-log item pickup markup use the same centralized rarity helpers with BBCode-safe bracket markers.
- Aimed scrolls (`scroll_fireball`, `scroll_blink`) are flagged `RequiresTargetSelection`; selecting one enters `TargetingOverlay` mode where directional keys move the cursor, Enter confirms, and Escape cancels. `WorldView` mirrors the cursor and AoE preview from EventBus events without mutating world state.
- `Minimap` remains a non-modal gameplay overlay toggled by `M`/`Tab`. It uses a darker framed map treatment with subdued explored/visible tile colors, gold player/stair cues, visible enemy/NPC/item/chest markers, distinct trap and door colors, and a compact in-panel legend for the marker/color set.
- Long text-driven UI surfaces now window or clamp overflow: inventory pages beyond its visible grid, shop and dialog option lists keep the selected row visible with ellipses, and tooltip bodies cap long content instead of rendering beyond the panel.
- Status effects are now visible in the HUD and on entity sprites. `EventBus.StatusEffectApplied` and `StatusEffectRemoved` drive immediate refreshes. `EntityRenderer` adds a `StatusOverlay` child per entity, populating it with `Sprite2D` icons looked up from `status_effects.json` `icon_path` and tinted with `color_tint`. `HUD` renders the same icons in a horizontal status badge row with remaining-turn labels.
- `HelpOverlay` keeps its gameplay and title-screen guidance condensed enough to fit inside the menu shell on short viewports, instead of relying on off-screen overflow.
- `DevToolsWorkbench` windows long mode summaries and action lists against the current viewport so the selected tool action, status line, and controls remain readable on shorter screens.

The important constraint is that rendering code mirrors simulation state; it should not become the source of truth.

### Combat Log

`Scripts/UI/CombatLog.cs` listens to `EventBus.LogMessage` plus combat, death, item, status, save/load, and floor events. Explicit log messages carry a `LogCategory` (`System`, `PlayerAction`, `EnemyAction`, `Loot`, `StatusEffect`, `Warning`, or `Critical`) so presentation no longer relies only on message-text heuristics. Existing one-argument emitters still route through `System` for compatibility.

The log renders BBCode-safe entries with category colors from `UiStyle`: system and status entries use muted text, player actions use parchment, enemy actions use danger red, loot and critical entries use bright gold, and warnings use warning amber. Critical entries are bolded. Older visible entries fade with BBCode alpha after three newer entries and fade further after six newer entries.

## Persistence

Persistence lives in `Core/Persistence/`.

### Components

- `SaveSerializer` converts `WorldState` to and from the normalized JSON save shape.
- `SaveValidator` checks normalized save payloads before they are accepted.
- `SaveMigrator` upgrades legacy save payloads to the current schema on load.
- `SaveManager` reads and writes save files and performs atomic temp-file replacement on save.
- `SaveSlots` defines valid slot indexes and file names.

### Current Save Version

The current normalized save version is `13`.

Notable details:

- Explored and visible map flags are stored as packed bitfields.
- Legacy version 1 through version 12 payloads are migrated on load.
- Saves now persist the active floor plus cached inactive floors through a normalized floor list, while retaining active-floor root aliases for compatibility with metadata and existing tooling.
- New saves include optional content metadata (`contentVersion` and a deterministic content hash) so load flows can warn when the authored JSON set differs from the one that created the save.
- Multi-floor validation requires unique floor depths, an active floor payload, and exactly one player entity across all saved floors on the active floor.
- Persistence tests cover malformed v8/v9 floor payloads for missing active floors, duplicate player entities across floors, and duplicate floor depths; `GameManager` save/load coverage also exercises travel back to a cached inactive floor.
- Progression, identity, inventory/equipment, wallet, NPC, merchant, chest loot table, rolled chest contents, ability, cooldown, XP value, AI/template rehydration data, enemy template identity, scheduler actor order, status-effect source attribution, character creation options, and trap component state (`TrapComponent` on trap entities) round-trip through the normalized save shape where applicable.
- `CombatRandomState` and `ItemRandomState` are persisted and rehydrated atomically through `WorldState.RehydrateRandomStates` so RNG continuation matches uninterrupted simulation with no transient intermediate state.
- Deterministic replay regression tests prove that fixed action sequences produce identical traces after save/load at turn boundaries.
- Content metadata is warning-only: legacy or migrated saves with missing metadata still load, and hash/version mismatches emit a runtime log warning instead of failing validation.
- Save validation checks dimensions, entity IDs, inventory/equipment integrity, persisted component payloads, status effects, payload sizes, and trap entity consistency (an entity with a `TrapComponent` must sit on a `Trap` tile and have a non-empty template id).

### Traps

Traps are entities with `TrapComponent` placed on `TileType.Trap` tiles during `GameManager.PopulateWorld`. When an entity enters a trap tile, `MoveAction` invokes `HazardProcessor.OnEntityEnteredTile`, which resolves the trap template, checks configured avoid flags (e.g., `phased`, `flying`), rolls damage/status deterministically via `CombatResolver`, applies damage, and routes kills through `DeathResolver.ResolveUnattributedDeath`. Successful triggers append `CombatEvent`s and log messages to the action outcome, disarm and reveal the trap, and raise `HazardProcessor.TrapTriggered`; `GameManager` forwards this to `EventBus.TrapTriggered` so the combat log/animation layer can react.

`TrapComponent` is the single source of truth for trap state (`TemplateId`, `IsArmed`, `IsRevealed`, `TriggerCount`). It is serialized as part of the owning entity's save payload and restored on load, so trap entities survive save/load round trips and continue to trigger correctly. Legacy v10/v11 standalone trap arrays are migrated into trap entities by `SaveMigrator`.

The rendering layer treats `TileType.Trap` as a visible floor tile with a trap marker, and `Minimap` colors trap tiles distinctly.

### Locked Doors

Locked doors are represented by `TileType.LockedDoor`. During generation, `DungeonGenerator.PlaceLockedDoorsAndKeys` selects connecting door positions leading into rooms flagged as locked (e.g., arenas, vaults) and converts them to `LockedDoor` tiles; it then places a `dungeon_key` item in a reachable non-locked room. `OpenDoorAction` validates that the actor is adjacent to a `LockedDoor` tile and has at least one `dungeon_key` in their inventory, consumes one key, and changes the tile to `TileType.OpenDoor`. Locked doors block movement and sight until opened. The change is persisted as map tiles and ground items, so locked state and key consumption survive save/load without a dedicated door component.

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
