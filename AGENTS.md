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
