# Godotussy Improvement Spec

## Purpose

This document is the current-state improvement spec for turning the existing project into a more complete, playable roguelike. It is intentionally written for parallel subagents. It replaces vague backlog work with concrete, codebase-specific workstreams, ordering, file targets, and acceptance criteria.

This spec is based on:

- direct audit of the live repository
- the current code and JSON content, not just the older one-shot spec
- a read-only repo audit subagent pass
- external roguelike and Godot best-practice references

## Research Notes

### Roguelike best-practice takeaways

The Berlin Interpretation is not a rulebook, but it is still useful as a design filter. The highest-value factors that matter to this repo are:

- random environment generation for replayability
- turn-based tactical decision making
- grid-based clarity
- exploration and discovery
- resource management
- enough system complexity that players can solve problems in multiple ways

Implication for this project: the next missing systems should deepen tactical choice and progression, not just add more assets.

### Godot best-practice takeaways

From Godot scene-organization and autoload guidance:

- keep autoloads limited to global coordination and cross-scene state
- keep scenes loosely coupled and parent-initialized rather than hard-wired
- prefer data injection and signals over deep node-path assumptions
- keep gameplay state out of presentation nodes when possible

Implication for this project: new progression, appearance, and combat systems should live in `Core/` first, then be surfaced through `GameManager`, `EventBus`, and `Scripts/UI/`.

## Verified Current Gaps

These gaps were verified against the live codebase.

### 1. Character creation has no race, gender, or appearance identity

Current state:

- [Scripts/UI/MainMenu.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/MainMenu.cs) supports `Name`, `Archetype`, `Origin`, `Trait`, and training points.
- [Scripts/Autoloads/GameManager.cs](c:/Users/kyle/projects/godotussy/Scripts/Autoloads/GameManager.cs) stores those options in `CharacterCreationOptions`.
- [Scripts/World/EntityRenderer.cs](c:/Users/kyle/projects/godotussy/Scripts/World/EntityRenderer.cs) renders the player from a single generic path and has no appearance-routing layer.

Missing:

- race/species selection
- gender/presentation selection
- class-specific sprite or tile variants
- appearance metadata persisted into runtime state and saves
- character sheet display of identity choices

### 2. No experience or leveling system

Current state:

- enemy JSON includes `xp_value` in [Content/enemies.json](c:/Users/kyle/projects/godotussy/Content/enemies.json)
- [Core/Contracts/Types/Stats.cs](c:/Users/kyle/projects/godotussy/Core/Contracts/Types/Stats.cs) has no XP or level fields
- [Core/Simulation/Actions/AttackAction.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/Actions/AttackAction.cs) kills enemies but grants no progression
- [Core/Persistence/SaveSerializer.cs](c:/Users/kyle/projects/godotussy/Core/Persistence/SaveSerializer.cs) has no progression payload

Missing:

- XP gain on kill
- level thresholds and carryover XP
- stat growth or player-chosen level-up rewards
- UI surfaces for XP and level
- save/load migration for progression data

### 3. Ability content exists but runtime ability usage is incomplete

Current state:

- [Content/abilities.json](c:/Users/kyle/projects/godotussy/Content/abilities.json) defines targeted, AoE, teleport, and status abilities
- enemies reference abilities in [Content/enemies.json](c:/Users/kyle/projects/godotussy/Content/enemies.json)
- items reference `cast_ability` actions in [Content/items.json](c:/Users/kyle/projects/godotussy/Content/items.json)
- [Core/AI/AIBrain.cs](c:/Users/kyle/projects/godotussy/Core/AI/AIBrain.cs) only generates Move, Attack, and Wait candidates

Missing:

- dedicated cast-ability action
- targeting resolution for tile, single-target, and AoE abilities
- AI candidate generation for ability use
- cooldown and range-aware decision making
- player targeting UI for aimed items or abilities

### 4. Combat depth is still shallow

Current state:

- [Core/Simulation/CombatResolver.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/CombatResolver.cs) does hit, crit, variance, and armor reduction
- [Content/items.json](c:/Users/kyle/projects/godotussy/Content/items.json) contains `damage_min`, `damage_max`, `crit_chance`, `speed_modifier`, and on-hit effects

Missing:

- weapon-derived damage ranges
- weapon-derived crit chance
- on-hit status effect application from weapons
- damage-type differentiation beyond the current minimal handling
- better enemy and player combat identities

### 5. AI schema/runtime drift reduces enemy variety

Current state:

- content AI types are `melee_rush`, `ranged_kite`, `ambush`, `patrol`, `support`
- [Core/Content/ContentLoader.cs](c:/Users/kyle/projects/godotussy/Core/Content/ContentLoader.cs) maps some of those into the frozen runtime brain strings
- [Core/AI/BrainFactory.cs](c:/Users/kyle/projects/godotussy/Core/AI/BrainFactory.cs) only instantiates `melee_rusher`, `ranged_kiter`, `patrol_guard`, `fleeing`
- `ambush` currently degrades into `fleeing` behavior via the content mapping

Missing:

- true ambush behavior
- support or buffer behavior
- ability-using ranged enemies
- role-specific target selection
- boss or elite encounter patterns

### 6. Progression economy is underdeveloped

Current state:

- player strength mostly comes from starting package and random loot
- item `requirements` exist in [Content/items.json](c:/Users/kyle/projects/godotussy/Content/items.json)
- those requirements are not enforced in equip flow

Missing:

- requirement validation for equipment
- stronger long-run progression than pure item RNG
- deterministic floor-based progression pacing
- clearer item tier and loadout decision support

### 7. UI guidance still trails system complexity

Current state:

- character creation shows narrative summaries but not full stat deltas
- character sheet shows core stats but not progression state or identity visuals
- inventory lacks equipment comparison

Missing:

- stat preview in character creation
- level-up UI
- equipment comparison and requirement warnings
- clearer explanation of archetype, race, gender, and appearance outcomes

### 8. Content volume is still modest for a replay-focused roguelike

Current state:

- enemy set is small
- items and abilities exist but not all are fully surfaced in combat or AI
- room prefabs and generation are solid but encounter variety can still repeat quickly

Missing:

- more enemy roles
- more item categories and progression tiers
- more status-effect combinations
- more distinct encounter patterns, including elites and minibosses

## Delivery Principles

All subagents must follow these rules:

- keep deterministic gameplay state in `Core/`
- use `Entity` components rather than bloating `Stats` with every new concern
- treat `GameManager` as coordinator, not simulation owner
- extend `EventBus` for UI/reactive updates instead of adding direct cross-layer references
- update save migration when persistent state changes
- add tests for every new simulation rule and every non-trivial UI contract

## Recommended Architecture For The Missing Systems

### Progression model

Do not overload `Stats` with XP and appearance. Add explicit components.

Recommended new components:

- `ProgressionComponent`
  - `Level`
  - `Experience`
  - `ExperienceToNextLevel`
  - `UnspentPerkPoints`
  - `Kills`
- `IdentityComponent`
  - `RaceId`
  - `GenderId`
  - `AppearanceId`
  - `SpriteVariantId`
  - optional `PortraitId`

Why:

- [Core/Simulation/Entity.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/Entity.cs) already supports generic components
- persistence already serializes selected known components centrally in [Core/Persistence/SaveSerializer.cs](c:/Users/kyle/projects/godotussy/Core/Persistence/SaveSerializer.cs)
- this keeps `Stats` focused on combat numbers and avoids future migration pain

### Ability model

Add a general cast pipeline instead of special-casing every item or enemy.

Recommended core pieces:

- `CastAbilityAction`
- `AbilityResolver`
- optional `CooldownComponent`
- player targeting state routed through UI, but final validation in `Core/`

### Visual identity model

Use data-driven appearance mapping, not hardcoded switch statements in renderers.

Recommended approach:

- add a small player appearance catalog JSON or runtime mapping file
- extend `CharacterCreationOptions` to include race, gender, and appearance ids
- let `EntityRenderer` resolve player visuals from identity component plus archetype

This matches Godot best practice: keep the renderer reactive to data rather than letting the menu directly manipulate world visuals.

## Parallel Workstreams

These workstreams are split for orchestrated subagents. They are grouped by dependency.

## Wave 1: Foundation Changes

These should start first because later work depends on them.

### Workstream A: Progression Foundation

Owner type: simulation/progression subagent

Scope:

- add `ProgressionComponent`
- award XP from enemy kills
- implement level threshold formula
- implement one first-pass level-up rule set
- emit progression events through `EventBus`
- serialize and migrate progression data

Likely files:

- `Core/Contracts/Types/` new progression types if needed
- [Core/Simulation/Entity.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/Entity.cs)
- [Core/Simulation/Actions/AttackAction.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/Actions/AttackAction.cs)
- [Scripts/Autoloads/GameManager.cs](c:/Users/kyle/projects/godotussy/Scripts/Autoloads/GameManager.cs)
- [Scripts/Autoloads/EventBus.cs](c:/Users/kyle/projects/godotussy/Scripts/Autoloads/EventBus.cs)
- [Core/Persistence/SaveSerializer.cs](c:/Users/kyle/projects/godotussy/Core/Persistence/SaveSerializer.cs)
- [Core/Persistence/SaveMigrator.cs](c:/Users/kyle/projects/godotussy/Core/Persistence/SaveMigrator.cs)
- tests under `Tests/SimulationTests`, `Tests/PersistenceTests`, `Tests/UITests`

Acceptance criteria:

- killing enemies grants XP based on content data
- player levels increase deterministically
- level-up grants a visible benefit
- save/load preserves progression
- HUD and log can display XP and level events

### Workstream B: Identity And Character Creation Foundation

Owner type: UI plus runtime-data subagent

Scope:

- extend character creation with race, gender, and appearance
- store those selections in `CharacterCreationOptions`
- create `IdentityComponent` on the player
- expose identity in character sheet and runtime state
- route player render variant through data instead of a hardcoded single sprite

Likely files:

- [Scripts/UI/MainMenu.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/MainMenu.cs)
- [Scripts/Autoloads/GameManager.cs](c:/Users/kyle/projects/godotussy/Scripts/Autoloads/GameManager.cs)
- [Scripts/UI/CharacterSheet.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/CharacterSheet.cs)
- [Scripts/World/EntityRenderer.cs](c:/Users/kyle/projects/godotussy/Scripts/World/EntityRenderer.cs)
- new content file for player appearance definitions
- tests under `Tests/UITests` and rendering smoke tests

Acceptance criteria:

- character creation exposes class, race, gender, and appearance choices
- different choices produce visibly different player tiles or sprites
- identity choices survive save/load
- sheet and menu preview show the chosen identity

## Wave 2: Core Tactical Depth

These can begin once Wave 1 contracts are in place.

### Workstream C: Ability Casting And Targeting

Owner type: combat/simulation subagent

Scope:

- implement `CastAbilityAction`
- parse and resolve ability effects from [Content/abilities.json](c:/Users/kyle/projects/godotussy/Content/abilities.json)
- allow items with `cast_ability` to work
- support single target, self, tile, and AoE circle targeting
- support effect types already present in content: damage, apply_status, teleport, heal_self

Likely files:

- `Core/Simulation/Actions/` new files
- [Core/Content/ContentModels.cs](c:/Users/kyle/projects/godotussy/Core/Content/ContentModels.cs)
- [Core/Simulation/Actions/UseItemAction.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/Actions/UseItemAction.cs)
- [Scripts/UI/UIActionFactory.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/UIActionFactory.cs)
- [Scripts/UI/InputHandler.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/InputHandler.cs)
- likely a new `Scripts/UI/AbilityTargetingUI.cs`

Acceptance criteria:

- fireball, blink, arrow shot, heavy slam, life drain, phase shift, acid splash, and war cry are executable
- aimed abilities validate line of sight and range
- item-based ability use works for the player
- tests cover direct resolution and targeting edge cases

### Workstream D: Combat Model Expansion

Owner type: combat/balance subagent

Scope:

- use weapon damage ranges and crit chance from item stats
- apply on-hit item effects
- support more meaningful damage typing and effect hooks
- improve combat log messaging for special outcomes

Likely files:

- [Core/Simulation/CombatResolver.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/CombatResolver.cs)
- [Core/Simulation/Actions/AttackAction.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/Actions/AttackAction.cs)
- [Core/Simulation/StatusEffectProcessor.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/StatusEffectProcessor.cs)
- [Scripts/UI/CombatLog.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/CombatLog.cs)
- tests under `Tests/SimulationTests`

Acceptance criteria:

- equipped weapons materially change damage output
- crit chance can come from gear rather than hardcoded defaults only
- Viper Fang poison and Flamebrand burning actually trigger
- combat outcomes remain deterministic under a fixed seed

### Workstream E: Enemy AI And Ability Use

Owner type: AI subagent

Scope:

- add new brain or profile support for `ambush` and `support`
- teach AI to consider ability candidates in [Core/AI/AIBrain.cs](c:/Users/kyle/projects/godotussy/Core/AI/AIBrain.cs)
- add cooldown and preferred-range logic
- stop degrading `ambush` into `fleeing`

Likely files:

- [Core/AI/AIBrain.cs](c:/Users/kyle/projects/godotussy/Core/AI/AIBrain.cs)
- [Core/AI/BrainFactory.cs](c:/Users/kyle/projects/godotussy/Core/AI/BrainFactory.cs)
- [Core/AI/AIProfiles.cs](c:/Users/kyle/projects/godotussy/Core/AI/AIProfiles.cs)
- [Core/AI/UtilityScorer.cs](c:/Users/kyle/projects/godotussy/Core/AI/UtilityScorer.cs)
- [Core/Content/ContentLoader.cs](c:/Users/kyle/projects/godotussy/Core/Content/ContentLoader.cs)
- tests under `Tests/AITests`

Acceptance criteria:

- goblin archers use ranged attacks
- wraiths behave like ambushers rather than generic runners
- support-capable enemies can buff allies or debuff the player when content asks for it
- AI remains deterministic and test-covered

## Wave 3: Progression Economy, Content, And UX

These can proceed in parallel after the foundational systems land.

### Workstream F: Equipment Requirements And Progression Economy

Owner type: content/runtime-rules subagent

Scope:

- validate item requirements on equip
- surface requirement failures in UI
- use level requirements now that progression exists
- rebalance loot table pacing around real player growth

Likely files:

- [Content/items.json](c:/Users/kyle/projects/godotussy/Content/items.json)
- [Core/Simulation/Actions/ToggleEquipAction.cs](c:/Users/kyle/projects/godotussy/Core/Simulation/Actions/ToggleEquipAction.cs)
- [Scripts/UI/InventoryUI.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/InventoryUI.cs)
- [Core/Content/LootTableResolver.cs](c:/Users/kyle/projects/godotussy/Core/Content/LootTableResolver.cs)
- [Core/Content/DifficultyScaler.cs](c:/Users/kyle/projects/godotussy/Core/Content/DifficultyScaler.cs)

Acceptance criteria:

- players cannot equip gear above their current requirements
- UI clearly explains why equip failed
- floor progression feels less RNG-dependent

### Workstream G: Character Creation UX And Runtime Feedback

Owner type: UI subagent

Scope:

- add stat delta preview in character creation
- add identity preview panel
- add level-up and perk-spend UI
- add equipment comparison in inventory and tooltip
- update help text and onboarding overlays

Likely files:

- [Scripts/UI/MainMenu.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/MainMenu.cs)
- [Scripts/UI/InventoryUI.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/InventoryUI.cs)
- [Scripts/UI/Tooltip.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/Tooltip.cs)
- [Scripts/UI/CharacterSheet.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/CharacterSheet.cs)
- [Scripts/UI/HUD.cs](c:/Users/kyle/projects/godotussy/Scripts/UI/HUD.cs)

Acceptance criteria:

- every creation choice has a visible mechanical preview
- level-up state is obvious in HUD and sheet
- inventory compares candidate gear against equipped gear

### Workstream H: Content Expansion

Owner type: content-design subagent

Scope:

- add more enemies for each tactical role
- add more weapons and armor that actually interact with the expanded combat model
- add more ability-driven encounters and elites
- expand room prefab tags to support varied encounter pacing

Likely files:

- [Content/enemies.json](c:/Users/kyle/projects/godotussy/Content/enemies.json)
- [Content/items.json](c:/Users/kyle/projects/godotussy/Content/items.json)
- [Content/abilities.json](c:/Users/kyle/projects/godotussy/Content/abilities.json)
- [Content/room_prefabs.json](c:/Users/kyle/projects/godotussy/Content/room_prefabs.json)
- [Content/loot_tables.json](c:/Users/kyle/projects/godotussy/Content/loot_tables.json)
- [Tests/ContentTests/ContentValidationTests.cs](c:/Users/kyle/projects/godotussy/Tests/ContentTests/ContentValidationTests.cs)

Acceptance criteria:

- deeper floor progression has visible enemy and loot variety
- at least one elite or miniboss pattern exists
- content validates cleanly and is actually consumed by runtime systems

## Concrete Mismatches That Must Be Closed

These are specific schema/runtime mismatches already present.

### Critical mismatches

- `xp_value` in [Content/enemies.json](c:/Users/kyle/projects/godotussy/Content/enemies.json) is effectively dead today
- enemy `abilities` in [Content/enemies.json](c:/Users/kyle/projects/godotussy/Content/enemies.json) are not fully used by AI
- item `cast_ability` actions in [Content/items.json](c:/Users/kyle/projects/godotussy/Content/items.json) need a real runtime path
- item `on_hit` effects need combat integration
- item `requirements` need runtime validation

### Secondary mismatches

- content AI types are richer than runtime brain coverage
- player identity choices are richer in the design intent than in runtime presentation
- character sheet and HUD trail behind the expected progression systems

## Recommended Orchestration Order

Use subagents in waves, not all at once.

### Wave 1

- Progression foundation
- Identity and character creation foundation

Reason:

- both create shared contracts and save-shape changes that later work depends on

### Wave 2

- Ability casting and targeting
- Combat model expansion
- Enemy AI and ability use

Reason:

- these three form the tactical layer and should converge against the same new contracts

### Wave 3

- Equipment requirements and progression economy
- Character creation UX and runtime feedback
- Content expansion

Reason:

- these are safest once the simulation and progression contracts are stable

## Definition Of Done

The project should be considered meaningfully improved only when all of the following are true:

- character creation includes race, gender, and appearance and those choices change the rendered player
- enemies grant XP and the player can level up during normal play
- level-up creates real gameplay decisions or real stat growth
- ability-bearing items and enemies can actually cast or use their abilities
- combat uses item-derived damage and on-hit effects
- enemy roles are tactically distinct in live play, not just in JSON labels
- equipment requirements are enforced and surfaced in the UI
- content additions are validated by tests and consumed by runtime systems
- save/load preserves new progression and identity data
- all new behavior is covered by deterministic tests where possible

## Non-Goals For This Pass

To keep the subagents focused, do not let the work expand into these unless explicitly requested later:

- full narrative campaign structure
- controller support overhaul
- full art pipeline replacement
- shops, towns, or meta-progression outside the dungeon loop
- networking or multiplayer

## Final Guidance For The Orchestrator

The orchestrator should treat this as a staged program, not a grab bag. Shared contracts and persistence changes must land before parallel polish work. Use subagents to own bounded workstreams with explicit tests, then run an integration pass after each wave.