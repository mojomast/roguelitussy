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

## Latest Verification - 2026-09-08

- Full strict suite: 734/734 passed.
- Rendering validation: 663/663 passed.
- Real Godot 4.5.2 API build: zero warnings/errors.
- `dotnet format --verify-no-changes` and `git diff --check`: passed.

The replayability coverage includes save/load-stable perk drafts, synergy trigger behavior, landmark/key validation, nine-floor completion and first-clear unlock idempotence, ranged weapon range/LOS input, retryable daily state and Thursday speed scoring, merchant reputation discounts, dangerous-status wait suppression, and onboarding/game-over actions.

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
- UI text fitting, compact inventory text bounds, title seed entry, start-game action routing, Echo Workshop close routing, HUD action feedback, and combat-log readability/filter behavior
- character creation and equipment UX
- Track 7 roguelite UI integration, including relic HUD text, boss/streak indicators, archetype preview updates, and component persistence for relic/shrine/streak/archetype state
- Wave 1 roguelite systems, including synergy detection, daily seed determinism, ascension modifier lookup, narrative epitaph determinism, expanded content validation, and meta-progression ascension persistence
- Wave 2 roguelite integration, including requested content IDs/reachability, critical/floor-clear/reputation/synergy event presentation through existing UI surfaces, daily title entry, ascension shop-price wiring, and save serialization for boss phase/faction/synergy payloads
- architecture smoke coverage
- save version 17 migration, v16 scheduler-zero preservation/Warlord baseline migration, relic hook/applied-stat round trips, stricter component validation, and corrupt meta/daily recovery
- generation boss/safe precedence, deep-floor scaling, locked-room keys, themed traps, authored initial start rooms, seed-profile room reservation, no-repeat prefab pools, varied corridor silhouettes, start-room exclusion, ragged prefabs, and content-backed depth sweeps
- deterministic dungeon survey export, including PNG signature/byte stability, high-resolution dimensions, exact workshop seed/depth entry, and active-run preservation
- AI group aggro/profile corrections, patrol reachability, and occupied-corridor pathfinding
- canonical sourced/unsourced status-death game over, player retention, skipped-turn action suppression/output propagation, typed DoT combat events, and relic damage hooks
- exact Bone Amulet/Soul Collector kill milestones, one-time Glass Cannon application, delta-based capped Warlord bonuses, and floor/rest hook log forwarding
- attack/death animation, critical/miss/heal/pickup feedback, and SVG/import metadata conventions
- audit regressions for wall-occupying actor restoration, Core content binding and trap RNG continuation, authored status application, lethal-healing ordering, mouse inventory input gates, and restart/floor-travel turn counters
- save version 18 floor-clear reward round trips, fresh/same-manager reloads, cached-floor revisits, malformed reward metadata, and legacy versions 1-17 migration with v17 scheduler/relic preservation
- authored enemy texture projection and rendering for the full roster, identity-over-name priority, missing import/source fallbacks, content rebinding, and stable visual-node reuse
- public GameManager boss population at actual depths, ordinary-slot boss exclusion, fixed-template overrides, unknown/empty pools, and deterministic selection across seeds

Asset convention tests expect source PNG/SVG assets and their committed `.import` sidecars to remain paired. They are static checks; asset deletion or import changes still require the real Godot editor import/startup smoke.

## Writing New Tests

1. Add a class that implements `ITestSuite`.
2. Register each test through the provided registry.
3. Keep tests deterministic and self-contained.
4. Prefer focused tests around one subsystem or regression.

Stub-backed tests may rely on the harness-level reset for shared Godot stub state, but each test should still set up its own required scene/input/resource state explicitly.

The Godot compatibility `Image` stub includes deterministic RGBA raster operations and PNG encoding so exporter tests validate a real PNG structure rather than only a requested path. The normal real-Godot build also compiles `DungeonMapExporter` against Godot 4.5.2 APIs; visual inspection in the playable shell remains useful for palette and annotation quality.

When changing simulation, persistence, generation, or content loading, add or update tests in the same change.

## Testing Guidance By Change Type

The NPC/flow pass adds content, simulation, persistence, and UI suites for conditional branching, filtered numbering, service authorization/payment/healing, derived expedition reports, four depth-gated NPCs, and deterministic population. Diagonal-input tests prove that a prefix plus perpendicular directions produces one existing action without a preparatory movement turn. UI tests cover configured text bounds, selected-row existence, status texture minimums, modal HUD suppression, panel-only tint, and compact log/status separation. Terrain tests preserve geometry/layering and RNG while checking depth palettes and locked-door presentation.

The run-pacing pass adds generation/reward coverage for shrine event identity, cursed chest tables, safe-floor recovery caches, boss guarantees, deterministic landmark metadata, populated shrine entities, all shrine reward paths, guarded relic claims, save/load offer stability, sanctuary-cache population, late-pool role diversity, and depth-band recovery access. Content tests reject accidental regression to a four-template late pool or a missing Orin emergency service.

The identity/art pass verifies archetype package/preview alignment, legacy save preservation, race normalization and heritage-slot reconciliation, cooldown persistence, owned-only ability palette routing, targeted cancellation, 0x72 portrait mappings, authored item-path projection, source-image fallback, inventory slot icons/badges, visible-only ground piles, pile cleanup, and player body palette preservation.

Run `Scenes/Tests/VisualCapture.tscn` in real Godot to catch font minimum sizes, inherited tint, and draw-order issues the stubs cannot simulate. Use `--assert-layout` for actual label containment, and visually review sibling panels as well. The fixture, commands, source-pack research, and known shutdown-leak limitation are documented in [VISUAL_VALIDATION.md](VISUAL_VALIDATION.md).

- Simulation/action changes: add simulation or integration tests.
- Save-format or migration changes: add persistence serialization and migration coverage.
- Content loader changes: add content tests and at least one failure-path case when practical.
- Rendering or UI changes: add rendering or UI tests where the behavior is covered by the stubbed environment.
- Authored visual path changes: update the content art-path audit and run the full harness; run Godot headless editor import/startup when assets are added or renamed.
- AI behavior changes: add deterministic brain or scorer coverage instead of relying on manual playtesting.

## Determinism Expectations

Tests should avoid hidden time, file-system, or random-number dependencies unless the behavior under test is explicitly about those concerns. If randomness is involved, drive it from a known seed.
