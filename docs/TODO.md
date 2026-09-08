# TODO

This document is a concise planning view for remaining work. `docs/IMPROVEMENT_SUGGESTIONS.md` remains the detailed backlog with target files and acceptance criteria.

| Priority | Feature | Description | Blocking Dependencies |
|---|---|---|---|
| P1 | Inventory mouse interaction | Support click selection, double-click activate, and right-click drop behavior. | Inventory action paths should stay routed through existing action factories. |
| P1 | Weapon archetypes | Add cleave/reach-style weapon properties for tactical differentiation. | Combat resolver/action updates and content validation. |
| P1 | Armor scaling polish | Soften high-defense early invulnerability with tested damage expectations. | Balance decision for reduction formula. |
| P1 | Critical-hit feedback | Make crits more visible and potentially guarantee on-hit procs. | Combat resolver tests and log presentation. |
| P1 | Guaranteed chest per floor | Ensure every generated floor has at least one chest reward opportunity. | Generation placement tests; chest loot table selection by depth. |
| P1 | Room item-quality bonus | Make room `ItemQualityBonus` bias loot rarity/value directly. | Loot table resolver support for quality modifiers. |
| P1 | Encumbrance | Make item weight affect energy/speed and show burden in inventory. | Inventory weight calculation and turn scheduler integration. |
| P1 | Mana and energy costs | Enforce mana costs and unify item-cast energy costs with ability templates. | MP/stat model decision. |
| P1 | Ability cooldowns | Add content-driven cooldowns to `AbilityTemplate`. | Cooldown persistence/UI expectations. |
| P1 | Teleport/heal combat events | Emit animation-friendly combat events for blink/teleport and `heal_self`. | `CombatEvent` shape update and EventBus forwarding. |
| P1 | Authored status `tick_timing` | Respect start/end/none timing from status content. | Scheduler/status processor sequencing decision. |
| P1 | XP curve | Flatten early XP thresholds for faster first payoff. | Progression balance tests. |
| P2 | Archetype-weighted perk draft | Bias perk drafts by selected archetype. | Three-perk draft implementation and perk tags. |
| P1 | Archetype growth | Auto-apply archetype-specific stat growth on level-up. | Character options/progression integration. |
| P2 | Randomize build | Add one-key/random menu option for character creation variety. | Decide deterministic seeding expectations. |
| P1 | Inventory filters | Add category/rarity filters for crowded inventory lists. | Inventory visible-list refactor. |
| P1 | Stack bulk actions | Add bulk use/drop and explicit stack split quantity prompt. | Drop/use action quantity semantics. |
| P1 | Ground item rendering | Render and highlight ground items in world view. | World rendering layer and fog rules. |
| P1 | Adjacent pickup | Allow radius pickup for nearby loot. | Pickup action radius semantics. |
| P1 | Crash-safe autosave | Make autosave automatic and safer against partial writes. | Persistence temp-file/write policy. |
| P1 | Content validation tooling | Add file-watcher validation, pre-save full loader validation, and stricter `res://` path policy. | Tooling UX decision; current tests validate paths. |
| P1 | Prefab library expansion | Clean up and expand room prefabs after current systems stay green. | Existing content validation and theme coverage. |
| P1 | Landmark depth semantics | Preserve stronger authored landmark/depth requests when fallback placement is needed. | Generation content contract. |
| P1 | Game feel pass | Hit flashes, attack lunges, death fades, and damage/crit/miss/heal/pickup popups are implemented; continue with camera shake, exact heal payloads, animation/SFX fields, and projectile travel. | Event payload support for all animated outcomes. |
| P1 | Generation integration gaps | Requested landmark depth semantics and candidate exhaustion remain possible edge cases; landmark metadata/fallbacks and lock/key solvability validation are shipped. | Generator-to-GameManager metadata contract. |
| P1 | Remaining relic semantics | Align rest-per-tick healing, Shadow Step evasion, Echo Shard floor-clear rewards, and Merchant Badge cached-floor pricing with authored descriptions. | Focused hook/event contracts and persistence where needed. |
| P1 | Unsupported challenge modifiers | Implement remaining authored daily and Ascension modifiers without overstating the current nine-floor contract. | Explicit effect contracts and deterministic tests. |
| P1 | Ability targeting shapes | Implement `aoe_line` and `aoe_cone`; current runtime supports self, single, tile, and circle shapes. | Ability geometry contract and content validation. |
| P2 | Class/resource polish | Decide whether class/resource terminology needs deeper mechanics; the current game has no MP resource. | Progression design decision. |
| P2 | Visual shutdown leaks | Isolate the remaining CanvasItem/ObjectDB shutdown leak reported by real Godot visual capture. | Reproduction under the supported Godot runtime. |
| P1 | Runtime art mapping | Add explicit locked-door art. Authored enemy `sprite_path` rendering is wired through template identity. | Content-backed renderer lookup. |
| P1 | Performance pass | Make world rendering more event-driven, reduce fog iteration, pool tile nodes, and cache radius queries. | Profiling target and rendering tests. |
