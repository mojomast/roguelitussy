# Testing

The project uses a custom .NET test harness instead of xUnit or NUnit.

## How Tests Run

`Tests/Program.cs` reflects over the test assembly, instantiates every concrete `ITestSuite`, registers its tests with `TestRegistry`, and executes them.

`TestRegistry` prints a pass/fail line per executed test and returns a non-zero exit code when any test fails. Failures include the test name, a separator, and `ex.ToString()` output so exception type, message, and stack trace are visible in CI logs.

The harness resets mutable Godot stub state before every registered test. This clears missing resource/image path sets, pressed mouse buttons, and shared viewport/tree state so UI and rendering tests do not depend on execution order.

## Standard Commands

Run the full suite:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
```

Run a filtered subset by case-insensitive test-name match:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -- --filter Simulation.
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -- --filter=TestFramework.
```

An empty filter runs all tests. A nonmatching filter executes zero tests, prints a summary with executed/registered/skipped/failure counts, and exits successfully.

The harness prints the current test count at runtime; avoid hardcoding exact counts in docs unless they are generated from the harness output.

Build the editorless Godot stub profile before a larger change set:

```powershell
dotnet build godotussy.csproj -p:UseGodotStubs=true
```

Solution builds include the custom test project, but they do not execute the harness. Use the explicit project commands above for canonical editorless stub validation:

```powershell
dotnet build godotussy.sln
```

Build the test project explicitly:

```powershell
dotnet build Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
```

Run the rendering-focused compile profile:

```powershell
dotnet restore godotussy.csproj -p:UseGodotStubs=true -p:RenderingValidation=true
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -p:RenderingValidation=true
```

The rendering profile defines `RENDERING_VALIDATION`, excludes `Core/Persistence/**/*.cs`, and skips persistence-dependent tests. It is a compile/runtime smoke for rendering and UI surfaces, not a replacement for the full persistence suite.

`Core/Persistence/MetaProgressionData.cs` is explicitly re-included in the rendering profile so the `MetaProgressionManager` autoload compiles while the rest of the save subsystem stays excluded.

Do not run the full harness and rendering-validation harness in parallel from the same checkout. They use different build profiles for the Godot-facing project and should be run sequentially to avoid shared output races.

## Continuous Integration Checks

The repository workflow at `.github/workflows/ci.yml` runs on every push and pull request, and also supports `workflow_dispatch` plus a weekly Sunday schedule. The .NET job performs:

- SDK pinning via `global.json` (`8.0.0` with `latestFeature` roll-forward).
- NuGet package caching.
- JSON syntax validation for all files under `Content/`.
- `dotnet format --verify-no-changes godotussy.sln`.
- The editorless stub build, test build, full harness, and rendering-validation profile.
- Warnings-as-errors on CI compile steps through explicit `-p:RoguelitussyWarningsAsErrors=true` opt-in.
- Artifact upload of `bin/`, `obj/`, and Godot cache directories on failure.

A second job downloads and caches Godot 4.5.2 Mono, runs a headless editor import, and runs a headless startup smoke test. The Godot version is centralized in a single workflow environment variable.

Use the same warnings-as-errors property locally when validating CI compile behavior:

```powershell
dotnet build godotussy.csproj -p:UseGodotStubs=true -p:RoguelitussyWarningsAsErrors=true
dotnet build Tests/godotussy.Tests.csproj -p:RoguelitussyWarningsAsErrors=true
dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -p:RenderingValidation=true -p:RoguelitussyWarningsAsErrors=true
```

## Godot Headless Smoke

CI also runs a real Godot 4.5.2 Mono headless check in addition to the editorless .NET stub profile.

Use this locally after changing scenes, autoloads, imported assets, Godot project settings, or Godot-facing startup code:

```powershell
godot --headless --editor --path . --quit
godot --headless --path . --quit
```

The editor import step regenerates ignored `.godot/imported/*.ctex` resources from committed assets and `.import` files. Do not commit `.godot/`; it is machine-generated cache state.

## Test Areas

The repository contains targeted suites for:

- AI
- ability casting and cooldown behavior
- content loading and validation
- generation
- integration flows
- persistence
- progression and save migration
- rendering
- simulation
- UI
- character creation and equipment UX
- Track 7 roguelite UI integration, including relic HUD text, boss/streak indicators, archetype preview updates, and component persistence for relic/shrine/streak/archetype state
- architecture smoke coverage

## Writing New Tests

1. Add a class that implements `ITestSuite`.
2. Register each test through the provided registry.
3. Keep tests deterministic and self-contained.
4. Prefer focused tests around one subsystem or regression.

Stub-backed tests may rely on the harness-level reset for shared Godot stub state, but each test should still set up its own required scene/input/resource state explicitly.

When changing simulation, persistence, generation, or content loading, add or update tests in the same change.

## Testing Guidance By Change Type

- Simulation/action changes: add simulation or integration tests.
- Save-format or migration changes: add persistence serialization and migration coverage.
- Content loader changes: add content tests and at least one failure-path case when practical.
- Rendering or UI changes: add rendering or UI tests where the behavior is covered by the stubbed environment.
- Authored visual path changes: update the content art-path audit and run the full harness; run Godot headless editor import/startup when assets are added or renamed.
- AI behavior changes: add deterministic brain or scorer coverage instead of relying on manual playtesting.

## Determinism Expectations

Tests should avoid hidden time, file-system, or random-number dependencies unless the behavior under test is explicitly about those concerns. If randomness is involved, drive it from a known seed.
