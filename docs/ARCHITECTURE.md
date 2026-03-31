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
- Bridge combat and state changes into `EventBus`
- Handle save/load requests through `ISaveManager`
- Emit world snapshots and floor transitions

### EventBus

`Scripts/Autoloads/EventBus.cs` is the shared notification surface between systems. It exposes gameplay, rendering, persistence, and UI lifecycle events such as:

- turn started/completed
- entity turn started
- damage dealt
- HP changed
- floor changed
- save/load requested and completed
- level generated/transitioned
- equipment changed
- game over

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
- New HUD, menu, overlay, or input behavior: `Scripts/UI/`
- New world animation or renderer: `Scripts/World/`
- New editor/debug/runtime-authoring utility: `Scripts/Tools/` plus a scene, plugin hook, or `UiRoot` integration if needed

## Determinism Rules

- Keep world mutation in `Core/`.
- Avoid introducing Godot randomness into the simulation layer.
- Use explicit seed-driven generation paths for reproducible level creation.
- Treat persistence as part of the deterministic contract; format changes require migration and validation.