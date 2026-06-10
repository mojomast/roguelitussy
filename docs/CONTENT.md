# Content

The project uses flat JSON documents under `Content/` as the source of truth for gameplay content.

## Content Files

- `items.json`
- `enemies.json`
- `abilities.json`
- `status_effects.json`
- `loot_tables.json`
- `room_prefabs.json`
- `perks.json`
- `dialogs.json`
- `npcs.json`

`ContentLoader` expects all of these files to exist and will fail loading if any are missing.

## File Roles

- `items.json` defines item metadata, stats, use effects, rarity, requirements, stacking, and visuals.
- `enemies.json` defines enemy stats, AI type, spawn ranges, abilities, loot, tags, and visuals. Current enemy `sprite_path` entries point at committed 0x72 sprites under `Assets/Sprites/0x72/`.
- `abilities.json` defines targeting, costs, effects, and audiovisual identifiers for abilities.
- `status_effects.json` defines status effect metadata used by simulation and content references.
- `loot_tables.json` defines weighted loot outcomes, including guaranteed chest-specific tables and tuned recovery drops such as more available health potions.
- `room_prefabs.json` defines prefab rooms and the tile legend used to interpret them.
- `perks.json` defines progression perk metadata and effects.
- `dialogs.json` defines NPC dialogue graphs.
- `npcs.json` defines NPC metadata, roles, service hooks, and dialogue references.

The current repository content set includes expanded mid- and late-game coverage. Use the loader/tests or the runtime validation tools for current generated counts instead of copying exact numbers into docs.

## Versioning

The current content documents use version `1`.

Keep document versions aligned with the expectations in `ContentLoader`. If the loader changes compatibility rules, update the documentation and validation tests at the same time.

`ContentLoader` also computes a deterministic SHA-256 hash from the required JSON files in fixed order. New saves store the current content version and hash as optional metadata. Loading remains warning-only: missing metadata or a version/hash mismatch should alert the player/developer through the runtime log, but it should not reject the save by itself.

## ID Rules

Content IDs should be stable lowercase snake_case strings.

This matters because:

- content is referenced across multiple JSON files
- tests assume stable IDs
- content lookups are exact and deterministic
- saves can contain content-backed references such as item template IDs, ability IDs, NPC/dialog IDs, and persisted runtime components

## Loader Behavior

`ContentLoader` performs these jobs:

1. Reads all required JSON documents from the selected content directory.
2. Deserializes them with strict casing and no trailing commas.
3. Builds deterministic lookups for items, enemies, abilities, status effects, room prefabs, loot tables, perks, dialogs, and NPCs.
4. Produces simulation-facing item and enemy templates.
5. Produces simulation-facing ability templates and validation-ready progression/NPC content lookups.
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

Item `stats` must map to runtime-supported equipment or item-use behavior. Supported authored keys are `damage_min`, `damage_max`, `accuracy`, `speed_modifier`, `crit_chance`, `defense`, `evasion`, `fov_bonus`, `attack`, `hp`, and `max_hp`. Passive item effects are not currently supported by runtime simulation and should not be authored until their action is implemented.

Consumable authoring rules:

- `on_use` healing effects should use the supported heal shape/amount expected by `ContentLoader` so runtime item templates receive a concrete heal value.
- `on_use` status effects should reference existing `status_effects.json` IDs and use the `apply_status` behavior recognized by item use.
- `on_use` ability casts should reference an existing `abilities.json` ID through `cast_ability`; aimed casts still require valid target information from UI or AI before `CastAbilityAction` executes.
- Keep item effects content-backed and covered by tests when adding a new effect family.

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

Supported ability targeting types currently in runtime use are `self`, `single`, `tile`, and `aoe_circle`. Supported effect types currently executed by the runtime are `damage`, `apply_status`, `teleport`, and `heal_self`. Targeting is validated by `CastAbilityAction`; keep content targeting definitions precise so direct casts and item-delegated casts behave the same way. For harmful area damage or harmful statuses, `hits_allies: false` defaults unfiltered effects to enemies only, while explicit effect filters and `hits_allies: true` preserve broader targeting.

Status-effect runtime behavior currently includes authored corroded stacking up to three stacks and burning/frozen mutual removal on apply. Status effects applied by melee on-hit effects or abilities retain source attribution for delayed poison/burning kill credit and save/load round-trips.

### Room Prefabs

Room prefab authoring depends on the shared tile legend in `room_prefabs.json`. Keep the legend and room definitions in sync when adding new symbols.

## Validation Workflow

After changing content:

1. Run the full test suite.
2. Pay attention to content, integration, and generation failures.
3. If you changed loader behavior or schema expectations, update documentation and tests in the same change.

Content validation tests assert the expected item, enemy, loot table, room prefab, ability, and status effect counts, so remember to update those expectations when intentionally expanding the catalog.

Authored item, enemy, and status-effect visual paths are also audited by tests to ensure each `res://` path resolves to a committed source file. Runtime loading remains soft: missing art should not crash content loading, but repository content should keep these paths valid.

Current item and status icons are simple limited-palette SVG source files under `Assets/Sprites/items/` and `Assets/Sprites/ui/`. Keep future SVGs simple, avoid embedded text, commit the matching `.svg.import` sidecars, and run the Godot headless editor import after changing assets so ignored `.godot/imported` cache files can be regenerated locally.

## Godot Tooling And Content

The editor tools resolve the content directory through `Scripts/Tools/ToolPaths.cs`, which delegates to `ContentLoader.FindContentDirectory(...)`. Keep the repository's flat `Content/*.json` layout intact so both runtime and tools can discover content reliably.
