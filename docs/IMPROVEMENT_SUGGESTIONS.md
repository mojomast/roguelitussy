# Improvement Suggestions — Roguelitussy

> Generated from a parallel review pass of 16 focused subagents across combat, items, abilities, status effects, AI, progression, identity, UI/UX, inventory, save/load determinism, tools, dungeon generation, onboarding, game feel, performance, and test/CI quality.
>
> Last updated: 2026-07-18

> Update 2026-07-02: Wave 1 roguelite foundations are partially implemented outside the original improvement table: content-backed synergies, ascension modifiers, daily modifiers, narrative templates, factions, boss phase state, deterministic daily seed helpers, run epitaph generation, faction reputation state, and save version 15 persistence. Wave 2 presentation/feel/content-expansion tasks remain open unless their existing table row says otherwise.

> Update 2026-07-02 Wave 2: the roguelite presentation/content pass is implemented at the text/UI-event level. Daily challenge title entry, ascension controls/shop-price modifier, synergy and reputation HUD/log feedback, boss phase event emission, floor-clear rewards, critical-hit log callouts, rest-interruption clarity, `blinded`, `chain_lightning`, `soul_collector`, new enemies/items/relics, and save serialization for boss/faction/synergy payloads are live. Remaining polish is real Godot 4.5.2 playthrough validation of timing/feel and richer non-text VFX.

> Update 2026-07-18 relic lifecycle: save version 17 persists cumulative applied relic-stat totals. Bone Amulet/Soul Collector use resulting kill counts, Glass Cannon applies once, Warlord's Crest applies only missing capped depth progress, and floor/rest hook messages reach EventBus. Remaining authored relic mismatches are rest cadence, Shadow Step, Echo Shard, and Merchant Badge cached-floor pricing.

## How Worker Subagents Should Use This Document

1. Pick a suggestion by ID. Each suggestion is scoped to a focused, implementable change.
2. Read the **Target files**, **Why it improves the game**, and **Acceptance criteria** before writing code.
3. Add tests first where feasible. Update docs (`docs/SYSTEMS.md`, `docs/CONTENT.md`, `docs/TESTING.md`, `README.md`) as required by `AGENTS.md`.
4. Run the verification commands after each batch:
   ```bash
   dotnet build godotussy.csproj -p:UseGodotStubs=true
   dotnet build Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
   dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true
   dotnet run --project Tests/godotussy.Tests.csproj -p:UseGodotStubs=true -p:RenderingValidation=true
   ```
5. Mark completed suggestions in this file (change status to `done`) and update `improvments.md` / `DEVELOPMENT_RESUME_REPORT.md` accordingly.
6. Do not start work that crosses ownership boundaries without coordinating. See `AGENTS.md` for ownership guidance.

## Priority Legend

- **P0** — Foundation fix; blocks other work or closes a critical authored-content gap.
- **P1** — High-impact gameplay/UX improvement; safe to implement in a focused PR.
- **P2** — Polish, optimization, or longer-term feature; implement after P0/P1 in a wave.

---

## Executive Summary Table

| ID | Title | Priority | Topic | Status |
|---|---|---|---|---|
| COM-1 | Implement authored status-effect control flags | P0 | Combat | done |
| COM-2 | Make ranged weapons actually ranged | P1 | Combat | open |
| COM-3 | Add weapon archetype properties (cleave/reach) | P1 | Combat | open |
| COM-4 | Soften armor scaling to avoid early invulnerability | P1 | Combat | open |
| COM-5 | Turn critical hits into sparkle moments | P1 | Combat | open |
| ITM-1 | Roll enemy loot tables on death and add gold drops | P0 | Itemization | done |
| ITM-2 | Add at least one guaranteed chest per floor | P1 | Itemization | open |
| ITM-3 | Make room `ItemQualityBonus` bias loot rarity | P1 | Itemization | open |
| ITM-4 | Add encumbrance from item weight | P1 | Itemization | open |
| ITM-5 | Enable aimed scrolls from the inventory UI | P0 | Itemization | done |
| ABI-1 | Add field targeting mode for aimed scrolls/abilities | P0 | Abilities | done |
| ABI-2 | Implement `aoe_line` and `aoe_cone` targeting shapes | P1 | Abilities | open |
| ABI-3 | Enforce mana costs and unify energy costs | P1 | Abilities | open |
| ABI-4 | Add content-driven cooldowns to `AbilityTemplate` | P1 | Abilities | open |
| ABI-5 | Emit `CombatEvent`s for teleport and `heal_self` | P1 | Abilities | open |
| STA-1 | Make `StatusEffectProcessor` data-driven from JSON | P0 | Status Effects | done |
| STA-2 | Implement authored `tick_timing` | P1 | Status Effects | open |
| STA-3 | Honor status flags (`skip_turn`, `phase`, `immune_physical`) | P0 | Status Effects | done |
| STA-4 | Wire status visuals into UI and entity renderer | P1 | Status Effects | done |
| STA-5 | Fix status event payloads and emit removals | P1 | Status Effects | done |
| AI-1 | Honor authored `ai_params` at runtime | P0 | AI / Encounters | done |
| AI-2 | Make ambushers lurk at chokepoints and strike on contact | P1 | AI / Encounters | done |
| AI-3 | Make ranged enemies respect minimum range and kite | P1 | AI / Encounters | done |
| AI-4 | Make support enemies seek allies and avoid friendly fire | P1 | AI / Encounters | done |
| AI-5 | Add group aggro propagation | P1 | AI / Encounters | done |
| PRG-1 | Flatten the early XP curve | P1 | Progression | open |
| PRG-2 | Make perks build-shaping, not just stat bundles | P1 | Progression | open |
| PRG-3 | Draft three perks per level-up instead of the full list | P1 | Progression | open |
| PRG-4 | Weight the draft by archetype tags | P2 | Progression | open |
| PRG-5 | Auto-apply archetype growth on level-up | P1 | Progression | open |
| CHR-1 | Restore `CharacterOptions` when loading a saved run | P0 | Character / Identity | done |
| CHR-2 | Make race mechanically meaningful with minor bonuses | P1 | Character / Identity | open |
| CHR-3 | Give each archetype a signature starting ability | P1 | Character / Identity | done |
| CHR-4 | Improve creation preview clarity | P1 | Character / Identity | done |
| CHR-5 | Add a "Randomize Build" option | P2 | Character / Identity | open |
| UI-1 | Add mouse interaction to the inventory grid | P1 | UI / UX | open |
| UI-2 | Add message categories and filtering to combat log | P1 | UI / UX | done |
| UI-3 | Make status effects visible on HUD and entity sprites | P1 | UI / UX | done |
| UI-4 | Standardize overlay close keys and inline hotkey hints | P1 | UI / UX | done |
| UI-5 | Add colorblind-safe rarity and HP indicators | P1 | UI / UX | done |
| INV-1 | Add category/rarity filters to inventory | P1 | Inventory | open |
| INV-2 | Add bulk use/drop for stacks | P1 | Inventory | open |
| INV-3 | Add explicit stack-split quantity prompt | P1 | Inventory | open |
| INV-4 | Render and highlight ground items in world view | P1 | Inventory | open |
| INV-5 | Add pickup radius for adjacent loot | P1 | Inventory | open |
| SAV-1 | Persist enemy template identity | P0 | Save / Determinism | done |
| SAV-2 | Persist `TurnScheduler` actor order | P0 | Save / Determinism | done |
| SAV-3 | Add deterministic replay regression tests | P1 | Save / Determinism | done |
| SAV-4 | Harden RNG rehydration order | P1 | Save / Determinism | done |
| SAV-5 | Make autosave automatic and crash-safe | P1 | Save / Determinism | open |
| TOL-1 | Add file-watcher-based content validation | P1 | Tools / Workflow | open |
| TOL-2 | Run full `ContentLoader` validation before tool save | P1 | Tools / Workflow | open |
| TOL-3 | Promote `res://` asset-path validation to loader errors | P1 | Tools / Workflow | open |
| TOL-4 | Expand item authoring to cover effects/requirements/sprite | P1 | Tools / Workflow | open |
| TOL-5 | Add room metadata editing to the Rooms tab | P1 | Tools / Workflow | open |
| GEN-1 | Implement authored traps and hazard tiles | P0 | Generation | done |
| GEN-2 | Add themed floor sets via prefab tag filtering | P1 | Generation | done |
| GEN-3 | Guarantee one landmark special room per floor | P1 | Generation | open |
| GEN-4 | Implement locked doors and key placement | P1 | Generation | done |
| GEN-5 | Clean up and expand the prefab library | P1 | Generation | open |
| ONB-1 | Add a "First Delve" welcome message to combat log | P1 | Onboarding | open |
| ONB-2 | Show an objective hint when down-stairs become visible | P1 | Onboarding | open |
| ONB-3 | Improve game-over screen with death-specific learning tips | P1 | Onboarding | open |
| ONB-4 | Surface starter-kit item effects in main menu preview | P1 | Onboarding | done |
| ONB-5 | Add a temporary key-reminder ribbon to HUD | P1 | Onboarding | open |
| GFX-1 | Make hit flashes readable and layered | P1 | Game Feel | done |
| GFX-2 | Add lightweight camera shake on impactful hits | P1 | Game Feel | open |
| GFX-3 | Polish damage popups (color/scale/crit/miss/heal) | P1 | Game Feel | done |
| GFX-4 | Wire ability/item `animation` and `sfx` fields | P1 | Game Feel | open |
| GFX-5 | Add projectile travel for ranged abilities and attacks | P2 | Game Feel | open |
| PERF-1 | Make `WorldView._Process` event-driven | P1 | Performance | open |
| PERF-2 | Avoid full-grid fog iteration every turn | P1 | Performance | open |
| PERF-3 | Replace `EntityRenderer` child-name string lookups | P1 | Performance | open |
| PERF-4 | Pool/reuse per-tile art nodes | P1 | Performance | open |
| PERF-5 | Cache `WorldState.GetEntitiesInRadius` | P1 | Performance | open |
| ARC-1 | Finish incremental `GameManager` facade refactor under 500 lines | P1 | Architecture | open |
| TST-1 | Capture full exception details on test failure | P1 | Test / CI | done |
| TST-2 | Add test filtering / selection to harness | P1 | Test / CI | done |
| TST-3 | Reset shared static stub state between tests | P1 | Test / CI | done |
| TST-4 | Treat build warnings as errors in CI | P1 | Test / CI | done |
| TST-5 | Cache NuGet packages and Godot editor in CI | P1 | Test / CI | done |

---

## 1. Combat

### COM-1 — Implement authored status-effect control flags

- **Priority:** P0
- **Target files:**
  - `Core/Simulation/TurnScheduler.cs`
  - `Core/Simulation/Actions/MoveAction.cs`
  - `Core/Simulation/CombatResolver.cs`
  - `Core/Simulation/Actions/AttackAction.cs`
  - `Content/status_effects.json`
- **Why it improves the game:** `skip_turn`, `phase_through_walls`, and `immune_physical` are already authored and validated but do nothing. Stun/freeze become real crowd control; phased becomes a real escape/ambush tool.
- **Implementation notes:**
  - In `TurnScheduler`, skip the actor's turn while `skip_turn` is active.
  - In `MoveAction`, treat walls as walkable when `phase_through_walls` is active.
  - In `CombatResolver`, return 0 physical damage for `immune_physical` defenders.
- **Acceptance criteria / tests:**
  - A stunned actor takes zero actions for the duration.
  - A phased player can move through walls.
  - An immune entity takes zero physical damage.
  - Existing burn/poison/regen tests still pass.

### COM-2 — Make ranged weapons actually ranged

- **Priority:** P1
- **Target files:**
  - `Content/items.json`
  - `Core/Contracts/Types/ItemTemplate.cs`
  - `Core/Content/ContentLoader.cs`
  - `Core/Simulation/Actions/AttackAction.cs`
  - `Scripts/UI/UIActionFactory.cs`
- **Why it improves the game:** `longbow` exists in content but functions as a melee stat stick. Ranged weapons enable kiting builds and symmetric archer encounters.
- **Implementation notes:**
  - Add optional `range` to weapon items and `ItemTemplate`.
  - Extend `AttackAction` to allow attacks beyond distance 1 when a ranged weapon is equipped and LOS exists.
  - Update `UIActionFactory` so directional input into a distant enemy fires rather than moves.
- **Acceptance criteria / tests:**
  - A player with `longbow` can attack an enemy 5–8 tiles away with LOS.
  - Attacking beyond range or without LOS returns `ActionResult.Blocked`.
  - Non-ranged weapons still require adjacency.

### COM-3 — Add weapon archetype properties (cleave and reach)

- **Priority:** P1
- **Target files:**
  - `Core/Contracts/Types/ItemTemplate.cs`
  - `Core/Content/ContentLoader.cs`
  - `Core/Simulation/Actions/AttackAction.cs`
  - `Content/items.json`
- **Why it improves the game:** Every weapon is currently a single-target melee swing. Cleave and reach create distinct positioning tactics.
- **Implementation notes:**
  - Add optional `Reach` (int) and `CleaveRadius` (int) fields.
  - In `AttackAction`, apply reduced cleave damage to other adjacent enemies when `CleaveRadius > 0`.
  - Give `battleaxe` a small cleave and add/retag a spear with `reach: 2`.
- **Acceptance criteria / tests:**
  - Battleaxe hit damages the primary target and one other adjacent enemy.
  - A spear can strike at Chebyshev distance 2 (respecting LOS/walkable rules).
  - Daggers/unarmed remain single-target, range 1.

### COM-4 — Soften armor scaling to avoid early invulnerability

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/CombatResolver.cs`
  - `Content/enemies.json`
  - `Tests/SimulationTests/CombatResolverTests.cs`
- **Why it improves the game:** A player in plate+shield can reach 15 Defense, trivializing early enemies. Diminishing returns keep light/medium armor relevant.
- **Implementation notes:**
  - Replace linear `armor / 2` reduction with a diminishing-return formula, e.g. `reduction = armor - (armor * armor) / (armor + K)` where `K ≈ 10–12`, keeping min damage 1.
  - Optionally add `armor_penetration` to some heavy enemy abilities.
- **Acceptance criteria / tests:**
  - 15 Defense no longer reduces a 10-damage hit to 1–3.
  - Low-Defense enemies still die quickly.
  - Regression test locks expected damage range for mid-game armor vs. mid-game enemy.

### COM-5 — Turn critical hits into sparkle moments

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/CombatResolver.cs`
  - `Core/Simulation/Actions/AttackAction.cs`
  - `Scripts/UI/CombatLog.cs`
- **Why it improves the game:** Crits currently only double damage and are easy to miss in the log. Guaranteeing on-hit procs on crit makes high-crit weapons feel deliberate.
- **Implementation notes:**
  - In `CombatResolver.ResolveMeleeAttack`, guarantee that a crit triggers the weapon's on-hit effect(s) regardless of their normal chance.
  - Add a distinct crit log line/flavor.
- **Acceptance criteria / tests:**
  - A crit with `Flamebrand` always applies burning, even when the normal roll would fail.
  - A 0% crit-chance weapon never applies the guaranteed-on-crit behavior.
  - Crit log messages are distinct and tested.

---

## 2. Itemization and Loot Economy

### ITM-1 — Roll enemy loot tables on death and add gold drops

- **Status:** done
- **Priority:** P0
- **Target files:**
  - `Core/Simulation/DeathResolver.cs`
  - `Core/Content/ContentModels.cs`
  - `Core/Contracts/Types/EnemyTemplate.cs`
  - `Core/Content/ContentLoader.cs`
  - `Core/Simulation/Actions/AttackAction.cs`
  - `Core/Simulation/Actions/CastAbilityAction.cs`
  - `Core/Simulation/StatusEffectProcessor.cs`
  - `Content/enemies.json`
  - `Tests/SimulationTests/DeathResolverTests.cs`
  - `Tests/ContentTests/ContentValidationTests.cs`
- **Why it improves the game:** Every enemy has a `loot_table_id`, but kills previously awarded only XP. Turning authored loot tables into actual drops funds the shop and gives each enemy an economic footprint.
- **Implementation notes:**
  - `EnemyTemplate` now carries `LootTableId`, `GoldMin`, and `GoldMax` projected from `enemies.json`.
  - `DeathResolver.ResolveKill` rolls the victim's loot table with a deterministic seed and drops items at the corpse position; it also awards gold to the killer's `WalletComponent` when `gold_max > 0`.
  - Loot and gold are derived from the enemy's `TemplateId` at death time so the save format does not need to change; `EnemyComponent` keeps only `TemplateId`.
  - `GameLoop` forwards normal and skipped-turn status tick logs, expirations, dirty positions, and typed combat/death events through the aggregate outcome.
- **Acceptance criteria / tests:**
  - `DeathResolver.KillingRatDropsRatLootAndGold` asserts items on the ground and gold in the killer's wallet.
  - `DeathResolver.LootRollIsDeterministicForSameSeed` asserts identical drops for identical seed/depth/position/turn/entity.
  - `DeathResolver.GoldRollRespectsMinMax` asserts every kill awards gold in the authored range.
  - `ContentValidationTests.EnemyGoldRangesAreValid` asserts `gold_min <= gold_max` and non-negative ranges for every enemy.

### ITM-2 — Add at least one guaranteed chest per floor

- **Priority:** P1
- **Target files:**
  - `Core/Generation/DungeonGenerator.cs`
  - `Core/Contracts/Types/LevelData.cs`
  - `Scripts/Autoloads/GameManager.cs`
  - `Tests/GenerationTests/DungeonGeneratorTests.cs`
- **Why it improves the game:** Chests only appear if a room prefab has a spawn point. A guaranteed chest creates a reliable reward-room moment.
- **Implementation notes:**
  - Add `PlaceGuaranteedChestSpawn` after item/enemy placement; pick a non-start room and an unoccupied tile.
  - `GameManager` already creates chest entities from `ChestSpawns`.
- **Acceptance criteria / tests:**
  - `DungeonGeneratorTests` asserts `LevelData.ChestSpawns.Count >= 1` for depths 0–5.
  - Chests use `chest_loot` for depth < 4 and `deep_chest_loot` for depth >= 4.

### ITM-3 — Make room `ItemQualityBonus` bias loot rarity

- **Priority:** P1
- **Target files:**
  - `Scripts/Autoloads/GameManager.cs` (`CreateFloorItems`)
  - `Core/Content/LootTableResolver.cs`
  - `Tests/ContentTests/BalanceTests.cs`
- **Why it improves the game:** `ItemQualityBonus` exists on prefabs but only shifts effective depth. Using it to suppress common items makes trophy rooms more rewarding.
- **Implementation notes:**
  - Add overload `RollTable(..., int qualityBonus)`.
  - For `qualityBonus > 0`, reduce common weight and increase uncommon/rare weight before the RNG roll.
- **Acceptance criteria / tests:**
  - For a fixed seed, mean item value/rarity from `floor_loot` with `qualityBonus = 2` is strictly higher than with `qualityBonus = 0`.
  - `LootResolutionIsDeterministic` still passes with `qualityBonus`.

### ITM-4 — Add encumbrance from item weight

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/Inventory.cs`
  - `Core/Simulation/TurnScheduler.cs`
  - `Scripts/UI/InventoryUI.cs`
  - `Tests/SimulationTests/ActionTests.cs`
- **Why it improves the game:** Weight is currently a dead stat. Encumbrance forces players to leave junk, sell to merchants, or invest in speed.
- **Implementation notes:**
  - Add `EncumbranceLevel` enum (`None`, `Light`, `Heavy`) and threshold logic in `InventoryComponent`.
  - Reduce energy gain based on encumbrance tier.
  - Show the tier in `InventoryUI` next to weight.
- **Acceptance criteria / tests:**
  - `TotalWeight` equals sum of `StackCount * template.Weight`.
  - A heavily encumbered actor gains less energy than an unencumbered one.
  - UI footer displays the encumbrance tier.

### ITM-5 — Enable aimed scrolls from the inventory UI

- **Status:** done
- **Priority:** P0
- **Target files:**
  - `Scripts/UI/UIActionFactory.cs`
  - `Scripts/UI/UIRoot.cs`
  - `Scripts/UI/InventoryUI.cs`
  - `Tests/SimulationTests/ItemAbilityRuntimeTests.cs`
- **Why it improves the game:** `scroll_fireball` and `scroll_blink` were unusable from the normal inventory because `CreateUseItemAction` returned `null` for non-self targeted abilities.
- **Implementation notes:**
  - `InventoryUI.SubmitUse` detects items flagged `RequiresTargetSelection` and delegates to `TargetingOverlay.EnterTargetingForItem`.
  - Once the player confirms a target tile, `TargetingOverlay.Confirm` constructs `UseItemAction(actorId, itemId, template, ability, targetPosition)` and emits `PlayerActionSubmitted`.
  - Canceling (`Esc`) exits targeting mode without consuming the scroll.
- **Acceptance criteria / tests:**
  - `ItemAbilityRuntimeTests.UIActionFactory creates UseItemAction with target for fireball` and `...for blink` pass.
  - `ItemAbilityRuntimeTests.UseItemAction with invalid target fails validation` ensures no consumption on bad targets.
  - `TargetingOverlayTests.Cancel does not emit action` ensures cancel is safe.

---

## 3. Abilities and Targeting

### ABI-1 — Add field targeting mode for aimed scrolls and abilities

- **Status:** done
- **Priority:** P0
- **Target files:**
  - `Scripts/UI/UIActionFactory.cs`
  - `Scripts/UI/UIRoot.cs`
  - `Scripts/UI/TargetingOverlay.cs`
  - `Scripts/UI/InputHandler.cs`
  - `Scripts/UI/InventoryUI.cs`
  - `Scripts/World/WorldView.cs`
  - `Core/Simulation/Actions/UseItemAction.cs`
- **Why it improves the game:** Restored authored scrolls to playable status and gives the player the same targeting expressiveness the AI already has.
- **Implementation notes:**
  - Added `TargetingOverlay` owned by `UIRoot`; it intercepts directional keys, Enter, and Escape while active.
  - `InventoryUI` enters targeting mode for items flagged `RequiresTargetSelection`.
  - `WorldView` renders a cursor and AoE preview from EventBus targeting events without mutating `WorldState`.
  - `UIActionFactory.CreateUseItemAction` accepts an optional `Position? target`.
- **Acceptance criteria / tests:**
  - `TargetingOverlayTests` cover cursor start position, invalid-target confirm, cancel safety, and AoE preview shape.
  - `ItemAbilityRuntimeTests` cover fireball/blink action construction and validation.
  - Content validation rejects mismatched `cast_ability:self`/`cast_ability:aimed` item/ability target combinations.

### ABI-2 — Implement `aoe_line` and `aoe_cone` targeting shapes

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/AbilityResolver.cs`
  - `Core/Simulation/Actions/CastAbilityAction.cs`
  - `Core/Contracts/Types/AbilityTemplate.cs`
  - `Core/Content/ContentModels.cs`
  - `Content/abilities.json`
  - `Tests/SimulationTests/AbilityTests.cs`
- **Why it improves the game:** Line beams and cones are staple tactical shapes; the content schema already anticipates them.
- **Implementation notes:**
  - `aoe_line`: cells from caster toward target, width 1 by default, widened by `Width`.
  - `aoe_cone`: origin at caster or target, arc from `Arc`, range from `Range`.
  - Add sample abilities `lightning_bolt` and `cone_of_cold`.
- **Acceptance criteria / tests:**
  - Line ability hits enemies along the line and stops at walls.
  - Cone ability hits enemies within the arc and misses those outside.
  - Update `ContentValidationTests` expected ability count if new abilities are added.

### ABI-3 — Enforce mana costs and unify energy costs

- **Priority:** P1
- **Target files:**
  - `Core/Contracts/Types/Stats.cs`
  - `Core/Contracts/Types/AbilityTemplate.cs`
  - `Core/Simulation/Actions/CastAbilityAction.cs`
  - `Core/Simulation/Actions/UseItemAction.cs`
- **Why it improves the game:** `ManaCost` is parsed but never consumed; `UseItemAction` hardcodes 500 energy even when the embedded ability declares a different cost.
- **Implementation notes:**
  - Add `MP`/`MaxMP` to `Stats`.
  - `CastAbilityAction.Validate` blocks when MP is insufficient; `Execute` deducts MP.
  - `UseItemAction.GetEnergyCost()` returns the embedded ability's `EnergyCost` when casting, otherwise 500.
- **Acceptance criteria / tests:**
  - Casting `fireball` with `MP < 8` is blocked.
  - Casting with enough MP deducts exactly 8.
  - `scroll_fireball` costs the ability's energy, not 500.

### ABI-4 — Add content-driven cooldowns to `AbilityTemplate`

- **Priority:** P1
- **Target files:**
  - `Core/Contracts/Types/AbilityTemplate.cs`
  - `Core/Content/ContentModels.cs`
  - `Core/Content/ContentLoader.cs`
  - `Core/Simulation/Actions/CastAbilityAction.cs`
  - `Content/abilities.json`
- **Why it improves the game:** Only enemy slots have authored cooldowns. Player abilities and scrolls can currently be spammed.
- **Implementation notes:**
  - Add optional `cooldown` to ability cost definition.
  - `CastAbilityAction.Execute` uses slot cooldown if present, otherwise `Ability.Cooldown`.
- **Acceptance criteria / tests:**
  - Casting a player ability with a 3-turn cooldown twice blocks the second cast until the cooldown ticks down.

### ABI-5 — Emit `CombatEvent`s for teleport and `heal_self`

- **Priority:** P1
- **Target files:**
  - `Core/Contracts/Types/CombatEvent.cs`
  - `Core/Simulation/Actions/CastAbilityAction.cs`
  - `Scripts/Autoloads/EventBus.cs`
  - `Scripts/World/WorldView.cs` / `AnimationController.cs`
- **Why it improves the game:** Teleport and `heal_self` currently modify state but produce no event payload, so the UI cannot animate blink or show healing numbers.
- **Implementation notes:**
  - Extend `CombatEvent` with movement/healing fields.
  - Add events in `CastAbilityAction.Execute` for teleport destination and heal amount.
  - Forward through `GameManager`/`EventBus` so `WorldView` can animate.
- **Acceptance criteria / tests:**
  - Blink produces a combat event containing old and new caster position.
  - Life drain produces a combat event containing caster healing amount.

---

## 4. Status Effects

### STA-1 — Make `StatusEffectProcessor` data-driven from JSON

- **Priority:** P0
- **Target files:**
  - `Core/Simulation/StatusEffectProcessor.cs`
  - `Core/Contracts/Types/StatusEffectInstance.cs`
  - `Core/Content/ContentLoader.cs`
  - `Core/Contracts/IContentDatabase.cs`
  - `Tests/SimulationTests/StatusEffectTests.cs`
- **Why it improves the game:** `status_effects.json` declares durations, stacking, tick effects, modifiers, lifecycle, etc., but `StatusEffectProcessor` hardcodes everything.
- **Implementation notes:**
  - Pass an `IContentDatabase`/`StatusEffectDefinition` lookup into `StatusEffectProcessor`.
  - Resolve `tick_effects`, `stat_modifiers`, `stackable`, `max_stacks`, `refreshable`, `on_apply_effects`, and `on_expire_effects` from the definition.
- **Acceptance criteria / tests:**
  - Changing `poisoned.tick_effects[0].value` from 2 to 3 changes per-tick damage without code changes.
  - `corroded` armor reduction and `shielded` armor bonus scale with authored `stat_modifiers`.
- **Status:** done. `StatusEffectProcessor` now resolves tick effects, stat modifiers, stacking, refresh, and lifecycle effects from `StatusEffectDefinition`; `IContentDatabase` exposes status lookup.

### STA-2 — Implement authored `tick_timing`

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/StatusEffectProcessor.cs`
  - `Core/Simulation/TurnScheduler.cs`
  - `Core/Simulation/GameLoop.cs`
- **Why it improves the game:** Differentiating "takes damage at start of turn" from "takes damage at end of turn" creates tactical timing.
- **Implementation notes:**
  - `start_of_turn` ticks run when the actor becomes the next ready actor, before it decides an action.
  - `end_of_turn` ticks run inside `ConsumeEnergy` after the action resolves (current behavior).
  - `none` never ticks damage/healing.
- **Acceptance criteria / tests:**
  - Poison (start) damages before the actor acts; burning (end) damages after acting.
  - Save/load preserves tick timing state.

### STA-3 — Honor status flags (`skip_turn`, `phase_through_walls`, `immune_physical`)

- **Priority:** P0
- **Target files:**
  - `Core/Simulation/StatusEffectProcessor.cs`
  - `Core/Simulation/TurnScheduler.cs`
  - `Core/Simulation/Actions/MoveAction.cs`
  - `Core/Simulation/CombatResolver.cs`
  - `Core/AI/AIBrain.cs`
- **Why it improves the game:** These flags are validated but inert. Making them functional unlocks crowd control, escape tools, and physical-immunity builds.
- **Implementation notes:**
  - Skip turn when `skip_turn` flag is present.
  - Allow walking through walls when `phase_through_walls` is present.
  - Nullify physical damage when `immune_physical` is present.
- **Acceptance criteria / tests:**
  - Stunned enemy skips a turn.
  - Phased player moves through a wall.
  - Immune entity takes zero physical damage.
- **Status:** done. `skip_turn`, `phase_through_walls`, and `immune_physical` are honored in `TurnScheduler`, `MoveAction`, and `CombatResolver` respectively.

---

## 5. AI and Encounters

### AI-1 — Honor authored `ai_params` at runtime

- **Priority:** P0
- **Status:** done. `EnemyTemplate` carries `AIParameters` parsed from `EnemyDefinition.AiParams`; `BrainFactory.Create(EnemyTemplate)` builds a profile from the base brain type plus authored overrides; `AIBrain`, `UtilityScorer`, `AIStateManager`, and `Pathfinder` honor `aggro_range`, `flee_hp_pct`, `wander_when_idle`, `preferred_range`, `min_range`, `patrol_radius`, `support_range`, and `phase_through_walls`.
- **Target files:**
  - `Core/Contracts/Types/EnemyTemplate.cs`
  - `Core/Content/ContentLoader.cs`
  - `Core/AI/BrainFactory.cs`
  - `Core/AI/AIProfiles.cs`
  - `Core/AI/AIBrain.cs`
  - `Core/AI/UtilityScorer.cs`
  - `Core/AI/AIStateManager.cs`
  - `Core/AI/Pathfinder.cs`
  - `Core/Contracts/IPathfinder.cs`
- **Why it improves the game:** `ai_params` (`preferred_range`, `flee_hp_pct`, `support_range`, etc.) now drive `AIProfile`, so content authoring immediately affects behavior.
- **Implementation notes:**
  - `AIParameters` is populated from `EnemyDefinition.AiParams` and validated by `ContentLoader`.
  - `BrainFactory.Create(EnemyTemplate)` starts from the baseline profile and overrides authored fields.
  - `phase_through_walls` is applied as a permanent `Phased` status effect on the entity when the brain first acts.
- **Acceptance criteria / tests:**
  - An enemy authored with `preferred_range: 4` stops 4 tiles away, not at the hardcoded default of 3.
  - Every `ai_params` key in `Content/enemies.json` is recognized by the runtime override parser.
  - Save/load rehydrates the brain from the saved `EnemyComponent.TemplateId` via the content database, so authored `ai_params` survive after loading.
  - A `CharacterOptionsSaveData` DTO was added to `SaveRunSnapshot`/`SaveFileData` (save version 10) so character creation choices round-trip.

### AI-2 — Make ambushers lurk at chokepoints and strike on contact

- **Priority:** P1
- **Status:** done. `AmbushBrain` scores `WaitAction` higher than chase when the player is visible but not adjacent, prefers wall/door-adjacent tiles when idle, and switches aggressively to `AttackAction` when the player steps adjacent.
- **Target files:**
  - `Core/AI/Brains/AmbushBrain.cs`
  - `Core/AI/AIBrain.cs`
  - `Core/AI/UtilityScorer.cs`
  - `Core/AI/AIStateManager.cs`
- **Why it improves the game:** `AmbushBrain` currently waits while the player is out of sight and then chases. True ambush behavior prefers wall/door adjacency and delays attack until the player is adjacent.
- **Implementation notes:**
  - While player is visible but >1 tile away, score `WaitAction` higher than `MoveAction` unless the player is leaving view radius.
  - When player becomes adjacent, switch aggressively to `AttackAction`/melee ability.
  - Prefer tiles adjacent to walls/doors when idle.
- **Acceptance criteria / tests:**
  - An ambusher waits at (3,3) while the player is at (5,3) but attacks when the player steps adjacent.
  - Given two valid tiles, ambusher picks the one with more wall/door neighbors.

### AI-3 — Make ranged enemies respect minimum range and kite

- **Priority:** P1
- **Status:** done. `AIProfile.MinRange` is consumed from authored `min_range`; `RangedKiterBrain`/`UtilityScorer` penalize moves that do not increase distance when inside minimum range, and range comparisons use Chebyshev distance to match ability targeting.
- **Target files:**
  - `Core/AI/UtilityScorer.cs`
  - `Core/AI/AIProfiles.cs`
  - `Core/AI/Brains/RangedKiterBrain.cs`
- **Why it improves the game:** Ranged enemies currently do not penalize being inside minimum range, so they walk into melee.
- **Implementation notes:**
  - Add `MinRange` to `AIProfile` and consume authored `min_range`.
  - Penalize moves that do not increase distance when below `MinRange`.
  - Normalize all range comparisons to Chebyshev to match ability targeting.
- **Acceptance criteria / tests:**
  - A ranged kiter with `min_range: 3` and player at distance 1 chooses a move that increases distance.
  - Ranged kiters never prefer moving closer when already inside `min_range`.

### AI-4 — Make support enemies seek allies and avoid friendly fire

- **Priority:** P1
- **Status:** done. `SupportBrain` selects nearest/most-damaged same-faction ally as a secondary objective, penalizes moves that block allied ranged attackers, and uses `FilterByRelation` to avoid harmful AoE casts that would hit allies unless `hits_allies` is true.
- **Target files:
  - `Core/AI/Brains/SupportBrain.cs`
  - `Core/AI/AIBrain.cs`
  - `Core/AI/UtilityScorer.cs`
  - `Core/Simulation/AbilityResolver.cs`
- **Why it improves the game:** Support enemies currently may buff no one or harm allies with AoEs. Ally-aware positioning makes shamans feel like force multipliers.
- **Implementation notes:**
  - Select nearest same-faction ally as secondary objective when no player is visible; most-damaged ally for healing/buffs.
  - Penalize moves that block allied ranged attackers' line of fire.
  - Use `FilterByRelation` to skip harmful AoE casts that would hit allies unless `hits_allies` is true.
- **Acceptance criteria / tests:**
  - A shaman with an ally in `war_cry` radius casts it; if no ally is in radius, it moves toward the ally.
  - A fireball-casting enemy does not center the blast on a player if an ally is in radius.

### AI-5 — Add group aggro propagation

- **Priority:** P1
- **Status:** done. `AIStateComponent` carries `AlertPosition` and `AlertTurn`; when an enemy enters `Chase`/`Attack`, same-faction allies within `AlertRadius` with no target enter `Chase` toward the alert position; alerts decay after a few turns.
- **Target files:**
  - `Core/AI/AIStateComponent.cs`
  - `Core/AI/AIStateManager.cs`
  - `Core/AI/AIBrain.cs`
- **Why it improves the game:** Enemies acquire targets independently. An orc can stand nearby while the player kills its ally in silence.
- **Implementation notes:**
  - When an enemy transitions to `Chase`/`Attack`, store an `AlertPosition` and `AlertTurn` in `AIStateComponent`.
  - Same-faction allies within `AlertRadius` with no target enter `Chase` toward the alert position.
  - Alert decays after a few turns.
- **Acceptance criteria / tests:**
  - Two enemies in the same room: rear one has no LOS. Once the front one sees the player, the rear one begins moving toward the player within one round.
  - Neutral chests/NPCs do not propagate alerts.

---

## 6. Progression

### PRG-1 — Flatten the early XP curve

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/ProgressionService.cs`
  - `Tests/SimulationTests/ProgressionTests.cs`
- **Why it improves the game:** The current threshold makes the first level-up require ~5–10 early kills. An earlier payoff keeps new players engaged.
- **Implementation notes:**
  - Change `CalculateXpThreshold` so the delta for level 1→2 is ~35 and 2→3 is ~75.
- **Acceptance criteria / tests:**
  - Update `XpThresholdCalculation` to assert the new sequence.
  - Add `FirstLevelUpRequiresReasonableKills`: typical early enemy XP reaches level 2 after 4–6 kills.

### PRG-2 — Make perks build-shaping, not just stat bundles

- **Priority:** P1
- **Target files:**
  - `Content/perks.json`
  - `Core/Contracts/Types/PerkTemplate.cs`
  - `Core/Content/ContentModels.cs`
  - `Core/Content/ContentLoader.cs`
  - `Core/Simulation/ProgressionService.cs`
  - `Core/Simulation/DeathResolver.cs`
- **Why it improves the game:** Current perks are mostly "+1 Attack" or "+6 MaxHP". Verbs like heal-on-kill or bonus-gold make choices memorable.
- **Implementation notes:**
  - Extend `PerkEffect` with trigger types: `heal_on_kill`, `bonus_gold_on_kill`, `status_resist`, `energy_on_kill`.
  - Implement triggers in `DeathResolver` / `StatusEffectProcessor`.
  - Add at least two new perks using the new types.
- **Acceptance criteria / tests:**
  - `HealOnKillPerkRestoresHp`, `GoldOnKillPerkAddsGold`, `StatusResistPerkShortensPoison` pass.
  - `ContentLoader.ValidatePerks` rejects unsupported effect types.

### PRG-3 — Draft three perks per level-up instead of the full list

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/ProgressionComponent.cs`
  - `Core/Simulation/ProgressionService.cs`
  - `Scripts/UI/LevelUpOverlay.cs`
  - `Core/Persistence/SaveSerializer.cs`
- **Why it improves the game:** Offering every unlocked perk at once becomes overwhelming once the pool exceeds ~8 perks.
- **Implementation notes:**
  - Add `PendingPerkChoiceIds` list to `ProgressionComponent` and persist it.
  - Generate the draft deterministically when a level is gained; reuse until spent.
  - `LevelUpOverlay` displays only the drafted perks.
- **Acceptance criteria / tests:**
  - `LevelUpDraftsThreePerks`, `DraftIsDeterministic`, `DraftPersistsAcrossSaveLoad` pass.
  - `LevelUpOverlayListsUnlockedPerks` expects exactly three drafted options.

### PRG-4 — Weight the draft by archetype tags

- **Priority:** P2
- **Target files:**
  - `Content/perks.json`
  - `Core/Contracts/Types/PerkTemplate.cs`
  - `Core/Content/ContentModels.cs`
  - `Core/Simulation/ProgressionService.cs`
  - `Scripts/Autoloads/GameManager.cs`
- **Why it improves the game:** A Mystic and a Vanguard currently see the same perk list. Archetype-biased drafts make creation a long-term choice.
- **Implementation notes:**
  - Add `Tags` to perks (e.g., `warrior`, `rogue`, `mystic`, `general`).
  - `ProgressionService.GetAvailablePerkChoices` accepts the player's archetype and weights matching tags higher.
- **Acceptance criteria / tests:**
  - `VanguardDraftSkewsTowardWarriorPerks`, `MysticDraftSkewsTowardMysticPerks` pass.

### PRG-5 — Auto-apply archetype growth on level-up

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/ProgressionService.cs`
  - `Scripts/Autoloads/GameManager.cs`
  - `Tests/SimulationTests/ProgressionTests.cs`
- **Why it improves the game:** Every character currently gets the same baseline stat growth. Archetype-specific secondary bumps reduce homogenization.
- **Implementation notes:**
  - Store chosen archetype in `ProgressionComponent` or pass via `GameManager`.
  - After baseline bump, apply:
    - Vanguard: +1 Defense
    - Skirmisher: +1 Evasion and +1 Speed
    - Mystic: +1 Accuracy and +1 ViewRadius
- **Acceptance criteria / tests:**
  - `StatsIncreaseOnLevelUp` and `ArchetypeGrowthAppliedOnLevelUp` pass.

---

## 7. Character Creation and Identity

### CHR-1 — Restore `CharacterOptions` when loading a saved run

- **Priority:** P0
- **Status:** done. `SaveRunSnapshot` carries a `CharacterOptionsSaveData` DTO; `SaveSerializer` writes/reads it at save version 10; `SaveMigrator.MigrateV9` supplies defaults for legacy saves; `GameManager` maps the DTO back into `CharacterCreationOptions` during `LoadFromSlot` and serializes it in `CreateSaveRunSnapshot`.
- **Target files:**
  - `Scripts/Autoloads/GameManager.cs`
  - `Core/Persistence/SaveSerializer.cs`
  - `Core/Persistence/SaveMigrator.cs`
  - `Core/Contracts/Types/SaveRunSnapshot.cs`
  - `Core/Contracts/Types/CharacterOptionsSaveData.cs`
  - `Scripts/UI/CharacterSheet.cs`
- **Why it improves the game:** `CharacterOptions` is set at creation but was never restored from saves, so the Character Sheet could lie about the loaded build.
- **Implementation notes:**
  - Added `CharacterOptionsSaveData` in `Core/` to keep the save pipeline free of Godot dependencies.
  - Bumped save version to 10 and added a v9 -> v10 migration that supplies default options.
  - `GameManager.LoadFromSlot` reconstructs `CharacterCreationOptions` from the snapshot before `LoadWorld`.
- **Acceptance criteria / tests:**
  - After round-tripping a run, `gameManager.CharacterOptions.Archetype` and `.RaceId` match the created character.

### CHR-2 — Make race mechanically meaningful with minor bonuses

- **Priority:** P1
- **Target files:**
  - `Scripts/Autoloads/GameManager.cs` (`CreatePlayer`)
  - `Scripts/UI/MainMenu.cs` (`BuildStatPreview`)
  - `Tests/UITests/CharacterUXTests.cs`
  - `Tests/SimulationTests/IdentityTests.cs`
- **Why it improves the game:** Human/Elf/Dwarf/Orc currently only change tint and portrait. Small biases make the choice tactically expressive.
- **Implementation notes:**
  - Add a `RaceBonuses` lookup; apply it inside `CreatePlayer` after archetype/origin/trait bonuses.
  - Update Main Menu stat preview to include race bonuses.
- **Acceptance criteria / tests:**
  - Switching race changes the stat preview.
  - An Orc Vanguard starts with higher attack than a Human Vanguard.
  - A Dwarf has more max HP than a Human with the same build.

### CHR-3 — Give each archetype a signature starting ability

- **Priority:** P1
- **Status:** done. Track 7 character creation now surfaces Vanguard, Ranger, Trickster, and Arcanist; `GameManager.CreatePlayer` attaches archetype starting abilities and archetype identity components from `ArchetypeDefinitions`.
- **Target files:**
  - `Scripts/Autoloads/GameManager.cs` (`CreatePlayer`)
  - `Scripts/UI/MainMenu.cs` preview
  - `Tests/SimulationTests/AbilityTests.cs`
- **Why it improves the game:** Mystic starts with scrolls but no native ability and targeted scrolls currently have no UI path. Signature abilities make archetype identity play out immediately.
- **Implementation notes:**
  - Add `StartingAbilityIds` to `CharacterCreationOptions` and archetype records.
  - In `CreatePlayer`, attach an `AbilitiesComponent` populated with archetype abilities.
  - Show signature ability name in Main Menu preview.
- **Acceptance criteria / tests:**
  - A Mystic player can cast `fireball` immediately without consuming a scroll.
  - A Vanguard can cast `heavy_slam`.
  - Save/load preserves `AbilitiesComponent` and cooldowns.

### CHR-4 — Improve creation preview clarity

- **Status:** done. Main menu and help now state exact training effects, starter kit preview uses `Equipped:` / `Pack:` grouping, and aimed starter scrolls show a targeting note.
- **Priority:** P1
- **Target files:**
  - `Scripts/UI/MainMenu.cs`
  - `Scripts/UI/HelpOverlay.cs`
  - `Tests/UITests/CharacterUXTests.cs`
- **Why it improves the game:** The menu says training raises stats but does not say exactly how; starting item IDs are shown instead of descriptions.
- **Implementation notes:**
  - Label each training stat with exact effect (e.g., "Vitality (+3 Max HP)", "Finesse (+1 ACC / +1 EVA)").
  - Split starting kit into "Equipped" and "Pack" with slot hints.
  - Add a note that aimed scrolls require targeting.
- **Acceptance criteria / tests:**
  - `StatPreviewMatchesExpected` still passes.
  - Preview contains "Equipped:" and correct training-stat labels.

### CHR-5 — Add a "Randomize Build" option

- **Priority:** P2
- **Target files:**
  - `Scripts/UI/MainMenu.cs`
  - `Tests/UITests/CharacterUXTests.cs`
- **Why it improves the game:** One-button variety improves replayability and helps players discover unexpected combinations.
- **Implementation notes:**
  - Add a menu entry or shortcut (e.g., `R`) that randomizes name, archetype, origin, trait, race, gender, appearance, and training allocation.
  - Keep seeding deterministic enough for replays if desired.
- **Acceptance criteria / tests:**
  - After invoking randomize, build differs from previous in at least race or archetype.
  - Multiple randomizes produce at least two distinct builds.

---

## 8. UI / UX

### UI-1 — Add mouse interaction to the inventory grid

- **Priority:** P1
- **Target files:**
  - `Scripts/UI/InventoryUI.cs`
  - `Scripts/UI/OverlayLayoutHelper.cs`
- **Why it improves the game:** Inventory is keyboard-only. Mouse support makes management faster and more accessible.
- **Implementation notes:**
  - Left-click selects a slot; double-click activates (use/equip).
  - Right-click drops one from stack (full stack with Shift).
- **Acceptance criteria / tests:**
  - `UI.Inventory mouse click selects a slot`
  - `UI.Inventory double-click emits use action`
  - `UI.Inventory right-click emits drop action`

### UI-2 — Add message categories and filtering to combat log

- **Priority:** P1
- **Status:** done. Message categories, category colors, critical emphasis, age fading, visible filter state, and filter cycling are implemented via `LogCategory`, `EventBus.LogMessage`, and `CombatLog` tests.
- **Target files:**
  - `Scripts/UI/CombatLog.cs`
  - `Scripts/Autoloads/EventBus.cs`
- **Why it improves the game:** Dense combat produces a wall of colored text. Categories and filters help players focus.
- **Implementation notes:**
  - Introduce `LogCategory` enum (Combat, Item, Status, System) and prefix messages with short tags (`[Dmg]`, `[Loot]`, `[Fx]`, `[Sys]`).
  - Add hotkeys to toggle categories.
- **Acceptance criteria / tests:**
  - Combat messages are prefixed with `[Dmg]`.
  - Filtering out item messages hides them.
  - Category prefixes survive BBCode escaping.

### UI-3 — Make status effects visible on HUD and entity sprites

- **Priority:** P1
- **Status:** done. Implemented under STA-4: HUD renders a status badge row and `EntityRenderer` attaches status icon children to entity sprites; `StatusEffectApplied` and `StatusEffectRemoved` events drive updates.
- **Target files:**
  - `Scripts/UI/HUD.cs`
  - `Scripts/World/EntityRenderer.cs`
  - `Scripts/Autoloads/EventBus.cs`
- **Why it improves the game:** Status effects are currently plain text only. Icons and tints make battlefield state readable.
- **Implementation notes:**
  - Add a status badge row to HUD using `TextureRect` or glyph labels.
  - Attach status icon children to entity sprites in `EntityRenderer`.
  - Subscribe to `StatusEffectApplied` and `StatusEffectRemoved` (STA-5).
- **Acceptance criteria / tests:**
  - HUD renders status effect badges.
  - `EntityRenderer` adds status badge node for affected entities.

### UI-4 — Standardize overlay close keys and inline hotkey hints

- **Priority:** P1
- **Status:** done in Block E4; F3 follow-up includes FloorSummaryUI Escape dismissal.
- **Target files:**
  - `Scripts/UI/UIRoot.cs`
  - `Scripts/UI/MenuBase.cs`
  - `Scripts/UI/InputHandler.cs`
- **Why it improves the game:** Close behavior is inconsistent; `E` and `F` both map to interact. Standardization reduces cognitive load.
- **Implementation notes:**
  - `Esc` closes every overlay (inventory, sheet, help, pause, level up, dialog, shop, floor summary).
  - Remove duplicate `E => InteractRequested` binding; keep only `F`.
  - Add footer hints consistently to modal overlay chrome.
- **Acceptance criteria / tests:**
  - `UI.UIRoot escape closes every overlay`
  - `UI.InputHandler does not bind E for interact`

### UI-5 — Add colorblind-safe rarity and HP indicators

- **Status:** done. Item rarity now has centralized `[C]`/`[U]`/`[R]`/`[E]`/`[L]`/`[A]` presentation helpers, inventory slots/descriptions/tooltips/combat-log pickups/shop rows include non-color rarity markers while preserving rarity colors, and the HUD shows a striped low-HP pattern plus `HPBarDangerPatternVisible` below the existing `< 0.3` danger threshold. GFX-1 also adds a timed bright/white damage flash so hit feedback is visible without relying on red-only tinting.
- **Priority:** P1
- **Target files:**
  - `Scripts/UI/ItemRarityPresentation.cs`
  - `Scripts/UI/HUD.cs`
  - `Scripts/UI/UiStyle.cs`
  - `Scripts/World/AnimationController.cs`
- **Why it improves the game:** Rarity and HP are color-only. Players with color vision deficiency may miss important information.
- **Implementation notes:**
  - Append rarity abbreviation to item names: `[R] Scroll of Fireball`.
  - Add bracket styling per tier.
  - Add texture fill or stripe pattern to HP bar so low HP is identifiable by shape.
  - Make damage flash a brief white/bright invert instead of only red.
- **Acceptance criteria / tests:**
  - Inventory rarity labels include non-color abbreviation.
  - HUD low-HP bar uses striped fill.

---

## 9. Inventory Management

### INV-1 — Add category/rarity filters to inventory

- **Priority:** P1
- **Target files:**
  - `Scripts/UI/InventoryUI.cs`
  - `Tests/UITests/UISmokeTests.cs`
- **Why it improves the game:** Once the bag fills with mixed loot, finding a potion or a newly picked-up ring requires manual paging.
- **Implementation notes:**
  - Add `FilterMode` enum (`All`, `Equipment`, `Consumables`, `Scrolls`, `Upgrades`) and hotkey (e.g., `F`).
  - Filter the visible subset before rendering; keep source list unchanged.
- **Acceptance criteria / tests:**
  - Filter shows only expected category glyphs and footer reads `Filter: Equipment`.

### INV-2 — Add bulk use/drop for stacks

- **Priority:** P1
- **Target files:**
  - `Scripts/UI/InventoryUI.cs`
  - `Scripts/UI/UIActionFactory.cs`
  - `Core/Simulation/Actions/UseItemAction.cs`
  - `Core/Simulation/Actions/DropItemAction.cs`
- **Why it improves the game:** Using or dropping a large stack one-at-a-time is tedious.
- **Implementation notes:**
  - `Shift+D` drops full selected stack.
  - `Shift+U` consumes up to N identical non-targeted consumables until full HP or stack exhausted.
- **Acceptance criteria / tests:**
  - A stack of 5 health potions with actor at 4/40 HP leaves 4 potions after bulk use.
  - `Shift+D` emits a `DropItemAction` with `Quantity = int.MaxValue`.

### INV-3 — Add explicit stack-split quantity prompt

- **Priority:** P1
- **Target files:**
  - `Scripts/UI/InventoryUI.cs`
  - `Tests/UITests/UISmokeTests.cs`
- **Why it improves the game:** UI currently only offers drop-one or drop-all. A quantity prompt lets players leave a precise remainder.
- **Implementation notes:**
  - Add inline numeric entry mode triggered by `S` for split.
  - Digits build quantity; `Enter` confirms; `Esc` cancels.
  - Validate quantity between 1 and `StackCount - 1`.
- **Acceptance criteria / tests:**
  - On a stack of 10 rocks, press `S`, type `3`, press `Enter`; emitted `DropItemAction.Quantity` equals 3 and remaining stack is 7.

### INV-4 — Render and highlight ground items in world view

- **Priority:** P1
- **Target files:**
  - `Scripts/World/WorldView.cs`
  - `Scripts/World/EntityRenderer.cs` or new `Scripts/World/GroundItemRenderer.cs`
  - `Scripts/Autoloads/EventBus.cs`
- **Why it improves the game:** `WorldState` tracks ground items but `WorldView` never renders them. Players have no visual indication that loot is on the floor.
- **Implementation notes:**
  - Add `_groundItemLayer` under `WorldView` drawing small sprites/labels per ground item.
  - Highlight lootable tiles when the player is adjacent or standing on them.
  - Hide under fog-of-war.
- **Acceptance criteria / tests:**
  - Drop an item, advance one turn, assert world view contains a child node for the ground item at the expected position.

### INV-5 — Add pickup radius for adjacent loot

- **Priority:** P1
- **Target files:**
  - `Core/Simulation/Actions/PickupAction.cs`
  - `Scripts/UI/UIActionFactory.cs`
  - `Tests/SimulationTests/ActionTests.cs`
- **Why it improves the game:** `PickupAction` only collects from the actor's current tile. After a chest opening or group kill, the player must walk onto every item.
- **Implementation notes:**
  - Extend `PickupAction` with optional `int Radius` (default 0).
  - Iterate Chebyshev neighbors in order of distance, picking up until full or no items remain.
  - `Shift+G` requests radius pickup.
- **Acceptance criteria / tests:**
  - Place three rocks within radius 1; `PickupAction` with radius 1 collects all three.
  - Returns `Blocked` if inventory cannot accept any item in range.

---

## 10. Save / Load / Determinism

### SAV-1 — Persist enemy template identity

- **Priority:** P0
- **Target files:**
  - `Core/Simulation/EnemyComponent.cs` (new)
  - `Scripts/Autoloads/GameManager.cs` (`CreateEnemyEntity`)
  - `Core/Persistence/SaveSerializer.cs`
  - `Core/Persistence/SaveValidator.cs`
- **Why it improves the game:** Enemies save stats/brain but not their source `EnemyTemplate.TemplateId`. Content changes can silently alter loaded enemies.
- **Implementation notes:**
  - Add `EnemyComponent { TemplateId }`.
  - Add `EnemyTemplateId` to `EntitySaveData`; serialize and restore.
  - If template no longer exists on load, keep saved stats/brain and log a warning.
- **Acceptance criteria / tests:**
  - Round-trip a fixed-entity enemy and assert `TemplateId == "goblin_archer"`.
  - Loading a save with a removed enemy template still succeeds.
- **Status:** done. Added `EnemyComponent` with `TemplateId`, serialized in v9 save data, restored on load, and validated.

### SAV-2 — Persist `TurnScheduler` actor order

- **Priority:** P0
- **Target files:**
  - `Core/Simulation/TurnScheduler.cs`
  - `Core/Persistence/SaveSerializer.cs`
  - `Core/Persistence/SaveValidator.cs`
  - `Scripts/Autoloads/GameManager.cs` (`LoadFromSlot`, `RegisterWorldEntities`)
- **Why it improves the game:** `TurnScheduler` breaks energy ties with `Order`. On load, actors are registered in `world.Entities` order, changing combat outcomes and RNG consumption.
- **Implementation notes:**
  - Add `GetOrder`/`SetOrder` accessors or persist order via per-actor field.
  - Add `SchedulerOrder` to `EntitySaveData` and `SchedulerNextOrder` to floor data.
  - On load, register actors in ascending order and restore `_nextOrder`.
- **Acceptance criteria / tests:**
  - Two actors with identical energy save/load and the same actor is returned by `GetNextActor`.
  - Replay test proves 10-turn sequence matches before/after save/load.
- **Status:** done. Per-actor `SchedulerOrder` and floor-level `SchedulerNextOrder` persist via `WorldState` staging and are restored during `RegisterWorldEntities`.

### SAV-3 — Add deterministic replay regression tests

- **Priority:** P1
- **Target files:**
  - `Tests/PersistenceTests/DeterminismTests.cs` (new)
- **Why it improves the game:** Current persistence tests compare static signatures. Replay tests prove the same action sequence yields the same results after save/load.
- **Implementation notes:**
  - Create helper that runs a fixed action sequence on a seeded world to produce a canonical trace.
  - Re-run with save/load at turns 3, 7, and after floor descent; compare traces.
- **Acceptance criteria / tests:**
  - `Determinism.Replay survives save/load every turn` passes.
  - `Determinism.Multi-floor travel is replay-stable` passes.
- **Status:** done. Added `Tests/PersistenceTests/DeterminismTests.cs` with fixed action-sequence replay tests that compare traces before and after save/load.

### SAV-4 — Harden RNG rehydration order

- **Priority:** P1
- **Target files:**
  - `Core/Contracts/WorldState.cs`
  - `Core/Persistence/SaveSerializer.cs`
- **Why it improves the game:** Setting `world.Seed` recreates `CombatResolver` and resets item RNG before later overwriting with saved states. Any future code reading RNG between those steps silently diverges.
- **Implementation notes:**
  - Add internal `RehydrateRandomStates(int seed, ulong combatState, ulong itemState)` that sets everything atomically.
  - Change `SaveSerializer.ToWorldState` to call it instead of public `Seed` + properties.
- **Acceptance criteria / tests:**
  - Existing RNG continuation tests still pass.
  - New test: peek at next 20 RNG outputs, load, and assert identical outputs without transient calls.
- **Status:** done. Added `WorldState.RehydrateRandomStates`; `SaveSerializer.ToWorldState` now sets seed and both RNG states atomically.

### SAV-5 — Make autosave automatic and crash-safe

- **Priority:** P1
- **Target files:**
  - `Scripts/Autoloads/GameManager.cs`
  - `Core/Persistence/SaveManager.cs`
  - `Scripts/Autoloads/EventBus.cs`
- **Why it improves the game:** Slot 0 is reserved for autosave but nothing writes to it automatically. A crash loses the entire run.
- **Implementation notes:**
  - Autosave after successful player turns and on floor transitions, throttled to every N turns.
  - Before overwriting `autosave.json`, copy existing valid file to `autosave.json.bak`.
- **Acceptance criteria / tests:**
  - `autosave.json` exists after `ProcessPlayerAction`.
  - `.bak` rotation works.
  - Corrupted temp file falls back to `.bak` successfully.

---

## 11. Content Authoring Tools

### TOL-1 — Add file-watcher-based content validation

- **Priority:** P1
- **Target files:**
  - `Scripts/Tools/DevToolsWorkbench.cs`
  - New helper `Scripts/Tools/ContentFileWatcher.cs`
  - `Tests/UITests/ToolingTests.cs`
- **Why it improves the game:** Authors must manually remember to validate after JSON edits. A watcher surfaces typos and broken cross-references within seconds.
- **Implementation notes:**
  - Watch `Content/*.json` while workbench is open.
  - Refresh status line with validation results on change.
- **Acceptance criteria / tests:**
  - Saving a malformed `items.json` updates status line with error count and first error.
  - Clean file shows validated definition count and content hash.

### TOL-2 — Run full `ContentLoader` validation before tool save

- **Priority:** P1
- **Target files:**
  - `Scripts/Tools/ItemEditor.cs`
  - `Scripts/Tools/MapEditor.cs`
  - `Core/Content/ContentLoader.cs`
  - `Tests/UITests/ToolingTests.cs`
- **Why it improves the game:** `ItemEditor` and `MapEditor` currently check only local structural rules, missing cross-reference errors.
- **Implementation notes:**
  - Reuse `ContentLoader.Validate` for the current `items.json` / `enemies.json` / room prefab set.
  - Allow save with warnings but distinguish "saved with N warnings" from "saved cleanly".
- **Acceptance criteria / tests:**
  - An item with `cast_ability: unknown_ability` is flagged both by `ContentLoader` and `ItemEditor.ValidateAll`.

### TOL-3 — Promote `res://` asset-path validation to loader errors

- **Priority:** P1
- **Target files:**
  - `Core/Content/ContentLoader.cs`
  - `Tests/ContentTests/ContentValidationTests.cs`
- **Why it improves the game:** Authored art paths are checked only by tests. Moving the check into `ContentLoader` means runtime workbench, editor plugin, and CI all report missing assets immediately.
- **Implementation notes:**
  - Report validation error for every `item.SpritePath`, `enemy.SpritePath`, and `status.IconPath` that does not resolve to an existing file.
  - Error message includes id, offending path, and `res://` hint.
- **Acceptance criteria / tests:**
  - Valid content still loads without errors.
  - `ContentValidationTests.AuthoredArtPathsResolveToCommittedFiles` continues to pass.

### TOL-4 — Expand item authoring to cover effects, requirements, sprite, description

- **Priority:** P1
- **Target files:**
  - `Scripts/Tools/ItemEditor.cs`
  - `Scripts/Tools/DevToolsWorkbench.cs`
- **Why it improves the game:** The workbench Items tab only edits type, slot, rarity, attack/defense, value, and stack. Description, sprite path, requirements, and effects must be hand-edited in JSON.
- **Implementation notes:**
  - Expose editable fields for description, sprite path, one `on_use` effect, and level requirement.
  - Validate that `cast_ability` references a known ability.
- **Acceptance criteria / tests:**
  - A drafted item with a `heal` effect persists its effect after save/load.
  - Consumables cannot select an invalid slot.

### TOL-5 — Add room metadata editing to the Rooms tab

- **Priority:** P1
- **Target files:**
  - `Scripts/Tools/DevToolsWorkbench.cs`
  - `Scripts/Tools/MapEditor.cs`
- **Why it improves the game:** The Rooms tab never exposes `RoomName`, `TagsText`, `MinDepth`, or `MaxDepth`. Authors must hand-edit JSON after painting geometry.
- **Implementation notes:**
  - Add editable rows for `Name`, `Tags`, `Min depth`, `Max depth`.
  - Persist metadata with the prefab and reload it.
- **Acceptance criteria / tests:**
  - `MapEditorRoundTripsPrefabs` asserts that `RoomName`, `TagsText`, `MinDepth`, and `MaxDepth` survive round trip.

---

## 12. Dungeon Generation

### GEN-1 — Implement authored traps and hazard tiles

- **Priority:** P0
- **Status:** done. `TileType.Trap` exists and is walkable; `^` in room prefabs maps to `TileType.Trap`; `LevelData` carries `TrapSpawnDetails`; `DungeonGenerator` collects trap tiles and explicit `trap_id` spawn points, marks them occupied before enemy/item placement, and `LevelValidator` confirms reachability. `GameManager.PopulateWorld` spawns trap entities from `TrapSpawnDetails`. `MoveAction` calls `HazardProcessor.OnEntityEnteredTile` after every successful position change (normal move, phased move, NPC swap). `HazardProcessor` resolves the trap template, skips actors with configured avoid flags (`phased`, `flying`), rolls damage/status with `CombatResolver`, applies damage, routes kills through `DeathResolver.ResolveUnattributedDeath`, appends `CombatEvent`s/log messages, disarms/reveals the trap, and raises `HazardProcessor.TrapTriggered`; `GameManager` forwards this to `EventBus.TrapTriggered`. Trap persistence was introduced in save version `12`; `TrapComponent` is the single source of truth for trap state and is serialized as part of the owning entity; legacy v10/v11 standalone trap arrays are migrated into trap entities by `SaveMigrator`, and `SaveValidator` rejects trap entities on non-Trap tiles or with missing template ids. The current normalized save version has since advanced.
- **Target files:**
  - `Core/Generation/RoomPrefab.cs`
  - `Core/Generation/DungeonGenerator.cs`
  - `Core/Contracts/Types/LevelData.cs`
  - `Core/Contracts/Types/LevelSpawnDetails.cs`
  - `Core/Contracts/Types/Enums.cs`
  - `Core/Contracts/TrapState.cs` (now `LegacyTrapState.cs`, used only for v10/v11 migration)
  - `Core/Contracts/Types/TrapTemplate.cs`
  - `Core/Simulation/Components/TrapComponent.cs`
  - `Core/Simulation/HazardProcessor.cs`
  - `Core/Simulation/Actions/MoveAction.cs`
  - `Core/Content/ContentLoader.cs`
  - `Core/Contracts/IContentDatabase.cs`
  - `Content/traps.json`
  - `Content/abilities.json`
  - `Content/room_prefabs.json`
  - `Scripts/Autoloads/GameManager.cs`
  - `Scripts/Autoloads/EventBus.cs`
  - `Scripts/World/WorldView.cs`
  - `Scripts/World/WorldArtCatalog.cs`
  - `Scripts/UI/Minimap.cs`
  - `Core/Persistence/SaveSerializer.cs`
  - `Core/Persistence/SaveMigrator.cs`
  - `Core/Persistence/SaveValidator.cs`
- **Why it improves the game:** Traps drawn in the tile legend (`^`) and referenced by `flooded_passage` now actually trigger when entities enter them, adding environmental hazard gameplay.
- **Implementation notes:**
  - Traps are entities with `TrapComponent` (`BlocksMovement=false`) placed on `TileType.Trap` tiles.
  - `HazardProcessor` uses `world.CombatResolver` for deterministic damage rolls; kills are unattributed (no XP/kill credit), matching environmental hazard design.
  - Trap definitions live in `Content/traps.json` and reference an optional ability in `Content/abilities.json` plus direct damage/status fields.
  - `ContentLoader` validates that every room `trap_id` resolves to a known trap definition and that trap art paths exist.
- **Acceptance criteria / tests:**
  - `RoomPrefab.GetTileType('^')` returns `Trap`.
  - `DungeonGeneratorTests` trap spawn/reachability/overlap tests pass.
  - `HazardProcessorTests` cover move trigger, deterministic damage, unattributed kill, phased/flying avoidance, NPC swap, disarmed trap, and status application.
  - `SaveManagerTests` round-trip trap tiles and state; `MigrationTests` migrate v10 to v11; `ValidationTests` reject traps on non-trap tiles.
  - `WorldView`/`Minimap`/`WorldArtCatalog` rendering tests cover trap tiles and markers.

### GEN-2 — Add themed floor sets via prefab tag filtering

- **Priority:** P1
- **Status:** done. `RoomPrefab.Tags` are projected from content; `DungeonGenerator` maps depth to theme (`prison` 1–3, `crypt` 4–6, `magma` 7+) and prefers theme-matching prefabs when at least four fit the BSP leaves, falling back to all valid prefabs otherwise. New themed rooms were added to `Content/room_prefabs.json`.
- **Target files:**
  - `Core/Generation/DungeonGenerator.cs`
  - `Content/room_prefabs.json`
  - `Core/Content/ContentLoader.cs`
- **Why it improves the game:** Prefabs are currently chosen uniformly from all fitting rooms. Themes make consecutive rooms cohesive and give each depth a visual identity.
- **Implementation notes:**
  - Define 3–4 themes mapped to depth ranges (e.g., 1–3 prison, 4–6 crypt, 7+ magma).
  - Filter prefabs by theme tags; fall back to all fitting prefabs if coverage drops below a threshold.
- **Acceptance criteria / tests:**
  - `DungeonGeneratorTests.FloorThemeConstrainsPrefabSelection` passes.
  - Determinism tests still pass.

### GEN-3 — Guarantee one landmark special room per floor

- **Priority:** P1
- **Target files:**
  - `Core/Generation/DungeonGenerator.cs`
  - `Core/Generation/RoomPrefab.cs` / `RoomPrefabLibrary.cs`
  - `Content/room_prefabs.json`
- **Why it improves the game:** Landmarks give the player a meaningful destination and create memorable "I found the vault" moments.
- **Implementation notes:**
  - Reserve one BSP leaf for a landmark prefab, placed farthest from start or behind a side branch.
  - Mark `treasure_room`, `library_room`, `vault_room`, `arena_room` as `landmark`.
- **Acceptance criteria / tests:**
  - Every generated floor contains exactly one prefab tagged `landmark`.
  - Landmark is not the start room.

### GEN-4 — Implement locked doors and key placement

- **Priority:** P1
- **Status:** done. Added `TileType.LockedDoor`; `DungeonGenerator.PlaceLockedDoorsAndKeys` converts selected connecting doors to locked doors and places a `dungeon_key` item in a reachable room. `OpenDoorAction` validates and consumes one key from the actor's inventory to unlock the door permanently.
- **Target files:**
  - `Core/Contracts/Types/Enums.cs` / `TileType`
  - `Core/Simulation/Entity.cs` or new `DoorComponent.cs`
  - `Core/Generation/DungeonGenerator.cs`
  - `Scripts/Autoloads/GameManager.cs`
  - `Content/items.json`
  - `Core/Simulation/Actions/OpenDoorAction.cs`
- **Why it improves the game:** Prefabs already declare `lock_doors_on_enter`, but the flag is dead. Locking arenas/vaults and hiding keys creates risk/reward loops.
- **Implementation notes:**
  - Add locked state to doors.
  - When a prefab has `lock_doors_on_enter: true` or is a landmark, mark connecting doors locked and place a key in a reachable non-locked room.
  - Add generic `key` item; `OpenDoorAction` consumes one key for locked doors.
- **Acceptance criteria / tests:**
  - A locked room's doors are initially impassable.
  - A key exists somewhere reachable before the locked door.
  - Opening a locked door consumes one key and unlocks it permanently.

### GEN-5 — Clean up and expand the prefab library

- **Priority:** P1
- **Target files:**
  - `Content/room_prefabs.json`
  - `Core/Generation/Prefabs/RoomPrefabLibrary.cs`
  - `Tests/ContentTests/ContentValidationTests.cs`
- **Why it improves the game:** `custom_room_1` (20×20) and `custom_room_2` (22×21) rarely fit inside a 60×40 BSP leaf, so authored content is wasted.
- **Implementation notes:**
  - Remove or resize oversized prefabs.
  - Add 6–10 small/medium prefabs (4×4 to 11×11).
  - Add fallback prefabs in `RoomPrefabLibrary`.
- **Acceptance criteria / tests:**
  - No prefab exceeds the maximum realistic leaf size for depth 1.
  - `ContentValidationTests.AllPrefabsArePlaceable` passes.

---

## 13. Onboarding

### ONB-1 — Add a "First Delve" welcome message to combat log

- **Priority:** P1
- **Target files:**
  - `Scripts/Autoloads/GameManager.cs`
  - `Scripts/UI/CombatLog.cs`
- **Why it improves the game:** Players spawn with no stated goal or control reminder.
- **Implementation notes:**
  - After `LoadWorld` in `StartNewGame`, emit 2–3 lines explaining objective and keys.
  - Do not re-emit on load.
- **Acceptance criteria / tests:**
  - `CombatLog.Messages` contains welcome text after new game starts.
  - Loading a saved game does not re-emit the welcome.

### ONB-2 — Show an objective hint when down-stairs become visible

- **Priority:** P1
- **Target files:**
  - `Core/Contracts/WorldState.cs`
  - `Scripts/Autoloads/GameManager.cs`
  - `Scripts/UI/CombatLog.cs`
- **Why it improves the game:** New players wander aimlessly. A contextual cue when `>` is first seen reduces friction.
- **Implementation notes:**
  - Track per-floor `HasSeenStairsDown` flag.
  - When visibility reveals stairs-down for the first time, emit a hint.
- **Acceptance criteria / tests:**
  - Revealing stairs-down logs the hint exactly once per floor.
  - Save/load preserves the flag.

### ONB-3 — Improve game-over screen with death-specific learning tips

- **Priority:** P1
- **Target files:**
  - `Scripts/UI/GameOverScreen.cs`
  - `Scripts/UI/UIRoot.cs`
- **Why it improves the game:** First death is the best teaching moment; the current screen only shows floor/enemies/turns.
- **Implementation notes:**
  - Add contextual tip based on run summary:
    - Died on floor 0 → explore carefully, use wait.
    - Died with health potions → use them earlier.
    - Few enemies killed → avoid fighting groups.
- **Acceptance criteria / tests:**
  - `GameOverScreen.BuildBodyText()` includes a tip inferred from `GameOverSummary`.
  - Unit test verifies each rule with mocked summaries.

### ONB-4 — Surface starter-kit item effects in main menu preview

- **Status:** done. Main-menu starter kits use content display names, descriptions, stack counts, Equipped/Pack grouping, targeting notes, and a `Tab` shortcut tooltip.
- **Priority:** P1
- **Target files:**
  - `Scripts/UI/MainMenu.cs`
  - `Scripts/UI/Tooltip.cs`
- **Why it improves the game:** Preview shows raw IDs like `sword_iron` and `scroll_blink`; new players don't know what they do.
- **Implementation notes:**
  - Replace IDs with display names + one-line descriptions.
  - Add a low-risk keyboard tooltip using `Tab` in the main menu.
- **Acceptance criteria / tests:**
  - `BuildStarterKitPreviewText()` and the `Tab` tooltip contain display names and short descriptions for starting items.
  - Content-driven test fails if a starting item template is unknown or lacks a description.

### ONB-5 — Add a temporary key-reminder ribbon to HUD

- **Priority:** P1
- **Target files:**
  - `Scripts/UI/HUD.cs`
- **Why it improves the game:** New players forget `H` for Help and `I` for Inventory.
- **Implementation notes:**
  - Add `_hintLabel` below minimap showing keys for first 15 turns, then remove it.
- **Acceptance criteria / tests:**
  - `HUD.Snapshot()` includes hint line when `world.TurnNumber <= 15`.
  - Hint disappears after turn 16.

---

## 14. Game Feel (Audio / Visual)

### GFX-1 — Make hit flashes readable and layered

- **Status:** done in F4. `AnimationController` keeps a timed bright/white flash state for damaged entities, restores sprites to exact white after ~0.12s, and `EntityRenderer.UpsertEntity` preserves active flashes during refresh.
- **Priority:** P1
- **Target files:**
  - `Scripts/World/AnimationController.cs`
  - `Scripts/World/EntityRenderer.cs`
- **Why it improves the game:** `AnimateDamage` currently sets `Modulate = Red` then immediately `White`, so the flash is invisible.
- **Implementation notes:**
  - Store a flash timer and interpolate `Modulate` over ~0.12s.
  - `Advance` drives the flash; `UpsertEntity` should not clobber an active flash.
- **Acceptance criteria / tests:**
  - After `DamageDealt`, defender sprite's `Modulate` is not pure white for the first 0.05s.

### GFX-2 — Add lightweight camera shake on impactful hits

- **Priority:** P1
- **Target files:**
  - `Scripts/World/CameraController.cs`
  - `Scripts/World/WorldView.cs`
- **Why it improves the game:** Screen shake sells heavy blows and criticals.
- **Implementation notes:**
  - Add `CameraController.AddTrauma(float amount)` with decay.
  - Offset `Camera2D.Position` by `shake = maxOffset * trauma^2 * randomUnitVector` each `_Process`.
  - Trigger on player damage (high), player crit (medium), heavy ability hits.
- **Acceptance criteria / tests:**
  - `WorldView._Process` applies non-zero camera offset after trauma is injected.

### GFX-3 — Polish damage popups

- **Priority:** P1
- **Target files:**
  - `Scripts/World/DamagePopup.cs`
  - `Scenes/UI/DamagePopup.tscn`
  - `Scripts/World/AnimationController.cs`
- **Why it improves the game:** Popups are plain labels. Crits should pop; heals green; misses gray.
- **Implementation notes:**
  - Add `LabelSettings` or custom font; crit = larger + gold/orange, heal = green, normal = parchment/white, miss = gray.
  - Animate scale with overshoot on crit.
- **Acceptance criteria / tests:**
  - Spawn a crit popup and assert its `Scale` is > 1 at midpoint.

### GFX-4 — Wire ability/item `animation` and `sfx` fields

- **Priority:** P1
- **Target files:**
  - `Scripts/Autoloads/SoundManager.cs` (new)
  - `Scripts/Autoloads/VfxCatalog.cs` (new)
  - `Scripts/World/WorldView.cs`
  - `Scripts/Autoloads/GameManager.cs`
  - `Core/Content/ContentModels.cs`
- **Why it improves the game:** Abilities declare `animation` and `sfx`, but nothing reads them.
- **Implementation notes:**
  - Add `SoundManager` autoload mapping `ability.Sfx` to `res://Assets/Audio/Sfx/{sfx}.ogg`/`.wav`.
  - Add `VfxCatalog` mapping `ability.Animation` to particle/color-flash presets.
  - Play matching SFX/VFX when `DamageDealt` originates from `CastAbility`.
- **Acceptance criteria / tests:**
  - Content validation test asserts every `animation`/`sfx` value has a matching resource or is in an allowed placeholder set.

### GFX-5 — Add projectile travel for ranged abilities and attacks

- **Priority:** P2
- **Target files:**
  - `Scripts/World/AnimationController.cs`
  - `Scripts/World/WorldView.cs`
  - `Scripts/Autoloads/VfxCatalog.cs`
- **Why it improves the game:** Ranged attacks currently use the same "bump toward target" animation as melee.
- **Implementation notes:**
  - Add `AnimationController.SpawnProjectile(from, to, animationKey, speed)`.
  - Create `Projectile` node that follows a line/arc and spawns impact preset on arrival.
- **Acceptance criteria / tests:**
  - Firing `arrow_shot` adds a projectile node that moves from attacker to defender over multiple frames.

---

## 15. Performance

### PERF-1 — Make `WorldView._Process` event-driven

- **Priority:** P1
- **Target files:**
  - `Scripts/World/WorldView.cs`
  - `Scripts/World/EntityRenderer.cs`
- **Why it improves the game:** `_Process` currently scans all entities every frame even when idle.
- **Implementation notes:**
  - Remove `ReconcileEntityPositions` from `_Process`.
  - Keep it in `OnTurnCompleted` and when `AnimationController.Advance` finishes.
  - Optionally add a dirty flag set by simulation events.
- **Acceptance criteria / tests:**
  - 100 `_Process(0.016)` calls with no active world/animations perform zero entity iteration.

### PERF-2 — Avoid full-grid fog iteration every turn

- **Priority:** P1
- **Target files:**
  - `Scripts/World/WorldView.cs`
  - `Core/Contracts/WorldState.cs` or `Scripts/Autoloads/GameManager.cs`
- **Why it improves the game:** Fog updates scan the entire `Width × Height` grid every turn.
- **Implementation notes:**
  - Track per-turn dirty visibility/exploration changes in `WorldState` or `GameManager`.
  - Apply only deltas; keep full refresh for initial bind/floor change.
- **Acceptance criteria / tests:**
  - After a step, `WorldView` updates no more fog cells than twice the number of changed visibility tiles.

### PERF-3 — Replace `EntityRenderer` child-name string lookups

- **Priority:** P1
- **Target files:**
  - `Scripts/World/EntityRenderer.cs`
- **Why it improves the game:** `ApplyAppearance` repeatedly calls `FindChild`/`GetOrCreateChild`, each walking `GetChildren()` and comparing names.
- **Implementation notes:**
  - Cache known children in a small struct/dictionary per sprite root.
  - Mutate existing `Sprite2D`/`ColorRect` directly instead of removing/recreating.
- **Acceptance criteria / tests:**
  - 100 `UpsertEntity` calls on the same entity do not grow `spriteRoot.GetChildCount()`.

### PERF-4 — Pool/reuse per-tile art nodes

- **Priority:** P1
- **Target files:**
  - `Scripts/World/WorldView.cs`
  - `Scripts/World/WorldArtCatalog.cs`
- **Why it improves the game:** `RenderTileArt` allocates new nodes per tile; door open/close triggers repeated rebuilds.
- **Implementation notes:**
  - Reuse tile container `Node2D` and child `Sprite2D`/`ColorRect`; only change texture/color/visibility.
  - Pool wall-cover nodes and atlas strip textures.
- **Acceptance criteria / tests:**
  - Opening/closing the same door 50 times leaves at most one container node per tile position.
  - `CreateFrontWallStripTexture` returns the same `AtlasTexture` instance for repeated calls.

### PERF-5 — Cache `WorldState.GetEntitiesInRadius`

- **Priority:** P1
- **Target files:**
  - `Core/Contracts/WorldState.cs`
- **Why it improves the game:** The method currently uses LINQ `.Where().OrderBy().ThenBy().ThenBy().ToArray()`, allocating and sorting on every AI/ability call.
- **Implementation notes:**
  - Replace LINQ with explicit loop over `_entities`, inserting into a pre-sized list and sorting once.
  - Consider a coarse spatial hash updated on entity add/move/remove.
- **Acceptance criteria / tests:**
  - `GetEntitiesInRadius` returns the same ordering as before for known arrangements.
  - 1,000 radius queries on a 100-entity world allocate zero arrays after initial setup (if pooled).

---

## 16. Architecture

### ARC-1 — Finish incremental `GameManager` facade refactor under 500 lines

- **Priority:** P1
- **Status:** open
- **Target files:**
  - `Scripts/Autoloads/GameManager.cs`
  - `Scripts/Services/FloorTransitionService.cs`
  - `Scripts/Services/SpawnService.cs`
  - `Scripts/Services/AutoplayService.cs`
  - `Scripts/Services/RuntimeServiceFactory.cs`
  - `Scripts/Services/WorldSessionService.cs`
  - `Scripts/Services/TurnOrchestrator.cs`
  - `Scripts/Services/ProgressionEventBridge.cs`
  - `Tests/IntegrationTests/`
  - `Tests/UITests/`
- **Why it improves the game:** `GameManager` is still a 3,195-line Godot autoload facade that owns too many unrelated responsibilities. Reducing it to a thin coordinator lowers merge conflicts, makes UI integration safer, and keeps future features from adding more hidden coupling.
- **Implementation notes:**
  - Do this after or alongside Track 7 UI work only when public facade methods can remain stable.
  - Extract behavior behind existing `GameManager` methods rather than changing UI call sites first.
  - Keep deterministic rules and gameplay mutations in `Core/`; Godot-side services should orchestrate and emit events only.
  - Prefer narrow constructor-injected services under `Scripts/Services/`.
  - Avoid mixing broad extraction with feature changes.
- **Suggested phases:**
  1. Characterize current behavior with integration/UI tests around new game, save/load, action processing, floor travel, chest interaction, targeting, relic choice, and game over.
  2. Extract service/runtime setup into `RuntimeServiceFactory`.
  3. Extract save/load/session binding into `WorldSessionService`.
  4. Extract player-action processing and EventBus fanout into `TurnOrchestrator` plus `ProgressionEventBridge`.
  5. Extract entity creation/spawning into `EntityFactory`/`SpawnService`.
  6. Extract floor travel/cache/arrival logic into `FloorTransitionService`.
  7. Extract run/rest/autoexplore into `AutoplayService`.
  8. Trim `GameManager` to facade state, lifecycle, public UI API, and service delegation.
- **Acceptance criteria / tests:**
  - `GameManager.cs` is under 500 lines.
  - Existing UI-facing public methods remain source-compatible or have coordinated migration notes.
  - Full stub build, test project build, full harness, and rendering-validation profile pass.
  - No `using Godot` is introduced into `Core/`.
  - Track 7 UI surfaces are not blocked while this item remains open.

---

## 17. Test / CI Quality

### TST-1 — Capture full exception details on test failure

- **Status:** done
- **Priority:** P1
- **Target files:**
  - `Tests/TestFramework/TestRegistry.cs`
- **Why it improves the game:** `RunAll` currently prints only `ex.Message`, making regressions hard to diagnose.
- **Implementation notes:**
  - Emit `ex.ToString()` or `ex.Message` + `ex.StackTrace`.
  - Include failed test name and separator line.
- **Acceptance criteria / tests:**
  - Harness self-test registers a deliberately failing test and verifies output contains a stack-trace-like substring.

### TST-2 — Add test filtering / selection to harness

- **Status:** done
- **Priority:** P1
- **Target files:**
  - `Tests/Program.cs`
  - `Tests/TestFramework/TestRegistry.cs`
- **Why it improves the game:** Developers cannot run a single test or category subset without editing code.
- **Implementation notes:**
  - Support `--filter <prefix>` via `Environment.GetCommandLineArgs()`.
  - `RunAll(string? filter)` includes only tests whose name starts with/contains the filter.
- **Acceptance criteria / tests:**
  - Empty filter runs all tests.
  - `"Simulation."` runs only simulation tests.

### TST-3 — Reset shared static stub state between tests

- **Status:** done
- **Priority:** P1
- **Target files:**
  - `Compat/Godot/GodotStubs.cs`
  - `Tests/TestFramework/TestRegistry.cs`
- **Why it improves the game:** Godot stubs contain mutable static state (`MissingResourcePaths`, `PressedButtons`, shared `Viewport`) that can make UI/rendering tests order-dependent and flaky.
- **Implementation notes:**
  - Add `public static void ResetTestState()` that clears mutable static collections and resets shared state.
  - Call it before every registered test.
- **Acceptance criteria / tests:**
  - A test adds a path to `GD.MissingResourcePaths`; a following dummy test asserts the set is empty.

### TST-4 — Treat build warnings as errors in CI

- **Status:** done
- **Priority:** P1
- **Target files:**
  - `Directory.Build.props`
  - `.github/workflows/ci.yml`
- **Why it improves the game:** Without warnings-as-errors, nullable/reference warnings and stub mismatches can accumulate silently.
- **Implementation notes:**
  - `Directory.Build.props` enables `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` only when `RoguelitussyWarningsAsErrors=true` is supplied.
  - `.github/workflows/ci.yml` passes `-p:RoguelitussyWarningsAsErrors=true` to compile steps without relying on GitHub Actions' global `CI=true` environment.
- **Acceptance criteria / tests:**
  - `dotnet build godotussy.csproj -p:UseGodotStubs=true -p:RoguelitussyWarningsAsErrors=true` produces zero warnings.
  - `dotnet build Tests/godotussy.Tests.csproj -p:RoguelitussyWarningsAsErrors=true` produces zero warnings.

### TST-5 — Cache NuGet packages and Godot editor in CI

- **Status:** Done
- **Priority:** P1
- **Target files:**
  - `.github/workflows/ci.yml`
- **Why it improves the game:** The `godot-headless` job downloads the full Godot archive on every run and NuGet packages are restored without a cache.
- **Implementation notes:**
  - Add `actions/cache@v4` for `~/.nuget/packages` keyed on project files.
  - Cache extracted Godot directory keyed on archive name.
- **Acceptance criteria / tests:**
  - Second CI run shows cache hit for both NuGet and Godot steps.

---

## Recommended Implementation Waves

### Wave 1 — Authoritative State and Determinism (foundation)

Do these first; they unblock or stabilize everything else.

1. SAV-1 — Persist enemy template identity
2. SAV-2 — Persist `TurnScheduler` actor order
3. SAV-4 — Harden RNG rehydration order
4. SAV-3 — Add deterministic replay regression tests
5. STA-1 — Make `StatusEffectProcessor` data-driven from JSON
6. STA-3 — Honor status flags
7. COM-1 — Implement authored status-effect control flags

### Wave 2 — Close Authored-Content Gaps

These make content that already exists actually work in-game.

1. ITM-1 — Roll enemy loot tables on death
2. ITM-5 — Enable aimed scrolls from inventory UI
3. ABI-1 — Add field targeting mode
4. ABI-3 — Enforce mana costs and unify energy costs
5. ABI-5 — Emit `CombatEvent`s for teleport and `heal_self`
6. GEN-1 — Implement authored traps and hazard tiles
7. AI-1 — Honor authored `ai_params`

### Wave 3 — Tactical Depth

Build on Wave 1–2 to make combat/AI more interesting.

1. COM-2 — Ranged weapons
2. COM-3 — Weapon archetype properties
3. COM-4 — Soften armor scaling
4. COM-5 — Crit sparkle moments
5. ABI-2 — `aoe_line` and `aoe_cone`
6. ABI-4 — Content-driven ability cooldowns
7. AI-2 — Ambush behavior
8. AI-3 — Ranged kiting
9. AI-4 — Support enemies
10. AI-5 — Group aggro propagation

### Wave 4 — Progression and Identity

1. PRG-1 — Flatten early XP curve
2. PRG-5 — Archetype growth on level-up
3. PRG-2 — Build-shaping perks
4. PRG-3 — Draft three perks per level-up
5. CHR-1 — Restore `CharacterOptions` on load
6. CHR-2 — Race bonuses
7. CHR-3 — Signature starting abilities
8. CHR-4 — Improve creation preview clarity (**done**)

### Wave 5 — UI/UX and Inventory Polish

1. UI-4 — Standardize overlay close keys
2. UI-5 — Colorblind-safe rarity/HP
3. UI-3 — Status effect visuals
4. UI-2 — Combat log categories and filtering (**done**)
5. UI-1 — Mouse inventory interaction
6. INV-4 — Render ground items
7. INV-1 — Inventory filters
8. INV-2 — Bulk use/drop
9. INV-5 — Pickup radius

Recent QOL features outside the original numbered backlog are tracked in `docs/FEATURES.md`: rebuilt game-over stats, floor summaries, pause-run stats, examine cursor, quick-use hotbar, minimap legend, animated HUD bars, run movement, rest-until-healed, and autoexplore. These are implemented and covered by `Tests/UITests` smoke/regression tests.

### Wave 6 — Generation and Tools

1. GEN-5 — Clean up prefab library
2. GEN-2 — Themed floor sets
3. GEN-3 — Landmark rooms
4. GEN-4 — Locked doors and keys
5. TOL-3 — Asset-path validation in loader
6. TOL-2 — Full `ContentLoader` validation in tools
7. TOL-4 — Expanded item authoring
8. TOL-5 — Room metadata editing
9. TOL-1 — File-watcher validation

### Wave 7 — Onboarding, Feel, Performance, CI

1. ONB-1 — First Delve welcome message
2. ONB-2 — Stairs-visible hint
3. ONB-3 — Death learning tips
4. ONB-4 — Starter kit preview and tooltip (**done**)
5. ONB-5 — Key-reminder ribbon
6. GFX-1 — Hit flashes
7. GFX-2 — Camera shake
8. GFX-3 — Damage popups
9. GFX-4 — SFX/VFX catalog
10. PERF-1 — Event-driven `WorldView._Process`
11. PERF-2 — Incremental fog updates
12. PERF-3 — `EntityRenderer` child caching
13. PERF-4 — Tile art pooling
14. PERF-5 — Radius query optimization
15. TST-4 — Warnings-as-errors in CI (**done**)

---

## Cross-Cutting Implementation Rules

- Keep all deterministic gameplay state mutations in `Core/`. Do not add `using Godot` in `Core/`.
- Use `IAction` implementations for gameplay mutations. Route player actions through `GameManager.ProcessPlayerAction`.
- Use `EventBus` for cross-system presentation notifications. Include enough payload to identify affected targets.
- Add or update tests for every simulation, persistence, generation, content, progression, or UI behavior change.
- Update save migration and bump `SaveSerializer.CurrentVersion` when persistent state shape changes.
- Update documentation per `AGENTS.md` requirements.
- Mark suggestions `done` in this file as they are completed and update `improvments.md` / `DEVELOPMENT_RESUME_REPORT.md` status sections.
