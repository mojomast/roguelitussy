# Agent Instructions

This repository is a Godot 4.4.1 C# roguelike with a pure simulation core, Godot-facing presentation scripts, JSON-authored content, and a custom .NET test harness.

Read these files before making non-trivial changes:

- `README.md`
- `DEVELOPMENT_RESUME_REPORT.md`
- `docs/ARCHITECTURE.md`
- `docs/SYSTEMS.md`
- `docs/PROGRESSION.md`
- `docs/TESTING.md`
- `docs/CONTRIBUTING.md`
- `docs/CONTENT.md`
- `improvments.md`
- `docs/IMPROVEMENT_SUGGESTIONS.md`

## Project Boundaries

- Keep deterministic gameplay state and mutations in `Core/`.
- Keep Godot-specific code in `Scripts/`, `Scenes/`, and `Addons/`.
- Do not introduce `using Godot` into `Core/`.
- Use `IAction` implementations for gameplay mutations.
- Use `Scripts/Autoloads/EventBus.cs` for cross-system presentation notifications instead of direct simulation-to-UI coupling.
- Treat `GameManager` as a Godot facade; avoid adding more responsibilities to it unless there is no smaller safe option.

## Current Development Priorities

Work should focus on stabilizing existing systems before adding new content.

1. Fix save/load rehydration for enemies, chests, abilities, cooldowns, XP values, and deterministic runtime state.
2. Fix deterministic RNG across save/load and replay-style tests.
3. Fix item/ability runtime drift, especially `apply_status:` and `cast_ability:` consumables.
4. Centralize death/progression handling so melee, abilities, status effects, traps, and future kill sources behave consistently.
5. Add tests for every fixed simulation, persistence, content, and progression behavior.
6. Add or improve CI and developer workflow documentation.
7. Validate or correct missing `res://` asset paths referenced by content JSON.
8. Update stale docs and `improvments.md` as work is completed.

Do not add more enemies, items, perks, rooms, or progression content until the current authored content reliably works at runtime.

## Subagent Orchestration

Use subagents for broad scouting and parallel review, but avoid parallel write collisions.

- Run research/scouting subagents in parallel first.
- Keep scouting agents read-only unless their task is explicitly isolated.
- Assign exact file ownership before any write-capable subagent starts editing.
- Never let two agents edit the same file family at the same time.
- Have one coordinator/integrator perform final conflict resolution and verification.
- Subagents should report proposed file changes before editing shared or high-risk areas.
- Prefer small sequential implementation batches after parallel research.
- Run relevant tests after each batch before starting the next batch.
- If required tools are unavailable, document the verification gap clearly.

Suggested ownership boundaries:

- Persistence owner: `Core/Persistence/*`, save/load tests, save migration docs.
- Determinism owner: RNG abstractions/state, `CombatResolver`, deterministic replay tests.
- Item/ability owner: `UseItemAction`, `CastAbilityAction`, `AbilityResolver`, content-backed item/ability tests.
- Death/progression owner: shared death handler, `AttackAction`, progression service/tests.
- UI/Godot owner: `Scripts/UI/*`, `Scripts/World/*`, `Scripts/Autoloads/GameManager.cs`.
- Workflow/docs owner: `.github/workflows/*`, `README.md`, `docs/*.md`, solution/project metadata, resource validation tests.

Coordinate explicitly before crossing these boundaries.

## Recommended Research Scouts

For a fresh development pass, launch read-only subagents for:

- Godot/C# best practices as of June 2026, including autoloads, scene organization, signals, resource loading, headless CI, and deterministic simulation boundaries.
- Persistence and deterministic RNG gaps.
- Item, ability, status effect, and content-schema/runtime drift.
- Death, XP, level-up, and progression architecture.
- Build, test, CI, docs, and asset-resource validation.

Use the scout reports to create a concrete implementation plan before editing.

## Implementation Sequence

### Phase 0: Baseline

- Check `git status --short`.
- Run available verification commands.
- If .NET or Godot is missing, continue only where static changes are safe and document that tests were not run.

Recommended commands:

```bash
dotnet --info
dotnet build godotussy.csproj -p:UseGodotStubs=true
dotnet build Tests/godotussy.Tests.csproj
dotnet run --project Tests/godotussy.Tests.csproj
dotnet run --project Tests/godotussy.Tests.csproj -p:RenderingValidation=true
```

### Phase 1: Persistence And Determinism

- Add tests first where feasible.
- Persist or rehydrate enemy/chest runtime components.
- Preserve deterministic RNG state or introduce replay-safe RNG.
- Add save/load replay regression tests.

### Phase 2: Item And Ability Correctness

- Fix `apply_status:` parsing.
- Fix `cast_ability:` resolution from item content.
- Add targeting or safe validation for targeted scrolls and abilities.
- Add content-backed tests for consumables and scrolls.

### Phase 3: Shared Death And Progression

- Extract common death handling.
- Ensure melee and ability kills award XP consistently.
- Cover kill counts, drops, logs, removal, combat events, and level-up behavior.

### Phase 4: Presentation Boundaries

- Make visibility/FOV ownership explicit.
- Keep `WorldView` from mutating authoritative simulation state if possible.
- Add status/combat event payloads that identify affected targets.
- Extract `GameManager` responsibilities incrementally when touching related code.

### Phase 5: Workflow, Assets, And Docs

- Add CI for stub build and custom test harness.
- Include tests in the solution or document validation commands clearly.
- Validate `res://` asset paths used by JSON content.
- Update docs and stale improvement notes.

## Testing Expectations

- Add or update tests whenever simulation, persistence, generation, content loading, progression, or UI behavior changes.
- Prefer deterministic regression tests over broad snapshot-style assertions.
- Add content-driven tests that fail when JSON declares an effect/stat/path unsupported by runtime.
- If a command cannot be run because tooling is missing, mention that in the final report.

## Documentation Requirements

Every completed feature or fix must update relevant documentation.

- Save/load or migration changes: update `docs/SYSTEMS.md`.
- Command/tooling changes: update `README.md`, `docs/SETUP.md`, and `docs/TESTING.md`.
- Content schema or authoring changes: update `docs/CONTENT.md`.
- Progression changes: update `docs/PROGRESSION.md`.
- Resolved roadmap items: update `DEVELOPMENT_RESUME_REPORT.md` or add a short status section.
- Stale information in `improvments.md` should be corrected, marked done/partial/open, or archived.

## Code Comment Guidance

- Add concise comments only where code is non-obvious.
- Good comment targets: save migrations, deterministic RNG decisions, tricky effect parsing, compatibility shims, and Godot/Core boundary choices.
- Do not add comments that merely restate the code.

## Safety Rules

- Keep changes minimal and focused.
- Do not rewrite broad systems unless required by tests or a confirmed design issue.
- Do not modify unrelated user changes.
- Do not use destructive git commands such as `git reset --hard` or `git checkout --` unless explicitly requested.
- Do not commit unless explicitly asked.
- Before finalizing, summarize changed files, tests run, verification gaps, and remaining risks.

## Repository Wiring Guide

A concise map of how the major pieces connect. Read this to orient yourself before editing.

### Layer overview

```
Content/ (JSON)  -->  Core/Content/ContentLoader.cs  -->  IContentDatabase  -->  Core/Simulation
                                                              |
Scripts/ (Godot)  -->  GameManager (autoload)  -->  WorldState / GameLoop / Actions
   |                           |
   v                           v
UIRoot / WorldView  <--  EventBus  <--  GameManager.ProcessPlayerAction
```

- **Core/**: pure C#, no `using Godot`. Authoritative state, rules, actions, generation, persistence, content loading.
- **Scripts/**: Godot autoloads, UI, world rendering, input, debug tools.
- **Content/**: JSON definitions for items, enemies, abilities, statuses, loot tables, rooms, perks, NPCs, dialogs.
- **Tests/**: custom .NET console harness (`Tests/Program.cs`) — not NUnit/xUnit.

### Core simulation

- **WorldState** (`Core/Contracts/WorldState.cs`) is the authoritative grid + entity index + RNG-state holder.
- **Entity** (`Core/Simulation/Entity.cs`) is a component bag: `InventoryComponent`, `StatusEffectsComponent`, `ProgressionComponent`, `CooldownComponent`, `AbilitiesComponent`, `XpValueComponent`, `ChestComponent`, etc.
- **IAction** (`Core/Contracts/IAction.cs`) is the supported way to mutate gameplay state. Implementations live in `Core/Simulation/Actions/`.
- **GameLoop** (`Core/Simulation/GameLoop.cs`) processes one round: runs every ready actor via `TurnScheduler`, then ticks cooldowns.
- **TurnScheduler** (`Core/Simulation/TurnScheduler.cs`) is energy-based (`EnergyThreshold = 1000`) and also ticks status effects inside `ConsumeEnergy`.

### Action flow (one player turn)

1. `UIRoot._UnhandledInput` (`Scripts/UI/UIRoot.cs`) routes keys.
2. `InputHandler.HandleKey` maps keys to semantic actions.
3. `UIActionFactory` builds an `IAction` (move, attack, use item, etc.).
4. `EventBus.EmitPlayerActionSubmitted(action)` is raised.
5. `GameManager.OnPlayerActionSubmitted` receives it and calls `ProcessPlayerAction(action)`.
6. `GameManager.ProcessPlayerAction` validates, executes the player action, then runs `GameLoop.ProcessRound` for enemy responses.
7. Actions mutate `WorldState` and return `ActionOutcome` with `CombatEvent`s and log messages.
8. `GameManager` emits `EventBus` events (`TurnStarted`, `DamageDealt`, `EntityDied`, `InventoryChanged`, etc.).
9. `WorldView`, `HUD`, `CombatLog`, and other UI subscribe and refresh.

### Combat and death

- **CombatResolver** (`Core/Simulation/CombatResolver.cs`) owns a deterministic RNG and computes hit/crit/damage/armor/on-hit effects.
- **DeathResolver** (`Core/Simulation/DeathResolver.cs`) is the shared kill handler: zeroes HP, increments kills, awards XP via `ProgressionService.AwardExperience`, and removes the entity.
- Deaths are triggered from `AttackAction`, `CastAbilityAction`, and `StatusEffectProcessor.Tick`.

### Items, abilities, and status effects

- Content JSON is projected into `ItemTemplate`, `AbilityTemplate`, etc.
- `UseItemAction` (`Core/Simulation/Actions/UseItemAction.cs`) handles `heal`, `apply_status`, `cast_ability`, `cure`, and stat modifiers.
- `CastAbilityAction` (`Core/Simulation/Actions/CastAbilityAction.cs`) resolves targets via `AbilityResolver` and applies damage/status/teleport/heal_self effects.
- `StatusEffectProcessor` (`Core/Simulation/StatusEffectProcessor.cs`) hardcodes most status behavior at the moment; `Content/status_effects.json` is largely descriptive.
- **Known gap**: targeted scrolls (`cast_ability:` with non-`self` targeting) cannot be used from the normal UI because `UIActionFactory.CreateUseItemAction` returns `null` for them.

### Content pipeline

- `ContentLoader.LoadFromDirectory`/`LoadFromRepository` reads the nine required JSON files, validates cross-references, builds templates, and computes a deterministic content hash.
- Runtime code accesses content through `IContentDatabase` / the Godot autoload `ContentDatabase`.
- Asset paths in content (`res://Assets/Sprites/...`) are validated by `Tests/ContentTests/ContentValidationTests`.

### Persistence

- `SaveManager` / `SaveSerializer` / `SaveMigrator` implement JSON save/load (current version 8).
- Saves include: map tiles, explored/visible flags, entities + components, ground items, open doors, RNG states, multi-floor cache.
- `GameManager.SaveToSlot` / `LoadFromSlot` are the Godot-facing entry points.
- RNG states restored: `CombatRandomState` and `ItemRandomState`.
- **Known gaps**: enemy template identity is not saved (brain is recreated generically), and `TurnScheduler` actor order is not persisted.

### Godot facade and presentation

- **GameManager** (`Scripts/Autoloads/GameManager.cs`) autoload owns services, content, save/load, floor travel, and turn processing. Treat it as a facade; avoid adding more responsibilities.
- **EventBus** (`Scripts/Autoloads/EventBus.cs`) is a C# event bus for simulation-to-UI notifications.
- **UIRoot** constructs and binds all UI controllers.
- **WorldView** mirrors `WorldState` each turn; it does not compute FOV, only copies visibility flags.
- **EntityRenderer** creates sprites per entity using `WorldArtCatalog` / `PlayerVisualCatalog`.
- Debug tools (`DebugCommandProcessor`, `DevToolsWorkbench`) mutate `WorldState` directly for authoring convenience; normal gameplay must use `IAction`s.

### Determinism

- `DeterministicRandom` (`Core/Simulation/DeterministicRandom.cs`) is a serializable LCG.
- `CombatResolver` owns the combat RNG; `WorldState` exposes `CombatRandomState`.
- `WorldState.AllocateItemInstanceId` uses a separate `ItemRandomState`.
- Generation and chest loot use `System.Random` seeded from world values; they are deterministic but separate streams.

### Common pitfalls for agents

- Do not add `using Godot` in `Core/`.
- Do not mutate `WorldState` directly from `Scripts/` except in debug/tooling code.
- New gameplay mutations must be `IAction`s routed through `GameManager.ProcessPlayerAction`.
- Any new status/item/ability effect needs both runtime implementation and content-driven tests.
- Save format changes require updating `SaveSerializer.CurrentVersion`, migration, and tests.
- Update docs per the "Documentation Requirements" section above.
