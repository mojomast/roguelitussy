# Content

The project uses flat JSON documents under `Content/` as the source of truth for gameplay content.

## Content Files

- `items.json`
- `enemies.json`
- `abilities.json`
- `status_effects.json`
- `loot_tables.json`
- `room_prefabs.json`

`ContentLoader` expects all of these files to exist and will fail loading if any are missing.

## File Roles

- `items.json` defines item metadata, stats, use effects, rarity, requirements, stacking, and visuals.
- `enemies.json` defines enemy stats, AI type, spawn ranges, abilities, loot, tags, and visuals.
- `abilities.json` defines targeting, costs, effects, and audiovisual identifiers for abilities.
- `status_effects.json` defines status effect metadata used by simulation and content references.
- `loot_tables.json` defines weighted loot outcomes.
- `room_prefabs.json` defines prefab rooms and the tile legend used to interpret them.

The current repository content set includes expanded mid- and late-game coverage:

- 22 items
- 13 enemies
- 8 abilities
- 9 status effects
- 15 loot tables
- 10 room prefabs

## Versioning

The current content documents use version `1`.

Keep document versions aligned with the expectations in `ContentLoader`. If the loader changes compatibility rules, update the documentation and validation tests at the same time.

## ID Rules

Content IDs should be stable lowercase snake_case strings.

This matters because:

- content is referenced across multiple JSON files
- tests assume stable IDs
- content lookups are exact and deterministic

## Loader Behavior

`ContentLoader` performs these jobs:

1. Reads all required JSON documents from the selected content directory.
2. Deserializes them with strict casing and no trailing commas.
3. Builds deterministic lookups for items, enemies, abilities, status effects, room prefabs, and loot tables.
4. Produces simulation-facing item and enemy templates.
5. Produces simulation-facing ability templates.
6. Collects validation errors for malformed or inconsistent content.

`LoadFromRepository()` can locate the repository content directory automatically by walking upward from a start directory.

## Adding New Content

### Items

When adding an item, provide at least:

- a stable `id`
- display fields such as `name` and `description`
- a `type`
- `stats` and `effects` that match the intended behavior
- stack or slot information where appropriate

Weapon items can now drive live combat behavior through fields such as damage range, crit chance, accuracy, speed modifier, and `on_hit` status effects. Equippable items may also include runtime-enforced `requirements`.

### Enemies

When adding an enemy, define:

- core stats
- `ai_type`
- depth range
- spawn weight
- faction
- ability and loot references if used

Enemy `ai_type` values currently expected by the loader are `melee_rush`, `ranged_kite`, `ambush`, `patrol`, and `support`.

Enemy speed values should stay on the engine's current 100-based scale.

### Abilities And Status Effects

When linking abilities or status effects from other content, make sure the referenced IDs already exist and match exactly.

Supported ability targeting types currently in runtime use are `self`, `single`, `tile`, and `aoe_circle`. Supported effect types currently executed by the runtime are `damage`, `apply_status`, `teleport`, and `heal_self`.

### Room Prefabs

Room prefab authoring depends on the shared tile legend in `room_prefabs.json`. Keep the legend and room definitions in sync when adding new symbols.

## Validation Workflow

After changing content:

1. Run the full test suite.
2. Pay attention to content, integration, and generation failures.
3. If you changed loader behavior or schema expectations, update documentation and tests in the same change.

Content validation tests assert the expected item, enemy, loot table, room prefab, ability, and status effect counts, so remember to update those expectations when intentionally expanding the catalog.

## Godot Tooling And Content

The editor tools resolve the content directory through `Scripts/Tools/ToolPaths.cs`, which delegates to `ContentLoader.FindContentDirectory(...)`. Keep the repository's flat `Content/*.json` layout intact so both runtime and tools can discover content reliably.