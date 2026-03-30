# godotussy

A deterministic roguelike engine built for Godot 4.4 with a pure C# simulation core.

## What’s Here

This repository contains a grid-based roguelike foundation with:
- Deterministic world state and persistence
- Energy-based turn scheduling
- Action-driven simulation for movement, combat, items, doors, and stairs
- BSP dungeon generation with connectivity validation
- Utility-driven enemy AI and A* pathfinding
- Godot-side world rendering, FOV, UI, and editor/debug tooling
- A custom .NET test harness covering simulation, generation, content, rendering, persistence, and UI behaviors

## Project Layout

- `Core/`
  Pure C# engine code: contracts, simulation, AI, generation, content, and persistence.
- `Scripts/`
  Godot-facing autoloads, world rendering, UI, and tools.
- `Scenes/`
  Scene definitions for the main world, UI, and tools.
- `Content/`
  Data-driven JSON content for items, enemies, loot, abilities, room prefabs, and status effects.
- `Tests/`
  Custom test runner plus subsystem suites and stubs.
- `Compat/Godot/`
  Build-time Godot stubs used so the solution can compile and test without a local Godot editor runtime.

## Build And Test

Build the full solution:

```powershell
dotnet build godotussy.sln
```

Run the full test suite:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj
```

## Current Validation Status

Latest validation completed during this update:
- `dotnet build godotussy.sln`
- `dotnet run --project Tests/godotussy.Tests.csproj`

Both succeeded, with `92` passing tests.

## Notable Engine Details

- Simulation code stays under `Core/` and does not depend on Godot types.
- Save files now use packed visibility/exploration flags and include migration from older formats.
- The rendering layer listens to `EventBus` rather than mutating simulation state directly.
- `Core/Simulation/GameLoop.cs` now exists as a pure orchestration surface for round execution.

## Working In The Repo

- Keep gameplay mutations inside `IAction.Execute()` implementations.
- Keep cross-system notifications flowing through `Scripts/Autoloads/EventBus.cs`.
- Prefer adding coverage in `Tests/` when changing simulation, persistence, or generation logic.
