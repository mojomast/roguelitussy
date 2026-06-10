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

### 8. Workflow and CI are not ready for confident resumption

Confirmed workflow issues:

- No `.github/workflows` CI files were found.
- `godotussy.sln` includes only `godotussy.csproj`, not `Tests/godotussy.Tests.csproj`.
- The canonical editorless build command is ambiguous because stubs are gated behind `UseGodotStubs=true`.
- Docs previously carried conflicting exact test counts instead of deferring to the harness output.
- Docs previously said Godot 4.4, while packages pin 4.4.1.

Relevant files:

- `godotussy.sln:6`
- `godotussy.csproj:1-47`
- `Tests/godotussy.Tests.csproj:1-21`
- `README.md`
- `docs/SETUP.md`
- `docs/TESTING.md`

Resume action:

Add CI and clarify docs before major feature work. At minimum, CI should build the stub profile and run the custom test harness.

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
- Add a minimal CI workflow for stub build and tests.
- Add `Tests/godotussy.Tests.csproj` to the solution or document that solution build is not full validation.
- Add or update `global.json` if a specific .NET SDK feature band is expected.

Acceptance criteria:

- CI runs on every push/PR.
- README setup commands match what CI runs.
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
