# Roguelitussy Development Resume Report

Generated after cloning and reviewing the repository with parallel subagent passes focused on architecture, gameplay/content correctness, and build/developer workflow.

## Executive Summary

The project is a strong Godot 4.4.1 Mono/.NET roguelike foundation with a mostly clean split between deterministic simulation in `Core/`, Godot presentation in `Scripts/`, JSON content in `Content/`, and a custom test harness in `Tests/`.

Development should resume by stabilizing correctness and verification before adding more content. The highest-impact blockers are save/load rehydration, item/ability runtime drift, centralized death/progression handling, and build/test automation.

## Verification Status

- Repository cloned to `/home/mojo/projects/roguelitussy`.
- Working tree was clean immediately after clone.
- `dotnet --info` failed in this environment: `dotnet: command not found`.
- `godot` was not found on `PATH`.
- Build, tests, and headless Godot startup could not be verified locally because required tools are missing.

### Follow-up Status - 2026-06-09

- Completed: minimal GitHub Actions CI now covers the .NET 8 Godot stub build, explicit test project build, full custom test harness run, and rendering validation profile.
- Completed: docs now use the canonical editorless commands and Godot 4.4.1 Mono/.NET wording.
- Completed: save documentation reflects save version 7, persisted behavior-critical components, and `CombatRandomState` deterministic RNG continuation.
- Completed: item-use and ability-cast docs now describe heal/apply_status/cast_ability behavior and targeting validation.
- Partial: shared `DeathResolver` progression behavior is documented for melee and ability-damage kills; status/trap/environment attribution remains follow-up work.
- Verification limitation: this environment still does not have `dotnet` available, so the new workflow and commands were not executed locally.

Recommended first verification on a machine with the toolchain:

```bash
dotnet build godotussy.csproj -p:UseGodotStubs=true
dotnet build Tests/godotussy.Tests.csproj
dotnet run --project Tests/godotussy.Tests.csproj
dotnet run --project Tests/godotussy.Tests.csproj -p:RenderingValidation=true
godot --headless --path . --quit
```

### Follow-up Status - 2026-06-26

- Completed: hardened RNG rehydration order with `WorldState.RehydrateRandomStates` so combat and item RNG states are restored atomically without transient public setter calls.
- Completed: added `DeterministicRandom.Peek()` for non-consuming RNG state inspection.
- Completed: added deterministic replay regression tests (`Tests/PersistenceTests/DeterminismTests.cs`) that prove fixed move/attack/wait sequences produce identical traces after save/load at turn 3 and turn 7.
- Completed: updated `docs/SYSTEMS.md` save version to 9 and documented atomic RNG rehydration plus replay regression coverage.
- Completed: marked SAV-3 and SAV-4 done in `docs/IMPROVEMENT_SUGGESTIONS.md`.

### Follow-up Status - Wave 1 Persistence

- Completed: SAV-1 — added `EnemyComponent { TemplateId }`, attached in `GameManager.CreateEnemyEntity`, and round-tripped through v9 save data with validation and migration.
- Completed: SAV-2 — added `SchedulerOrder` per actor and `SchedulerNextOrder` per floor, staged through `WorldState.SchedulerOrders`, and restored during `GameManager.RegisterWorldEntities` so energy tie-break order survives save/load.
- Completed: bumped normalized save version to `9` and added `SaveMigrator.MigrateV8` for legacy save compatibility.
- Completed: added persistence tests for enemy template identity and scheduler actor order.
- Verification: stub build, test project build, full harness, and rendering-validation profile all pass with zero failures.

### Follow-up Status - Wave 1 Status/Combat

- Completed: STA-1 — `StatusEffectProcessor` now resolves `tick_effects`, `stat_modifiers`, stacking/refresh rules, and `on_apply_effects` from authored `StatusEffectDefinition`s via `IContentDatabase`.
- Completed: STA-3 / COM-1 — authored status flags are now honored: `skip_turn` skips the actor's turn in `TurnScheduler`, `phase_through_walls` lets actors move through walls in `MoveAction`, and `immune_physical` nullifies physical damage in `CombatResolver`.
- Completed: added `shielded` status effect to `Content/status_effects.json` with a matching `status_shield.svg` asset so `CombatResolver` armor code has authored data.
- Completed: added `StatusEffects` lookup and `TryGetStatusEffect` to `IContentDatabase` and `ContentLoader`.
- Completed: added data-driven and flag-honoring tests in `Tests/SimulationTests/StatusEffectTests.cs`.
- Verification: stub build and test project build pass with zero warnings. Custom harness run passes all new status/combat tests plus existing status tests. 15 unrelated pre-existing UI test failures remain and are outside the status/combat workstream.

### Follow-up Status - ITM-1 Enemy Loot And Gold

- Completed: `EnemyTemplate` and `EnemyDefinition` now carry `LootTableId`, `GoldMin`, and `GoldMax` mapped through `ContentLoader.BuildEnemyTemplate`.
- Completed: `DeathResolver.ResolveKill` rolls the victim's loot table with a deterministic seed (world seed, depth, position, turn, and victim id) and drops items at the corpse position; unattributed deaths also drop loot.
- Completed: `DeathResolver.ResolveKill` awards rolled gold from the enemy's `gold_min`/`gold_max` range to the killer's `WalletComponent` when present.
- Completed: `AttackAction`, `CastAbilityAction`, and `StatusEffectProcessor` now include loot/gold messages in their respective death log outputs.
- Completed: `Tests/SimulationTests/DeathResolverTests.cs` covers deterministic loot drops, gold range compliance, and ground placement.
- Completed: `Tests/ContentTests/ContentValidationTests.cs` validates `gold_min <= gold_max` for every enemy.
- Decision: `EnemyComponent` was not extended with `LootTableId`/`GoldMin`/`GoldMax`; loot and gold are derived from the persisted `TemplateId` at death time to avoid a save-format bump.
- Deferred: `EventBus.EmitCurrencyChanged` when player gold changes from enemy death requires a `GameManager` change; a TODO is left in `DeathResolver` with the exact integration point.
- Deferred: `StatusTickResult.LogMessages` are generated but not yet flushed to the combat log because `ITurnScheduler.ConsumeEnergy` returns void; a TODO is left in `StatusEffectProcessor`.
- Documentation: `docs/CONTENT.md` now documents enemy `gold_min`/`gold_max`; `docs/IMPROVEMENT_SUGGESTIONS.md` marks ITM-1 done.
- Verification: builds and tests could not be executed in this environment because `dotnet` is not available.

### Follow-up Status - Wave 2 Itemization And Targeting

- Completed: ITM-1 — `DeathResolver.ResolveKill` rolls enemy loot tables and awards gold to the killer's `WalletComponent`; loot/gold are derived from the persisted `EnemyComponent.TemplateId` so save format stays at v9.
- Completed: ITM-1 integration — `GameManager.ProcessPlayerAction` snapshots the player wallet and emits `EventBus.EmitCurrencyChanged` when enemy-death gold changes it.
- Completed: ABI-1 / ITM-5 — added `TargetingOverlay` owned by `UIRoot`; selecting an aimed scroll from `InventoryUI` enters targeting mode, moves a cursor with directional keys, confirms with Enter, and cancels with Escape.
- Completed: ABI-1 / ITM-5 — `UIActionFactory.CreateUseItemAction` accepts an optional `Position? target`, and `UseItemAction` routes aimed scrolls through the existing `CastAbilityAction` validation/execution path.
- Completed: ABI-1 / ITM-5 — `WorldView` renders targeting cursor and AoE preview tiles from EventBus events without mutating `WorldState`.
- Completed: ABI-1 / ITM-5 — `ContentLoader.ValidateItems` now rejects `cast_ability` items whose declared target (`self`/`aimed`) does not match the referenced ability's `targeting.type`.
- Completed: added `ItemTemplate.RequiresTargetSelection` derived flag so `InventoryUI` can distinguish self-targeted scrolls from aimed ones.
- Completed: added `Tests/SimulationTests/DeathResolverTests.cs`, extended `Tests/SimulationTests/ItemAbilityRuntimeTests.cs`, and added `Tests/UITests/TargetingOverlayTests.cs`.
- Documentation: `docs/IMPROVEMENT_SUGGESTIONS.md` marks ITM-1, ITM-5, and ABI-1 done.
- Verification: stub build, test project build, full harness (300 tests), and rendering-validation profile (261 tests) all pass with zero failures.

### Follow-up Status - Wave 3 AI And Character Save

- Completed: AI-1 — `EnemyTemplate` carries `AIParameters` parsed from `EnemyDefinition.AiParams`; `ContentLoader.ValidateEnemies` recognizes all authored keys (`flee_hp_pct`, `aggro_range`, `wander_when_idle`, `preferred_range`, `min_range`, `patrol_radius`, `support_range`, `phase_through_walls`) and rejects unknown keys.
- Completed: AI-1 — `BrainFactory.Create(EnemyTemplate)` builds a runtime `AIProfile` from the base brain-type profile overridden by authored `ai_params`; brain subclasses accept profile injection.
- Completed: AI-1 — `AIBrain`, `UtilityScorer`, `AIStateManager`, and `Pathfinder` honor all recognized `ai_params`; `phase_through_walls` applies a permanent `Phased` status on the entity.
- Completed: AI-1 — `Content/enemies.json` goblin archer `preferred_range` changed to `4`; `Tests/AITests/AIParamsTests.cs` covers every recognized key and the concrete goblin-archer range-4 stop behavior.
- Completed: AI-1 save/load integration — `SaveSerializer`, `SaveManager`, and `GameManager` now pass `IContentDatabase` through load paths; brains rehydrate from the persisted `EnemyComponent.TemplateId`, so authored `ai_params` survive after loading.
- Completed: CHR-1 — `SaveRunSnapshot` carries a `CharacterOptionsSaveData` DTO; `SaveSerializer` serializes/deserializes it at save version 10; `SaveMigrator.MigrateV9` supplies default options for legacy saves.
- Completed: CHR-1 — `GameManager` maps `CharacterCreationOptions` into the snapshot on save and reconstructs it from the snapshot during `LoadFromSlot`; `Tests/PersistenceTests/SaveManagerTests.cs` round-trips archetype/race and other options.
- Completed: GEN-1 — authored traps and hazard tiles. `TileType.Trap` is walkable; `^` in `room_prefabs.json` maps to `TileType.Trap`; `DungeonGenerator` collects `^` tiles and explicit `type: "trap"` spawn points into `LevelData.TrapSpawnDetails`, marks trap positions occupied before enemy/item placement, and `LevelValidator` confirms reachability. `GameManager.PopulateWorld` spawns trap entities from `TrapSpawnDetails`.
- Completed: GEN-1 — `MoveAction` invokes `HazardProcessor.OnEntityEnteredTile` after every successful position change (normal move, phased move, NPC swap). `HazardProcessor` resolves trap templates, respects `avoid_flags` (`phased`, `flying`), rolls damage/status with `CombatResolver`, applies damage, routes kills through `DeathResolver.ResolveUnattributedDeath`, appends `CombatEvent`s/log messages, disarms/reveals traps, and raises `EventBus.TrapTriggered` via `GameManager`.
- Completed: GEN-1 — `Content/traps.json` defines `spike_trap` with direct damage and optional `ability_id`; `ContentLoader` validates trap/ability cross-references and room `trap_id` references. `Assets/Sprites/objects/trap_spikes.svg` added.
- Completed: GEN-1 save/load — save version bumped to `12`; `TrapComponent` is the single source of truth for trap state and is serialized as part of the owning entity; legacy v10/v11 standalone `Traps` arrays are migrated into trap entities by `SaveMigrator`; `SaveValidator` rejects trap entities on non-`Trap` tiles or with missing template ids.
- Completed: GEN-1 rendering — `WorldView`, `WorldArtCatalog`, and `Minimap` handle `TileType.Trap` (floor + marker, distinct minimap color).
- Documentation: `docs/IMPROVEMENT_SUGGESTIONS.md` marks AI-1, CHR-1, and GEN-1 done; `docs/SYSTEMS.md` documents trap generation, trigger rules, persistence, and rendering; `docs/CONTENT.md` documents trap authoring schema; `docs/PROGRESSION.md` notes trap kills are unattributed.
- Verification: stub build, test project build, full harness (332 tests), and rendering-validation profile (287 tests) all pass with zero failures.

### Follow-up Status - 2026-06-10

- Completed: local .NET 8 SDK installed under `$HOME/.dotnet`; stub build, test project build, full harness, and rendering validation now run in this environment.
- Completed: ability damage/status randomness now advances the serialized combat RNG stream, with save/load continuation coverage.
- Completed: sourced poison/burning status kills now route through `DeathResolver` and award XP/kill credit to the source; source attribution is persisted.
- Completed: harmful area effects now honor `hits_allies: false` by defaulting unfiltered harmful effects to enemies only.
- Completed: rendering validation profile compiles without persistence implementation files by using a no-op save manager under `RENDERING_VALIDATION` and skipping persistence-backed tests.
- Completed: save version 8 persists cached multi-floor run state through `SaveRunSnapshot`, including migration from v7 single-floor saves and validation that the player exists only on the active floor.
- Completed: CI now includes a real Godot 4.4.1 Mono headless editor import and startup smoke, and rendering-validation intermediates are isolated from the normal stub build profile.
- Completed: v8 persistence loose-end coverage now rejects missing active floors, duplicate player entities across floors, and duplicate floor depths; UI/GameManager coverage saves a multi-floor run, reloads it, and travels back to a cached inactive floor.
- Completed: authored item/status/enemy visual paths now resolve to committed assets, with simple SVG item/status icon source art and enemy paths repointed to existing 0x72 sprites.
- Completed: inventory and menu screens received a focused presentation pass: stable item category glyphs, explicit equipped/stack/charge/comparison details, contextual inventory footer text, clearer pause/help hierarchy, and sectioned character-sheet chrome.
- Completed: v8 saves now carry optional content version/hash metadata for warning-only load diagnostics when runtime JSON content differs from the saved run; legacy/migrated saves still load with unknown content metadata.
- Completed: neutral chests are protected from mob targeting and melee validation, chest-open logs now explicitly list found loot and where it went, and long inventory/dialog/shop/tooltip UI content is windowed or clamped.
- Completed: stacked consumables and scrolls now consume exactly one item per successful use instead of deleting the whole stack.
- Completed: loading a save with pending perk choices reopens the level-up overlay, and Core pickup actions now resolve stack metadata from world content instead of relying on UI-supplied templates.
- Completed: UI chrome received an incremental Diablo II-inspired pass in Godot-facing scripts only: shared gothic palette, gold/parchment menu and sheet chrome, blood-red HUD HP styling, rarity-tinted inventory/tooltip text, muted combat log frame, and darker framed minimap colors. Core simulation behavior was not changed.
- Deferred: per-item chest loot selection needs persistent chest contents for safe leave-behind semantics; current v8-compatible behavior remains atomic open with inventory stow plus ground spill.

### Follow-up Status - GEN-1 Trap Persistence and Rendering

- Completed: added `TileType.Trap` to the dungeon tile enum so authored trap tiles have a distinct simulation type.
- Completed: added `TrapComponent` as the single source of truth for trap state (`TemplateId`, `IsArmed`, `IsRevealed`, `TriggerCount`) and removed the redundant `WorldState._traps` / `TrapState` dictionary.
- Completed: `SaveSerializer` persists `TrapComponent` as part of the owning entity and restores it on load; bumped normalized save version to `12`.
- Completed: `SaveMigrator.MigrateV11` converts legacy v10/v11 standalone `Traps` arrays into trap entities with `TrapComponent`; `SaveMigrator.MigrateV10` was updated to perform the same conversion so legacy v10 saves load correctly under version 12.
- Completed: `SaveValidator` validates trap entities (must sit on a `Trap` tile and have a non-empty template id) and allows non-blocking trap entities to share tiles with other entities.
- Completed: added persistence tests for trap component round-trip, multi-floor trap entities, v10/v11 migration to trap entities, validator tile consistency, and post-load trap triggering.
- Documentation: updated `docs/SYSTEMS.md` to save version `12` and revised the Traps persistence subsection to describe entity-owned `TrapComponent`.



- Completed: `AIParameters` record added and projected from `EnemyDefinition.AiParams` into `EnemyTemplate` by `ContentLoader.BuildEnemyTemplate`.
- Completed: `ContentLoader.ValidateEnemies` recognizes and validates all authored keys: `flee_hp_pct`, `aggro_range`, `wander_when_idle`, `preferred_range`, `min_range`, `patrol_radius`, `support_range`, `phase_through_walls`; unknown keys are rejected.
- Completed: `AIProfiles.ForTemplate` builds a runtime `AIProfile` from the base brain-type profile overridden by authored `ai_params`.
- Completed: `BrainFactory.Create(EnemyTemplate)` uses the template-derived profile; brain subclasses accept profile injection.
- Completed: `AIBrain` honors `aggro_range` for target detection, `phase_through_walls` by applying a permanent `Phased` status on first decision, and routes the phasing flag through `IPathfinder`.
- Completed: `UtilityScorer` honors `preferred_range`, `min_range`, and `support_range` for movement and support-ability scoring.
- Completed: `Pathfinder` supports a `phaseThroughWalls` flag for wall-passing path queries.
- Completed: `Content/enemies.json` goblin archer `preferred_range` changed to `4`; `Tests/AITests/AIParamsTests.cs` covers every recognized key and the concrete goblin-archer range-4 behavior.
- Completed: save/load brain rehydration from template — `SaveSerializer`, `SaveManager`, and `GameManager` now pass `IContentDatabase` through load paths, and `BrainFactory.Create(EnemyTemplate)` is used when an `EnemyComponent.TemplateId` is present, so authored `ai_params` survive after loading.
- Documentation: `docs/CONTENT.md` documents `ai_params`; `docs/IMPROVEMENT_SUGGESTIONS.md` and `docs/ARCHITECTURE.md` mark AI-1 done and remove the stale save/load rehydration gap.
- Verification: stub build, test project build, full harness (313 tests), and rendering-validation profile (272 tests) all pass with zero failures.

### Follow-up Status - GEN-2 Themed Floor Sets

- Completed: `RoomPrefab` now exposes `Tags` and `RoomPrefabDefinition.Tags` is projected into runtime prefabs by `DungeonGenerator.ResolvePrefabs`.
- Completed: `DungeonGenerator` maps depth to theme (`prison` for depths 1–3, `crypt` for depths 4–6, `magma` for depth 7+) and enables theme-preference filtering when at least four theme-matching prefabs fit the generated BSP leaves.
- Completed: `RoomPlacer.PlaceRooms` accepts an optional `themeTag`; per leaf it prefers fitting prefabs carrying the theme and falls back to all fitting prefabs when no theme match fits that leaf.
- Completed: `RoomData` carries an optional `Tags` list so placed-room metadata survives in `LevelData` for tests and diagnostics.
- Completed: `Content/room_prefabs.json` assigns theme tags to existing rooms and adds six new theme-specific rooms (`prison_block`, `prison_yard`, `crypt_niche`, `crypt_hall`, `magma_pit`, `magma_forge`).
- Completed: `Tests/GenerationTests/DungeonGeneratorTests.cs` adds `FloorThemeConstrainsPrefabSelection` (majority-theme assertion plus determinism) and `FallbackWhenThemeHasFewMatchingPrefabs` (connectivity under thin theme coverage).
- Documentation: `docs/CONTENT.md` documents the depth-to-theme mapping and tag authoring convention; `improvments.md` notes GEN-2 theme filtering is implemented.
- Verification: stub build and test project build pass; full harness and rendering-validation profile each have one pre-existing failure in `AI.Ability support brain prefers buffing allies` unrelated to generation changes.

### Follow-up Status - Wave 4 Simulation And Generation Polish

- Completed: trap state reconciliation. `TrapComponent` is now the single source of truth for trap state (`TemplateId`, `IsArmed`, `IsRevealed`, `TriggerCount`); the redundant `WorldState._traps` / `TrapState` runtime dictionary was removed. Save version bumped to `12`; legacy v10/v11 standalone trap arrays migrate into trap entities via `SaveMigrator.MigrateV11` (and `MigrateV10` updated for the same conversion). `SaveValidator` enforces trap entities sit on `TileType.Trap` tiles and carry a non-empty template id.
- Completed: GEN-2 themed floor sets. `RoomPrefab.Tags` and `RoomData.Tags` are projected from content; `DungeonGenerator` maps depth to theme (`prison` 1–3, `crypt` 4–6, `magma` 7+) and prefers theme-matching prefabs when at least four fit the BSP leaves, falling back to all valid prefabs otherwise. Six new themed rooms added to `Content/room_prefabs.json` and covered by generation tests.
- Completed: GEN-4 locked doors and keys. Added `TileType.LockedDoor`; `DungeonGenerator.PlaceLockedDoorsAndKeys` converts connecting doors for rooms with `lock_doors_on_enter: true` into locked doors and places a `dungeon_key` item in a reachable non-locked room. `OpenDoorAction` consumes one key from the actor's inventory and unlocks the door permanently. Locked state persists as map tiles and ground items.
- Completed: STA-4 / UI-3 status visuals. HUD renders a status badge row; `EntityRenderer` attaches status icon children to entity sprites; `EventBus.StatusEffectApplied` payload now carries the correct target id; `StatusEffectRemoved` is emitted when a status expires or is cleared.
- Completed: STA-5 status event payloads. `StatusEffectApplied` and `StatusEffectRemoved` events correctly identify the affected entity and are consumed by HUD/entity renderer updates.
- Completed: AI-2 ambush behavior. `AmbushBrain` waits while the player is visible but not adjacent, prefers wall/door-adjacent tiles when idle, and switches to `AttackAction` when the player becomes adjacent.
- Completed: AI-3 ranged kiting. `AIProfile.MinRange` is consumed from authored `min_range`; `RangedKiterBrain`/`UtilityScorer` penalize moves that do not increase distance when inside minimum range, using Chebyshev distance for range comparisons.
- Completed: AI-4 support behavior. `SupportBrain` selects nearest/most-damaged same-faction ally as a secondary objective, penalizes moves that block allied ranged attackers, and uses `FilterByRelation` to avoid harmful AoE casts that would hit allies unless `hits_allies` is true.
- Completed: AI-5 group aggro propagation. `AIStateComponent` carries `AlertPosition` and `AlertTurn`; when an enemy enters `Chase`/`Attack`, same-faction allies within `AlertRadius` with no target enter `Chase` toward the alert position; alerts decay after a few turns.
- Completed: CI/workflow hardening. Added `global.json` pinning SDK `8.0.0` with `latestFeature` roll-forward; updated `.github/workflows/ci.yml` with solution build, `dotnet format --verify-no-changes`, JSON syntax validation, NuGet and Godot caching, scheduled triggers, and failure artifact upload. Applied `dotnet format` across the solution.
- Completed: `dungeon_key` item and `dungeon_key.svg` asset added; content validation expected item count updated from 22 to 23.
- Completed: documentation updates. `docs/SYSTEMS.md` now documents locked doors; `docs/CONTENT.md` documents `lock_doors_on_enter`, theme tags, and `dungeon_key`; `docs/IMPROVEMENT_SUGGESTIONS.md` marks GEN-2, GEN-4, STA-4, STA-5, AI-2, AI-3, AI-4, AI-5, UI-3, and TST-5 done.
- Verification: 346 tests pass in the full harness; 299 tests pass in the rendering-validation profile. One pre-existing CS0109 warning in `Scripts/UI/Tooltip.cs` remains unresolved to avoid unrelated changes.

## Current Strengths

- `Core/` has no Godot dependency and remains suitable for deterministic tests.
- Runtime boundaries are documented well in `README.md`, `docs/ARCHITECTURE.md`, and `docs/SYSTEMS.md`.
- JSON-driven content is broad: items, enemies, abilities, status effects, loot, rooms, perks, NPCs, and dialogs.
- Save files are versioned and migrated through `SaveSerializer` and related persistence classes.
- Test coverage exists across simulation, AI, generation, content, persistence, rendering, UI, and integration flows.
- Equipment requirements are now enforced in `ToggleEquipAction` through `RequirementValidator`.

## Confirmed High-Priority Findings

### 1. Save/load loses behavior-critical entity components

`SaveSerializer` serializes only generic entity fields plus inventory, status effects, progression, identity, wallet, NPC, and merchant data.

Relevant files:

- `Core/Persistence/SaveSerializer.cs:40-69`
- `Core/Persistence/SaveSerializer.cs:332-400`
- `Core/Persistence/SaveSerializer.cs:541-654`

Missing from save/rehydration:

- `IBrain`
- `AbilitiesComponent`
- `CooldownComponent`
- `XpValueComponent`
- `ChestComponent`

Likely effects after loading:

- Enemies can lose AI behavior.
- Enemies can stop granting XP.
- Ability-using enemies can lose abilities/cooldowns.
- Chests can become inert entities without chest state or loot behavior.

Resume action:

Persist template IDs and component state for enemies/chests, then rehydrate components from content on load. Add save/load integration tests proving loaded enemies act, grant XP, retain abilities/cooldowns, and loaded chests can still open or remain opened as appropriate.

### 2. Save/load does not preserve deterministic combat RNG state

`WorldState` stores a `CombatResolver`, and `CombatResolver` owns mutable random state, but save data stores only `Seed` and `TurnNumber`.

Relevant files:

- `Core/Persistence/SaveSerializer.cs:216-234`
- `Core/Persistence/SaveSerializer.cs:253-260`
- `Core/Simulation/Actions/CastAbilityAction.cs:80-82`

`CastAbilityAction` also creates ad hoc RNG from seed, turn, and actor ID hash. This is deterministic-ish per execution but not a unified serializable stream.

Resume action:

Move all simulation randomness behind one serializable deterministic RNG service/state, or derive every roll from stable replayable inputs. Add a regression test: simulate N turns, save/load, simulate M more turns, and compare against an uninterrupted N+M run.

### 3. Consumable item effects drift from authored content

`ContentLoader.BuildUseEffect` emits strings such as `heal`, `apply_status:haste`, and `cast_ability:fireball`.

Relevant files:

- `Core/Content/ContentLoader.cs:610-629`
- `Core/Simulation/Actions/UseItemAction.cs:71-114`
- `Scripts/UI/UIActionFactory.cs:90-110`
- `Content/items.json`

Confirmed mismatches:

- `UseItemAction` parses `status:` and `apply:`, but content emits `apply_status:`.
- `cast_ability:*` is only executed if an `AbilityTemplate` is supplied to `UseItemAction`.
- `UIActionFactory` always constructs `new UseItemAction(actorId, itemInstanceId, template)` and never resolves `cast_ability` into an ability template or target.
- Heal amount comes from `Template.StatModifiers["heal"]` or fallback `5`, so verify that authored item effects actually populate that key as intended.

Resume action:

Replace stringly typed use effects with structured item-use data, or fully parse the current string prefixes. Then add content-backed tests for every consumable in `items.json`.

### 4. Ability kills do not share melee death/progression behavior

`CastAbilityAction` removes killed targets directly.

Relevant files:

- `Core/Simulation/Actions/CastAbilityAction.cs:98-123`
- `Core/Simulation/Actions/AttackAction.cs`
- `docs/PROGRESSION.md:151-159`

Melee kills have progression logic, while ability kills do not appear to award XP or run common death side effects.

Resume action:

Centralize death handling into a core service or helper used by melee, abilities, status ticks, traps, and future systems. Death handling should cover XP, kill counts, drops, logs, removal, and combat events.

### 5. `GameManager` is carrying too many responsibilities

`Scripts/Autoloads/GameManager.cs` is over 2,000 lines and coordinates service setup, save/load, generation, entity factories, floors, turn processing, debug travel, FOV, and progression/event bridging.

Resume action:

Keep `GameManager` as the Godot autoload facade, but extract small application services as features are touched:

- `RuntimeServiceFactory`
- `WorldSessionService`
- `FloorTransitionService`
- `EntityFactory`
- `TurnOrchestrator`
- `ProgressionEventBridge`

Do this incrementally while fixing functional issues; avoid a broad refactor-only pass.

### 6. Presentation appears to mutate authoritative visibility state

Architecture docs say rendering should mirror simulation state. Subagent review found visibility writes in both `GameManager` and `WorldView`.

Relevant files:

- `Scripts/Autoloads/GameManager.cs`
- `Scripts/World/WorldView.cs`

Resume action:

Make FOV ownership explicit. Prefer simulation/session code computes authoritative explored/visible state, while `WorldView` only reads it and maintains local render caches.

### 7. Content references many missing sprite/icon paths

Content references paths such as:

- `res://Assets/Sprites/items/sword_iron.png`
- `res://Assets/Sprites/ui/status_poison.png`
- `res://Assets/Sprites/enemies/rat.png`

Actual `Assets/Sprites/` mostly contains `0x72/`, `player_tiny_dungeon.png`, and `enemies/enemy_tiny_dungeon.png`.

Relevant files:

- `Content/items.json`
- `Content/status_effects.json`
- `Content/enemies.json`
- `Assets/Sprites/`

Resume action:

Either add the referenced assets, repoint content to existing assets, or treat those fields as metadata and prevent runtime loading. Add a resource existence validation test for `res://` paths. The current Godot stubs can mask missing resources by returning generic objects.

### 8. Workflow and CI are hardened

Confirmed workflow state:

- `.github/workflows/ci.yml` exists and runs on push, pull request, `workflow_dispatch`, and a weekly Sunday schedule.
- CI pins the .NET SDK via `global.json` (`8.0.0` with `latestFeature` roll-forward).
- CI caches NuGet packages and the Godot 4.4.1 Mono download with `actions/cache@v4`.
- CI validates JSON syntax for all files under `Content/`, verifies formatting with `dotnet format --verify-no-changes`, builds the full solution, builds the Godot stub profile, runs the full custom test harness, and runs the rendering validation profile.
- A second CI job performs a real Godot 4.4.1 Mono headless editor import and startup smoke test.
- `godotussy.sln` includes both `godotussy.csproj` and `Tests/godotussy.Tests.csproj`.
- The canonical editorless build command remains `dotnet build godotussy.csproj -p:UseGodotStubs=true`.

Known remaining workflow gaps:

- Build warnings are not yet treated as errors in CI.
- The repository currently fails `dotnet format --verify-no-changes`; code needs a one-time formatting pass before the new CI check will be green.

Relevant files:

- `.github/workflows/ci.yml`
- `global.json`
- `godotussy.sln`
- `README.md`
- `docs/TESTING.md`
- `docs/IMPROVEMENT_SUGGESTIONS.md`

## Medium-Priority Findings

- Cooldowns tick globally after every processed `GameLoop.ProcessRound`, not necessarily when the owning actor takes a turn. See `Core/Simulation/GameLoop.cs:43-48`.
- Some AI/content parameters may still be schema-only if not mapped into runtime brain construction.
- Many item stat keys should be audited against implemented stat application. `ToggleEquipAction` applies HP, max HP, attack, accuracy, defense, evasion, speed, and view radius, but authored keys like block chance, magic resist, regen, or specialized passive effects need runtime support or validation.
- Floor cache persistence needs a design decision. If previous floors should persist across saves, the current single-world save shape is insufficient.
- `improvments.md` contains stale entries that describe already-implemented systems as missing, which can mislead future agents.

## Recommended Resume Plan

### Phase 0: Tooling Baseline

Goal: know whether the repository is currently green.

- Install .NET 8 SDK and Godot 4.4.1 Mono/.NET.
- Run the verification commands listed above.
- Add a minimal CI workflow for stub build and tests. Status: completed; CI now also validates formatting, JSON syntax, solution build, and caches NuGet/Godot.
- Add `Tests/godotussy.Tests.csproj` to the solution or document that solution build is not full validation. Status: completed; the solution already includes the test project.
- Add or update `global.json` if a specific .NET SDK feature band is expected. Status: completed; `global.json` pins `8.0.0` with `latestFeature` roll-forward.

Acceptance criteria:

- CI runs on every push/PR, `workflow_dispatch`, and weekly schedule.
- README and testing docs list the same commands CI runs, including formatting and JSON syntax checks.
- Test count documentation is accurate.

### Phase 1: Persistence Correctness

Goal: save/load does not silently break runs.

- Persist or rehydrate enemy template identity, brain type/state, abilities, cooldowns, XP value, and chest component state.
- Decide whether multi-floor cache state is saved or intentionally regenerated.
- Add deterministic replay tests across save/load.

Acceptance criteria:

- Loaded enemies can act.
- Loaded enemies grant XP.
- Loaded ability enemies can cast after load.
- Loaded chests behave correctly.
- Save/load replay matches uninterrupted deterministic simulation for covered scenarios.

### Phase 2: Item And Ability Runtime Integration

Goal: authored content does what it says.

- Replace or fully parse string use effects.
- Wire `cast_ability` items through UI targeting and `AbilityTemplate` lookup.
- Define tile targeting semantics.
- Add content-driven tests for all consumables and scrolls.
- Validate unsupported item stat/effect keys as errors or warnings.

Acceptance criteria:

- Health, haste, might, phase, fireball, blink, and other consumables behave according to content.
- Using a scroll either prompts for a valid target or clearly fails without consuming the item.
- Content tests fail when a new effect key has no runtime implementation.

### Phase 3: Centralize Death And Progression

Goal: all kill sources produce consistent outcomes.

- Move kill/XP/level-up mutation out of individual action implementations.
- Follow the direction in `docs/PROGRESSION.md` to make level-up choices content-driven.
- Ensure ability/status/trap/environment kills share the same death pipeline.

Acceptance criteria:

- Melee and ability kills both award XP and update kill counts.
- Death logs/events are consistent.
- Level-up state is testable outside presentation code.

### Phase 4: Presentation Boundary Cleanup

Goal: avoid state desync while keeping changes incremental.

- Make one owner for visibility/exploration mutation.
- Extract `GameManager` responsibilities only when touching related behavior.
- Add tests around event payloads and status effect target reporting.

Acceptance criteria:

- `WorldView` does not mutate authoritative simulation state.
- Status/combat UI can identify which entity received each effect.
- `GameManager` shrinks through targeted extractions, not a risky rewrite.

### Phase 5: Content, Assets, And Progression Expansion

Goal: resume feature development on a stable base.

- Fix missing sprite/icon references.
- Update `improvments.md` into done/partial/open status or archive it.
- Continue `docs/PROGRESSION.md` with perk choices, NPC services, and floor milestone rewards.

Acceptance criteria:

- Resource validation passes.
- Docs reflect current state.
- New content has runtime behavior and tests.

## Suggested First Tickets

1. Add CI for .NET 8 stub build and custom test runner. Status: completed for stub build, test build, full harness run, and rendering validation.
2. Add a save/load regression test that proves enemies still act and grant XP after load.
3. Persist/rehydrate enemy and chest behavior components.
4. Fix `UseItemAction` parsing for `apply_status:` and `cast_ability:`. Status: completed per follow-up documentation; keep content-backed coverage current.
5. Add content-backed consumable tests for every item with `on_use` effects.
6. Centralize death handling so ability kills award XP. Status: completed for melee and ability-damage kills; status/trap attribution remains.
7. Add resource path validation for `res://` content references.
8. Update README/docs for Godot 4.4.1 Mono/.NET and harness-reported test counts. Status: completed.

## Bottom Line

Do not add more enemies, items, or progression content yet. The codebase is ready for development, but the next work should make existing authored systems reliable: persistence, deterministic RNG, item/ability execution, shared death/progression, and CI. Once those are green, `docs/PROGRESSION.md` is the best roadmap for expanding the game.
