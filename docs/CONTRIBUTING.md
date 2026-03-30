# Contributing

This repository is organized around a strict split between deterministic simulation code and Godot-facing presentation code. Keep changes aligned with that split.

## Placement Rules

- Put simulation rules and world mutation in `Core/`.
- Put Godot nodes, rendering, menus, and input routing in `Scripts/` and `Scenes/`.
- Put editor-only extensions in `Addons/` and `Scripts/Tools/`.
- Put data-driven definitions in `Content/`.
- Put regression coverage in `Tests/`.

## Coding Expectations

- Preserve the existing nullable-enabled C# style.
- Prefer small, explicit changes over broad rewrites.
- Avoid introducing Godot dependencies into `Core/`.
- Keep public contracts stable unless the change genuinely requires contract evolution.
- Treat save compatibility as a real project constraint.

## Workflow Expectations

Before opening or shipping a change:

1. Build the solution.
2. Run the relevant tests, and run the full suite for behavior-changing work.
3. Update documentation when behavior, workflow, or structure changes.
4. Keep content IDs and file names stable unless the change explicitly handles migration or reference updates.

## Common Change Patterns

### New Gameplay Action

- add or extend an `IAction` implementation
- keep validation and execution in the simulation layer
- emit follow-up presentation changes through `EventBus`
- add simulation tests

### New UI Feature

- add the Godot-side script under `Scripts/UI/`
- keep it reactive to `GameManager` and `EventBus`
- avoid storing authoritative gameplay state in the UI layer

### Persistence Change

- update serialization, validation, and migration together
- preserve compatibility with existing save versions where required
- add migration and round-trip tests

### Content Schema Change

- update `ContentLoader`
- update content docs
- update tests and sample data as needed

## Review Checklist

Before committing, verify:

- the code is in the right layer
- tests cover the changed behavior
- docs still match the implementation
- no temporary root-level `.cs` files were left behind
- content changes preserve stable references

## Documentation Rule

If you change how the project is built, tested, booted, loaded, or extended, update the relevant file in `docs/` and the root `README.md` in the same change.