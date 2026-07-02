# Content

The project uses flat JSON documents under `Content/` as the source of truth for gameplay content. Editor and source-checkout runs load these files from the filesystem; exported Godot builds read the same `res://Content/*.json` resources from the packaged PCK before projecting them into `ContentLoader` templates.

## Content Files

- `items.json`
- `enemies.json`
- `abilities.json`
- `status_effects.json`
- `loot_tables.json`
- `room_prefabs.json`
- `traps.json`
- `perks.json`
- `relics.json`
- `floor_events.json`
- `synergies.json`
- `ascension_modifiers.json`
- `daily_modifiers.json`
- `narrative_templates.json`
- `factions.json`
- `meta_upgrades.json`
- `dialogs.json`
- `npcs.json`

`ContentLoader` expects the runtime gameplay files to exist and will fail loading if any are missing. `meta_upgrades.json` is loaded by the Godot-side `MetaProgressionManager` because it belongs to between-run state rather than deterministic floor simulation.

## File Roles

- `items.json` defines item metadata, stats, use effects, rarity, requirements, stacking, and visuals.
- `enemies.json` defines enemy stats, AI type, spawn ranges, abilities, loot, tags, and visuals. Current enemy `sprite_path` entries point at committed 0x72 sprites under `Assets/Sprites/0x72/`.
- `abilities.json` defines targeting, costs, effects, and audiovisual identifiers for abilities.
- `status_effects.json` defines status effect metadata used by simulation and content references.
- `loot_tables.json` defines weighted loot outcomes, including guaranteed multi-roll chest-specific tables and tuned recovery drops such as more available health potions.
- `room_prefabs.json` defines prefab rooms and the tile legend used to interpret them.
- `traps.json` defines trap templates referenced by room prefabs and spawned as trap entities at runtime.
- `perks.json` defines progression perk metadata and effects.
- `relics.json` defines passive relic templates and hook metadata used by the relic processor.
- `floor_events.json` defines safe-floor, boss-floor, shrine, curse-room, and vault event metadata.
- `synergies.json` defines build-combination hints and passive effects from relics, perks, archetypes, and item tags.
- `ascension_modifiers.json` defines the 0-10 challenge ladder modifiers available after the first full clear.
- `daily_modifiers.json` defines the weekday-specific daily challenge modifiers layered over the deterministic daily seed.
- `narrative_templates.json` defines deterministic epitaph sentence templates for run history and death screens.
- `factions.json` defines social reputation thresholds and faction display metadata.
- `meta_upgrades.json` defines Echo-purchased long-term upgrade nodes consumed by the meta shop and character-creation archetype gates.
- `dialogs.json` defines NPC dialogue graphs, including optional rotating `start_nodes` for repeated greetings.
- `npcs.json` defines NPC metadata, roles, service hooks, merchant stock, and dialogue references.

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
3. Builds deterministic lookups for items, enemies, abilities, status effects, room prefabs, traps, loot tables, perks, relics, floor events, synergies, ascension modifiers, daily modifiers, narrative templates, factions, dialogs, and NPCs.
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

Item `tags` are projected into runtime templates and are used by synergy detection. Keep tags lowercase snake_case concepts such as `heavy`, `shield`, `arcane`, `light`, or `ranged`.

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
- `ai_params` (optional overrides for the base AI profile)
- depth range
- spawn weight
- faction
- ability and loot references if used
- optional `gold_min`/`gold_max` for gold awarded to the killer on death

Enemy `ai_type` values currently expected by the loader are `melee_rush`, `ranged_kite`, `ambush`, `patrol`, and `support`.

Recognized `ai_params` keys are:

- `flee_hp_pct` (number 0-100): HP percentage at which the enemy will start fleeing.
- `aggro_range` (positive integer): detection radius that overrides the entity's `fov_range` for acquiring hostile targets.
- `wander_when_idle` (boolean): whether the enemy patrols when it has no target.
- `preferred_range` (positive integer): ideal distance the enemy tries to maintain from its target.
- `min_range` (positive integer): minimum distance the enemy will allow; it backs away if closer.
- `patrol_radius` (positive integer): maximum distance from the spawn point when selecting patrol targets.
- `support_range` (positive integer): radius within which support abilities look for allies to buff.
- `phase_through_walls` (boolean): grants permanent phasing, allowing movement and pathfinding through walls.

Enemy speed values should stay on the engine's current 100-based scale.

Boss enemies may declare `boss_phase_data`. Each phase entry includes `phase`, `threshold` as an HP fraction, optional `ability_id`, `stat_boost`, `status_effect`, and `message`. Referenced abilities must exist in `abilities.json`; triggered phase state persists in save version 15.

### Abilities And Status Effects

When linking abilities or status effects from other content, make sure the referenced IDs already exist and match exactly.

Supported ability targeting types currently in runtime use are `self`, `single`, `tile`, and `aoe_circle`. Supported effect types currently executed by the runtime are `damage`, `apply_status`, `teleport`, and `heal_self`. Targeting is validated by `CastAbilityAction`; keep content targeting definitions precise so direct casts and item-delegated casts behave the same way. For harmful area damage or harmful statuses, `hits_allies: false` defaults unfiltered effects to enemies only, while explicit effect filters and `hits_allies: true` preserve broader targeting.

Status-effect runtime behavior currently includes authored corroded stacking up to three stacks, burning/frozen mutual removal on apply, and `blinded` as an accuracy-reducing combat status. Status effects applied by melee on-hit effects or abilities retain source attribution for delayed poison/burning kill credit and save/load round-trips.

`chain_lightning` is an authored lightning ability with a deterministic runtime special case: it starts from the authored AoE candidate set and resolves up to three nearest hostile targets to the selected tile.

### Loot Tables, Chests, And Merchants

Chest-specific loot tables should not contain no-drop entries. `chest_loot` and `deep_chest_loot` currently roll multiple rewards per chest, and runtime chests persist rolled contents so players can take selected items and leave the rest behind. Chest contents are save-facing item instances once rolled, so changing chest behavior requires persistence coverage. The Wave 2 expansion adds six new enemies, five relics, `blinded`, chain-lightning scroll support, and new reachable item drops while preserving the early health-potion sustain share.

Merchant stock is authored on NPC definitions in `npcs.json`. Stock entries must reference existing item IDs and use positive price/quantity values. Prefer a spread of recovery items, scrolls, armor, and weapons so vendors provide build correction rather than only one starter weapon and potion.

### NPC Dialogs

Dialog definitions require `start_node` for compatibility. They may also provide `start_nodes`, a list of valid node IDs that `DialogUI` rotates through on repeated openings for greeting variety. Every start node and option `next` target must exist in the same dialog graph. Keep shop-opening options authored with `action: "shop"` and close options with `action: "close"`.

### Traps

Traps are authored in `Content/traps.json`. A trap definition contains:

- `id` — unique trap identifier.
- `name` / `description` — display text.
- `ability_id` (optional) — ability from `abilities.json` the trap can delegate to.
- `damage_min` / `damage_max` / `damage_type` — direct damage range and type.
- `status_effect` / `status_duration` / `status_magnitude` (optional) — status applied to the victim.
- `sprite_path` — `res://` path to the trap icon/sprite.
- `avoid_flags` — list of actor status flags that prevent triggering (e.g., `phased`, `flying`).
- `trigger_chance` — percent chance to trigger on entry (default 100).

Room prefabs can place traps by using the `^` tile in the layout and/or a `type: "trap"` spawn point with `trap_id` referencing a trap definition. `ContentLoader` validates every `trap_id` and every trap `ability_id`.

### Room Prefabs

Room prefab authoring depends on the shared tile legend in `room_prefabs.json`. Keep the legend and room definitions in sync when adding new symbols. The `+` symbol represents a door; doors may be converted into locked doors at generation time for rooms with `lock_doors_on_enter: true`.

Each room should include `tags` that describe its role and, for procedural floors, its theme membership. The generator uses depth-to-theme mapping:

- Depths 1–3: `prison`
- Depths 4–6: `crypt`
- Depth 7+: `magma`

When a floor has at least four theme-matching prefabs that fit the BSP leaves, the generator prefers those prefabs; otherwise it falls back to all valid prefabs. Tag rooms with the appropriate theme(s) (`prison`, `crypt`, `magma`) plus functional tags such as `combat`, `loot`, `hazard`, or `boss` so the theme filter can select them.

When a room has `lock_doors_on_enter: true` (typically arenas, vaults, or boss rooms), the generator converts its connecting door tiles into locked doors and places a `dungeon_key` item in a reachable non-locked room. The player must pick up the key and use it via `OpenDoorAction` to unlock the door permanently.

## Validation Workflow

After changing content:

1. Run the full test suite.
2. Pay attention to content, integration, and generation failures.
3. If you changed loader behavior or schema expectations, update documentation and tests in the same change.

Content validation tests assert the expected item, enemy, loot table, room prefab, ability, status effect, and trap counts, so remember to update those expectations when intentionally expanding the catalog.

Authored item, enemy, trap, and status-effect visual paths are also audited by tests to ensure each `res://` path resolves to a committed source file. Runtime loading remains soft: missing art should not crash content loading, but repository content should keep these paths valid.

Current item, status, and trap icons are simple limited-palette SVG source files under `Assets/Sprites/items/`, `Assets/Sprites/ui/`, and `Assets/Sprites/objects/`. Keep future SVGs simple, avoid embedded text, commit the matching `.svg.import` sidecars, and run the Godot headless editor import after changing assets so ignored `.godot/imported` cache files can be regenerated locally.

## Godot Tooling And Content

The editor tools resolve the content directory through `Scripts/Tools/ToolPaths.cs`, which delegates to `ContentLoader.FindContentDirectory(...)`. Keep the repository's flat `Content/*.json` layout intact so both runtime and tools can discover content reliably.
