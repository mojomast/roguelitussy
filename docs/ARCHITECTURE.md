# Architecture

The repository is split into a deterministic simulation core and a Godot-facing presentation layer.

## Layer Boundaries

### Core

`Core/` contains the engine model and rules:

- Contracts such as `IAction`, `IEntity`, `IBrain`, `IGenerator`, `IPathfinder`, `ISaveManager`, and `ITurnScheduler`
- Simulation state and mechanics
- Dungeon generation
- Content loading and validation
- Save serialization, validation, and migration

`Core/` should not take runtime dependencies on Godot types.

### Godot Integration

`Scripts/` and `Scenes/` host the engine-facing adapters and presentation layer:

- `Scripts/Autoloads/` for runtime coordination and events
- `Scripts/World/` for rendering, animation, and FOV presentation
- `Scripts/UI/` for menus, HUD, inventory, logs, and input routing
- `Scripts/Tools/` for debug, editor, and in-app authoring utility scripts
- `Scenes/` for scene resources consumed by those scripts

### Content

`Content/` contains JSON documents loaded by `ContentLoader`. These files define content data, not runtime code.

### Tests

`Tests/` runs as a standalone executable with a custom registry-based harness. It references the main project and exercises the engine without requiring the Godot editor.

## Boot Flow

At runtime the project starts from `Scenes/Main.tscn`.

1. Godot loads the autoloads declared in `project.godot`.
2. `GameManager` coordinates the loaded world, scheduler, generator, save manager, and event flow.
3. `EventBus` exposes the project-wide notification surface used by UI and world scripts.
4. `ContentDatabase` holds the loaded `IContentDatabase` instance for Godot-side consumers.
5. `UiRoot` binds itself to the active `GameManager`, `EventBus`, and content services.
6. `UiRoot` also owns the in-app developer workshop and debug surfaces so runtime authoring flows stay outside the simulation core.

`GameManager` also seeds the runtime-only bridge state that the simulation now depends on at action time: player progression and identity components, enemy XP values, enemy ability slots, and the world's loaded content database reference.

## Runtime Flow

The intended direction of dependencies is:

1. Input or AI chooses an `IAction`.
2. `GameManager` routes the turn through `Core/Simulation/GameLoop.cs`.
3. The action validates and mutates `WorldState` inside `Core/`.
4. `GameManager` emits state changes through `EventBus`.
5. UI and rendering scripts react to those events.

This keeps the simulation authoritative and the presentation layer reactive.

## Important Autoloads

### GameManager

`Scripts/Autoloads/GameManager.cs` is the high-level session coordinator. It owns the active world reference, current floor, game state, attached services, and the player-action entry point.

Key responsibilities:

- Start and load sessions
- Route player actions into the simulation loop
- Bridge combat, progression, equipment, and state changes into `EventBus`
- Own authoritative visibility/exploration updates for the active `WorldState`
- Handle save/load requests through `ISaveManager`
- Emit world snapshots and floor transitions
- Attach runtime components such as `ProgressionComponent`, `IdentityComponent`, `XpValueComponent`, `AbilitiesComponent`, and `CooldownComponent`

Planned direction: keep `GameManager` as the stable Godot autoload facade, but continue extracting implementation details into focused Godot-side services. UI code should continue calling facade-level methods while services move behind it. The long-term target is for `GameManager.cs` to stay under 500 lines by delegating session setup, save/load binding, turn orchestration, spawning, floor travel, progression event bridging, and autoplay behavior.

### EventBus

`Scripts/Autoloads/EventBus.cs` is the shared notification surface between systems. It exposes gameplay, rendering, persistence, and UI lifecycle events such as:

- turn started/completed
- damage dealt
- healed
- HP changed
- floor changed
- save/load requested and completed
- level transitioned
- equipment changed
- experience gained
- leveled up
- game over

`GameOverWithStats` is the canonical run-ended integration event. `GameOver` is emitted once afterward for compatibility. The former `EntityTurnStarted`, `LevelGenerated`, and `AscensionLevelChanged` surfaces have been removed.

If a presentation system needs to react to a simulation change, prefer adding or using an event here rather than introducing direct references into `Core/`.

### ContentDatabase

`Scripts/Autoloads/ContentDatabase.cs` is a thin Godot node that exposes the loaded `IContentDatabase` to scene-side consumers.

## Runtime Tooling

`Scripts/Tools/DevToolsWorkbench.cs` is the runtime authoring overlay exposed from the title screen, pause menu, and gameplay hotkeys. It wraps the lower-level `MapEditor`, `ItemEditor`, and debug-command tooling so the foundation can be extended from inside the actual app.

This keeps authoring utilities on the Godot side while still allowing non-editor workflows:

- `MapEditor` remains the room-prefab backend
- `ItemEditor` remains the item and enemy document backend
- `DevToolsWorkbench` is the menu-driven runtime shell around those tools
- `DebugConsole` remains the freeform escape hatch for direct commands

## Where New Code Belongs

- New gameplay rule or state mutation: `Core/Simulation/` or another `Core/` subsystem
- New action: `Core/Simulation/Actions/`
- New AI decision rule: `Core/AI/`
- New procedural-generation step: `Core/Generation/`
- New persistence shape or migration: `Core/Persistence/`
- New reusable entity state: a focused component under `Core/Simulation/`
- New HUD, menu, overlay, or input behavior: `Scripts/UI/`
- New world animation or renderer: `Scripts/World/`
- New editor/debug/runtime-authoring utility: `Scripts/Tools/` plus a scene, plugin hook, or `UiRoot` integration if needed
- New item/status/menu source art: `Assets/Sprites/` plus content-path tests; keep runtime handling soft so art metadata does not become simulation state.

## Determinism Rules

- Keep world mutation in `Core/`.
- Avoid introducing Godot randomness into the simulation layer.
- Use explicit seed-driven generation paths for reproducible level creation.
- Treat persistence as part of the deterministic contract; format changes require migration and validation.

## Agent Onboarding Reference

Quick-reference entry points for agents working on this codebase.

### Core layer entry points

| Concern | File | Line | What to know |
|---|---|---|---|
| Authoritative state | `Core/Contracts/WorldState.cs` | 7 | Grid, entity index, visibility, RNG state hooks |
| Entity model | `Core/Simulation/Entity.cs` | 6 | Component bag; all state lives in components |
| Action contract | `Core/Contracts/IAction.cs` | 5 | `Validate`, `Execute`, `GetEnergyCost`, `ActionOutcome` |
| Turn loop | `Core/Simulation/GameLoop.cs` | 7 | `ProcessRound` drives actors and ticks cooldowns |
| Scheduler | `Core/Simulation/TurnScheduler.cs` | 7 | Energy-based; status ticks happen in `ConsumeEnergy` |
| Combat | `Core/Simulation/CombatResolver.cs` | 6 | Deterministic hit/damage/crit/on-hit |
| Relic hooks | `Core/Simulation/Relics/RelicProcessor.cs` | — | Shared outgoing/incoming damage and lifecycle hooks |
| Death | `Core/Simulation/DeathResolver.cs` | 6 | Central kill handler; awards XP |
| Items | `Core/Simulation/Actions/UseItemAction.cs` | 6 | `heal`, `apply_status`, `cast_ability`, `cure` |
| Abilities | `Core/Simulation/Actions/CastAbilityAction.cs` | 6 | Damage/status/teleport/heal_self |
| Ability targets | `Core/Simulation/AbilityResolver.cs` | 9 | Only self/single/tile/aoe_circle implemented |
| Statuses | `Core/Simulation/StatusEffectProcessor.cs` | 19 | Data-driven status definitions with legacy fallbacks; `tick_timing`/expire hooks remain follow-up work |
| Progression | `Core/Simulation/ProgressionService.cs` | 7 | XP/level-up helpers |
| Content load | `Core/Content/ContentLoader.cs` | 122 | `LoadFromDirectory` entry point |
| Save | `Core/Persistence/SaveManager.cs` | 9 | File I/O and slot management |
| Serialize | `Core/Persistence/SaveSerializer.cs` | 9 | JSON round-trip, current version 17 (see `SaveSerializer.CurrentVersion`) |

### Godot layer entry points

| Concern | File | Line | What to know |
|---|---|---|---|
| Autoloads | `project.godot` | 11 | `GameManager`, `EventBus`, `ContentDatabase` |
| Facade | `Scripts/Autoloads/GameManager.cs` | 10 | Owns world, services, turn processing, save/load |
| Events | `Scripts/Autoloads/EventBus.cs` | 7 | Large, growing set of events spanning turn flow, combat, inventory, progression, and UI; GameManager emits, UI/World subscribes (see file for the current list) |
| Input | `Scripts/UI/UIRoot.cs` | 162 | `_UnhandledInput` routes all keys |
| Input mapping | `Scripts/UI/InputHandler.cs` | 37 | Key-to-action mapping |
| Action factory | `Scripts/UI/UIActionFactory.cs` | 7 | Builds `IAction` from UI intent |
| World render | `Scripts/World/WorldView.cs` | 83 | Mirrors `WorldState`; no FOV computation |
| Entity sprites | `Scripts/World/EntityRenderer.cs` | — | Procedural sprites from catalogs |
| Catalogs | `Scripts/World/WorldArtCatalog.cs` | 16 | Builds `res://Assets/...` paths |
| Shared colors | `Scripts/World/RenderPalette.cs` | — | Central world, entity, targeting, and popup colors |

### Data flow cheat sheet

Player turn:
`UIRoot._UnhandledInput` → `InputHandler.HandleKey` → `UIActionFactory` → `EventBus.PlayerActionSubmitted` → `GameManager.ProcessPlayerAction` → `IAction.Execute` + `GameLoop.ProcessRound` → `WorldState` mutation → `EventBus` → `WorldView`/UI refresh.

Save/load:
`GameManager.SaveToSlot`/`LoadFromSlot` → `SaveManager` → `SaveSerializer`/`SaveMigrator` → JSON. `CombatRandomState` and `ItemRandomState` round-trip per floor.

Content:
`ContentLoader.LoadFromDirectory` → JSON validation → template projection → `IContentDatabase` → consumed by `GameManager`, actions, and tests.

### Coupling points to treat carefully

- `GameManager` is the only Godot-side owner of `WorldState` for normal gameplay. Do not let other presentation scripts mutate state outside debug tooling.
- `EventBus` events should carry enough payload to identify affected targets (actor, target, positions). GameManager currently emits some events with the actor ID where the target ID would be more useful.
- `WorldView` reads `WorldState` visibility flags; `GameManager`/`WorldState` compute them. Do not move FOV ownership into `WorldView`.
- Actions are the only legitimate gameplay mutation path. Debug tools are intentionally exempt but make deterministic replay/save harder.

### Known wiring gaps

- Quick-use hotbar intentionally refuses aimed scrolls; use the inventory targeting overlay for `cast_ability:` items that require a field target.
- Status definitions are partially data-driven, but authored `tick_timing` and `on_expire_effects` are not yet fully enforced by normal turn processing.
- `aoe_line` and `aoe_cone` ability targeting validate but are not resolved at runtime.
- `GameManager` directly mutates `WorldState` for map reveal, teleport, and floor travel.
- `GameManager.cs` is still oversized; remaining extraction work is tracked as non-blocking architecture backlog and should not delay UI polish that only consumes the existing facade API.
- Enemy rendering still uses `WorldArtCatalog` name mappings rather than authored `EnemyTemplate.SpritePath` values.
- Floor-event tags influence room placement, but shrine/curse entities and event metadata are not yet fully propagated into world population.
