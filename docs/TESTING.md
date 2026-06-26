# Testing

The project uses a custom .NET test harness instead of xUnit or NUnit.

## How Tests Run

`Tests/Program.cs` reflects over the test assembly, instantiates every concrete `ITestSuite`, registers its tests with `TestRegistry`, and executes them.

`TestRegistry` prints a simple pass/fail line per test and returns a non-zero exit code when any test fails.

## Standard Commands

Run the full suite:

```powershell
dotnet run --project Tests/godotussy.Tests.csproj
```

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
dotnet build Tests/godotussy.Tests.csproj
```

Run the rendering-focused compile profile:

```powershell
dotnet restore godotussy.csproj -p:UseGodotStubs=true -p:RenderingValidation=true
dotnet run --project Tests/godotussy.Tests.csproj -p:RenderingValidation=true
```

The rendering profile defines `RENDERING_VALIDATION`, excludes `Core/Persistence/**/*.cs`, and skips persistence-dependent tests. It is a compile/runtime smoke for rendering and UI surfaces, not a replacement for the full persistence suite.

Do not run the full harness and rendering-validation harness in parallel from the same checkout. They use different build profiles for the Godot-facing project and should be run sequentially to avoid shared output races.

## Continuous Integration Checks

The repository workflow at `.github/workflows/ci.yml` runs on every push and pull request, and also supports `workflow_dispatch` plus a weekly Sunday schedule. The .NET job performs:

- SDK pinning via `global.json` (`8.0.0` with `latestFeature` roll-forward).
- NuGet package caching.
- JSON syntax validation for all files under `Content/`.
- `dotnet format --verify-no-changes godotussy.sln`.
- `dotnet build godotussy.sln`.
- The editorless stub build, test build, full harness, and rendering-validation profile.
- Artifact upload of `bin/`, `obj/`, and Godot cache directories on failure.

A second job downloads and caches Godot 4.4.1 Mono, runs a headless editor import, and runs a headless startup smoke test. The Godot version is centralized in a single workflow environment variable.

## Godot Headless Smoke

CI also runs a real Godot 4.4.1 Mono headless check in addition to the editorless .NET stub profile.

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
- architecture smoke coverage

## Writing New Tests

1. Add a class that implements `ITestSuite`.
2. Register each test through the provided registry.
3. Keep tests deterministic and self-contained.
4. Prefer focused tests around one subsystem or regression.

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
