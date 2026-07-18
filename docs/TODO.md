# TODO

This document is a concise planning view for remaining work. `docs/IMPROVEMENT_SUGGESTIONS.md` remains the detailed backlog with target files and acceptance criteria.

| Priority | Feature | Description | Blocking Dependencies |
|---|---|---|---|
| P1 | Inventory mouse interaction | Support click selection, double-click activate, and right-click drop behavior. | Inventory action paths should stay routed through existing action factories. |
| P1 | Ranged weapons | Make authored ranged weapons attack beyond adjacency with range/LOS validation. | Item schema/runtime support for weapon range. |
| P1 | Weapon archetypes | Add cleave/reach-style weapon properties for tactical differentiation. | Combat resolver/action updates and content validation. |
| P1 | Armor scaling polish | Soften high-defense early invulnerability with tested damage expectations. | Balance decision for reduction formula. |
| P1 | Critical-hit feedback | Make crits more visible and potentially guarantee on-hit procs. | Combat resolver tests and log presentation. |
| P1 | Guaranteed chest per floor | Ensure every generated floor has at least one chest reward opportunity. | Generation placement tests; chest loot table selection by depth. |
| P1 | Room item-quality bonus | Make room `ItemQualityBonus` bias loot rarity/value directly. | Loot table resolver support for quality modifiers. |
| P1 | Encumbrance | Make item weight affect energy/speed and show burden in inventory. | Inventory weight calculation and turn scheduler integration. |
| P1 | Ability targeting shapes | Implement `aoe_line` and `aoe_cone`. | Ability resolver shape support and content validation. |
| P1 | Mana and energy costs | Enforce mana costs and unify item-cast energy costs with ability templates. | MP/stat model decision. |
| P1 | Ability cooldowns | Add content-driven cooldowns to `AbilityTemplate`. | Cooldown persistence/UI expectations. |
| P1 | Teleport/heal combat events | Emit animation-friendly combat events for blink/teleport and `heal_self`. | `CombatEvent` shape update and EventBus forwarding. |
| P1 | Authored status `tick_timing` | Respect start/end/none timing from status content. | Scheduler/status processor sequencing decision. |
| P1 | XP curve | Flatten early XP thresholds for faster first payoff. | Progression balance tests. |
| P1 | Build-shaping perks | Add perk triggers such as heal-on-kill, bonus gold, resist, or energy on kill. | Perk effect schema/runtime validation. |
| P1 | Three-perk level-up draft | Offer deterministic limited perk drafts instead of the full unlocked pool. | Persist pending draft IDs. |
| P2 | Archetype-weighted perk draft | Bias perk drafts by selected archetype. | Three-perk draft implementation and perk tags. |
| P1 | Archetype growth | Auto-apply archetype-specific stat growth on level-up. | Character options/progression integration. |
| P1 | Race mechanics | Give races minor stat or rule differences and preview them during creation. | Character creation preview updates. |
| P1 | Signature starting abilities | Give archetypes native starting abilities. | Ability component save/load and creation preview. |
| P2 | Randomize build | Add one-key/random menu option for character creation variety. | Decide deterministic seeding expectations. |
| P1 | Inventory filters | Add category/rarity filters for crowded inventory lists. | Inventory visible-list refactor. |
| P1 | Stack bulk actions | Add bulk use/drop and explicit stack split quantity prompt. | Drop/use action quantity semantics. |
| P1 | Ground item rendering | Render and highlight ground items in world view. | World rendering layer and fog rules. |
| P1 | Adjacent pickup | Allow radius pickup for nearby loot. | Pickup action radius semantics. |
| P1 | Crash-safe autosave | Make autosave automatic and safer against partial writes. | Persistence temp-file/write policy. |
| P1 | Content validation tooling | Add file-watcher validation, pre-save full loader validation, and stricter `res://` path policy. | Tooling UX decision; current tests validate paths. |
| P1 | Landmark special rooms | Guarantee one landmark/special room per floor. | Generation placement rules and content tags. |
| P1 | Prefab library expansion | Clean up and expand room prefabs after current systems stay green. | Existing content validation and theme coverage. |
| P1 | Onboarding hints | Add first-delve message, stairs objective hint, game-over tips, and key-reminder ribbon. | Help/log/HUD copy pass. |
| P1 | Game feel pass | Hit flashes, attack lunges, death fades, and damage/crit/miss/heal/pickup popups are implemented; continue with camera shake, exact heal payloads, animation/SFX fields, and projectile travel. | Event payload support for all animated outcomes. |
| P1 | Generation integration gaps | Resolve boss-marked spawns to boss templates, populate shrine/curse rooms, guarantee requested landmarks, and validate enough reachable keys for locks. | Generator-to-GameManager metadata contract. |
| P1 | Remaining relic semantics | Align rest-per-tick healing, Shadow Step evasion, Echo Shard floor-clear rewards, and Merchant Badge cached-floor pricing with authored descriptions. | Focused hook/event contracts and persistence where needed. |
| P1 | Runtime art mapping | Make enemy rendering honor authored `sprite_path` and add explicit locked-door art. | Content-backed renderer lookup. |
| P1 | Performance pass | Make world rendering more event-driven, reduce fog iteration, pool tile nodes, and cache radius queries. | Profiling target and rendering tests. |
