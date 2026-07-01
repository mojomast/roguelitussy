# godotussy

A deterministic roguelike foundation for Godot 4.5.2 Mono/.NET with a pure C# simulation core, Godot-facing presentation scripts, data-driven content, and a custom .NET test harness.

The current build includes character identity and progression, ability casting, gear-driven combat, role-specific AI, equipment requirements, authoritative GameManager-owned visibility, refreshed CC0 0x72 dungeon art with contextual wall layering, committed SVG item/status icons, clearer menu and inventory presentation, reconciled world-space movement, overlay-aware gameplay chrome, expanded enemy sprite coverage, and broader mid- and late-floor content.

## Quick Start

1. Install .NET 8 SDK.
   If a system package is unavailable, the Microsoft user-local installer works for this repo; set `DOTNET_ROOT=$HOME/.dotnet` and put `$HOME/.dotnet` on `PATH` before running the commands below.
2. Install Godot 4.5.2 Mono/.NET if you want to open or run the game inside the editor.
3. Build the editorless Godot stub profile:

   ```powershell
   dotnet build godotussy.csproj -p:UseGodotStubs=true
   ```

4. Build and run the automated test harness:

   ```powershell
   dotnet build Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
   dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
   ```

5. Open `project.godot` in Godot 4.5.2 Mono/.NET to inspect scenes, autoloads, and editor tools.

6. Launch the playable shell and use the built-in developer workshop from the title screen or pause menu if you want to author rooms and content without opening the Godot editor.

## What This Project Contains

- A pure C# simulation layer for entities, actions, combat, abilities, inventory, AI, generation, and persistence.
- Godot-side autoloads and presentation scripts for UI, rendering, and debug/editor tooling.
- Layered 0x72 world rendering with contextual wall caps, trims, and sprite-backed entity presentation for the current enemy roster.
- SVG-backed item/status icon source art and content-path validation for authored `res://` visuals.
- An in-app developer workshop for creating room drafts and scaffolding item/enemy content directly from the runtime shell.
- JSON-driven content for items, enemies, abilities, status effects, loot tables, room prefabs, perks, NPCs, dialogs, relics, floor events, and meta upgrades.
- Save/load infrastructure with validation and migration support for progression and identity state.
- A custom test runner covering simulation, AI, generation, content, persistence, rendering, UI, and integration flows.

## Repository Layout

- `Core/` - engine code with no Godot runtime dependency.
- `Scripts/` - Godot-facing autoloads, UI, world rendering, and tools.
- `Scenes/` - scene resources for the game shell, UI, and editor/debug tools.
- `Content/` - authoritative JSON content files loaded by `ContentLoader`.
- `Compat/Godot/` - compile-time stubs so the solution can build and test without a local Godot editor runtime.
- `Tests/` - custom test runner, subsystem suites, and test doubles.
- `Addons/roguelike_tools/` - Godot editor plugin that exposes project tools.

## Runtime Entry Points

- Main scene: `Scenes/Main.tscn`
- Project autoloads:
  - `GameManager` - simulation/session coordinator.
  - `EventBus` - cross-system event hub.
  - `ContentDatabase` - bridge from Godot autoload space to the loaded content database.
  - `MetaProgressionManager` - persistent between-run Echo balance, upgrades, and recent run history.

## Documentation Index

- [docs/SETUP.md](docs/SETUP.md) - prerequisites, first-time setup, build/test commands, and opening the project in Godot.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) - system boundaries, runtime flow, and where new code belongs.
- [docs/SYSTEMS.md](docs/SYSTEMS.md) - turn loop, AI, generation, rendering, and persistence behavior.
- [docs/CONTENT.md](docs/CONTENT.md) - content file inventory, schema expectations, and authoring rules.
- [docs/TOOLS.md](docs/TOOLS.md) - editor plugin, debug tools, and in-project utility surfaces.
- [docs/TESTING.md](docs/TESTING.md) - custom test harness usage and subsystem coverage.
- [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) - code placement rules, workflow expectations, and review checklist.

## Core Development Rules

- Keep deterministic gameplay state and mutations inside `Core/`.
- Keep Godot-specific code in `Scripts/`, `Scenes/`, or `Addons/`.
- Add or update `IAction` implementations when introducing new gameplay actions.
- Emit cross-system notifications through `Scripts/Autoloads/EventBus.cs` instead of wiring presentation code directly into simulation logic.
- Extend tests whenever you touch simulation, persistence, generation, or content-loading behavior.

## Common Commands

Build the editorless stub profile:

```powershell
dotnet build godotussy.csproj -p:UseGodotStubs=true
```

Build the solution, including the test project for IDE/structural coverage:

```powershell
dotnet build godotussy.sln
```

Verify formatting matches the repository style (CI enforces this):

```powershell
dotnet format --verify-no-changes godotussy.sln
```

Run the CI warnings-as-errors compile checks locally:

```powershell
dotnet build godotussy.csproj -p:UseGodotStubs=true -p:RoguelitussyWarningsAsErrors=true
dotnet build Tests/godotussy.Tests.csproj -p:RoguelitussyWarningsAsErrors=true
```

Validate JSON content syntax locally (CI runs the same check):

```powershell
Get-ChildItem -Recurse -Filter *.json -Path Content | ForEach-Object { python3 -m json.tool $_.FullName > $null }
```

Build the test project:

```powershell
dotnet build Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
```

Run the full test suite:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
```

Run a filtered test subset:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -- --filter Simulation.
```

Run the rendering-focused compile profile:

```powershell
dotnet restore godotussy.csproj -p:UseGodotStubs=true -p:RenderingValidation=true
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -p:RenderingValidation=true
```

The rendering validation profile intentionally excludes persistence implementation files and persistence-backed tests so rendering/UI code can compile against the Godot stubs without requiring the full save subsystem in that profile.

Run the game headlessly to validate startup:

```powershell
Godot_v4.5.2-stable_mono_linux_x86_64 --headless --editor --path . --quit
Godot_v4.5.2-stable_mono_linux_x86_64 --headless --path . --quit
```

Run the editor import step first in fresh checkouts so ignored Godot import products under `.godot/` are regenerated from committed assets and `.import` files.

## Notes For Contributors

- The simulation layer intentionally avoids Godot imports so it remains testable and deterministic.
- Save data is versioned and migrated on load; do not change persistence shapes casually.
- Content IDs are expected to be stable lowercase snake_case keys.
- The runtime shell now exposes a developer workshop for room and content authoring, so editor plugin usage is optional for common content-building tasks.
- The custom harness prints the current deterministic test count; keep new gameplay, rendering, and content changes covered in the same style.
- Temporary root-level `.cs` scratch files are included by the SDK globbing rules and can break builds.

Start with [docs/SETUP.md](docs/SETUP.md) if you are new to the repository, then use [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) before making structural changes.
