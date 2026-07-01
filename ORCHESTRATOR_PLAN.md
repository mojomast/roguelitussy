# Roguelitussy — Parallel Subagent Development Plan

> **Orchestrator instructions:** This document defines all development work to transform `roguelitussy` from a roguelike into a full roguelite. Work is organized into **parallel tracks** that subagents can execute simultaneously without merge conflicts. Each track owns a non-overlapping set of files. All code targets **Godot 4.x with C# (.NET 8)**. The codebase uses an ECS-style component model, an `EventBus` autoload, a `ContentLoader` JSON pipeline, and `GameManager.cs` as the primary game controller.
>
> **Do not stop** until every task in every track is marked complete. After each track finishes, the orchestrator must run a **vertical integration pass** (Track 7) that wires all tracks together and validates the full game loop.

---

## Dependency Map

```
Track 1 (Meta-Progression)      ─┐
Track 2 (Relic System)          ─┤
Track 3 (Archetype Overhaul)    ─┤──► Track 7: Integration & Polish
Track 4 (Floor Events)          ─┤
Track 5 (Refactor / Bug Fixes)  ─┤
Track 6 (Content Authoring)     ─┘
```

Tracks 1–6 are fully parallel. Track 7 begins only after all others complete.

---

## Track 1 — Meta-Progression System

**Owns:** `Core/Persistence/`, `Core/Simulation/MetaProgression*.cs`, `Content/meta_upgrades.json`, `Content/run_history.json` (new files), `Scripts/Autoloads/MetaProgressionManager.cs` (new)

### Goal
Implement a persistent between-run upgrade store that survives death, giving players a reason to keep playing after each failed run.

### Tasks

#### T1.1 — `MetaProgressionData` model
- Create `Core/Persistence/MetaProgressionData.cs`
- Fields: `int EchoesTotal`, `int EchoesSpent`, `Dictionary<string, int> UnlockLevels`, `List<RunHistoryEntry> RunHistory` (cap at 20)
- `RunHistoryEntry`: `string CharacterName`, `string Archetype`, `int FloorReached`, `int EnemiesKilled`, `int GoldCollected`, `string CauseOfDeath`, `string BestItemName`, `int TotalTurns`, `long Timestamp`
- Serialize to/from JSON using `System.Text.Json`
- Save path: `user://meta_progress.json` (Godot user data dir)

#### T1.2 — `MetaProgressionManager.cs` autoload
- Create `Scripts/Autoloads/MetaProgressionManager.cs` as a Godot `Node` singleton
- Methods: `Load()`, `Save()`, `int GetEchoes()`, `void AddEchoes(int amount)`, `bool SpendEchoes(int cost)`, `bool IsUnlocked(string upgradeId)`, `int GetUnlockLevel(string upgradeId)`, `bool TryUpgrade(string upgradeId)`, `void RecordRun(RunHistoryEntry entry)`
- Auto-load on game start, bind to `EventBus.GameOver` to append run history and award echoes
- Register in `project.godot` autoloads

#### T1.3 — `Content/meta_upgrades.json`
Author at minimum the following upgrade nodes:
```json
[
  { "id": "starting_gold", "display_name": "Fortune's Favor", "max_level": 3, "cost_per_level": [10, 25, 50], "description": "Start each run with +30/+60/+100 gold.", "effect": "starting_gold", "values": [30, 60, 100] },
  { "id": "extra_inventory", "display_name": "Bottomless Pack", "max_level": 2, "cost_per_level": [20, 40], "description": "Gain +2/+4 inventory slots.", "effect": "inventory_bonus", "values": [2, 4] },
  { "id": "unlock_ranger", "display_name": "Ranger's Path", "max_level": 1, "cost_per_level": [30], "description": "Unlock the Ranger archetype.", "effect": "unlock_archetype", "values": ["ranger"] },
  { "id": "unlock_trickster", "display_name": "Trickster's Edge", "max_level": 1, "cost_per_level": [30], "description": "Unlock the Trickster archetype.", "effect": "unlock_archetype", "values": ["trickster"] },
  { "id": "unlock_arcanist", "display_name": "Arcanist's Tome", "max_level": 1, "cost_per_level": [30], "description": "Unlock the Arcanist archetype.", "effect": "unlock_archetype", "values": ["arcanist"] },
  { "id": "blessed_start", "display_name": "Blessed Beginning", "max_level": 3, "cost_per_level": [15, 35, 60], "description": "Start with 1/2/3 extra health potions.", "effect": "starting_items", "values": ["potion_health", "potion_health", "potion_health"] },
  { "id": "echo_magnet", "display_name": "Echo Magnet", "max_level": 3, "cost_per_level": [20, 45, 80], "description": "Earn +10%/+25%/+50% more Echoes per run.", "effect": "echo_bonus_pct", "values": [10, 25, 50] },
  { "id": "relic_seeker", "display_name": "Relic Seeker", "max_level": 1, "cost_per_level": [50], "description": "Guarantee one relic on floor 1.", "effect": "floor1_relic", "values": [1] }
]
```

#### T1.4 — Echo award formula
- Base echoes per run: `floor_reached * 2 + enemies_killed / 5 + (gold_collected / 50)`
- Bonus: first time reaching a new floor depth: `+5 echoes`
- Apply `echo_bonus_pct` meta upgrade multiplier from T1.3
- Call `MetaProgressionManager.AddEchoes()` inside the `GameOver` event handler

#### T1.5 — Apply meta bonuses at `StartNewGame`
- In `GameManager.StartNewGame`, query `MetaProgressionManager` before building `CharacterCreationOptions`
- Add `starting_gold` bonus to `WalletComponent` initial gold (currently hardcoded `StartingGold = 120`)
- Add `inventory_bonus` to `InventoryCapacityBonus`
- Add `starting_items` to `StartingItemTemplateIds`
- If `floor1_relic` is unlocked, add a relic item to the starting inventory (coordinate with Track 2 for relic item template IDs)

#### T1.6 — Persist run history and surface it
- After every `GameOver` event, call `MetaProgressionManager.RecordRun()` with data from `RunStats`
- Expose `MetaProgressionManager.GetRunHistory()` returning the last 20 `RunHistoryEntry` records
- This data is consumed by the UI (Track 7 will wire UI scenes)

---

## Track 2 — Relic System

**Owns:** `Core/Simulation/Relics/` (new directory), `Core/Content/RelicTemplate.cs` (new), `Content/relics.json` (new), modifications to `Core/Simulation/CombatResolver.cs`, `Core/Simulation/DeathResolver.cs`, `Core/Simulation/StatusEffectProcessor.cs`

### Goal
Add passive relic items that modify combat formulas and game events through hooks — NOT through stat additions. Relics are the primary roguelite build expression layer.

### Tasks

#### T2.1 — `RelicTemplate` content model
- Create `Core/Content/RelicTemplate.cs`:
```csharp
public sealed class RelicTemplate
{
    public string RelicId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Rarity { get; set; } = "common"; // common | uncommon | rare | legendary
    public string TriggerHook { get; set; } = string.Empty; // on_kill | on_hit | on_damaged | on_floor_enter | on_poison_tick | on_rest | on_low_hp
    public string EffectType { get; set; } = string.Empty; // heal | damage_bonus | gold_bonus | stat_mod | shield | echo_bonus
    public int EffectValue { get; set; }
    public string? ConditionTag { get; set; } // optional item/enemy tag filter
    public bool IsUnique { get; set; } = true;
}
```
- Register in `IContentDatabase` interface and `ContentLoader`

#### T2.2 — `RelicComponent` on player entity
- Create `Core/Simulation/Relics/RelicComponent.cs`:
```csharp
public sealed class RelicComponent
{
    public List<string> RelicIds { get; } = new();
    public int ShieldCharges { get; set; } // used by shield relics
    public bool LowHpRelicFired { get; set; } // one-time trigger guard
}
```
- Add `HasRelic(string relicId)` and `AddRelic(string relicId)` helpers
- Enforce `IsUnique` — prevent stacking the same relic twice

#### T2.3 — `RelicProcessor` static service
- Create `Core/Simulation/Relics/RelicProcessor.cs`
- Method signature: `static void ProcessHook(string hook, IEntity player, IWorldState world, IContentDatabase content, RelicHookContext ctx)`
- `RelicHookContext` carries: `EntityId? TargetId`, `int? DamageAmount`, `string? ItemTag`, `string? EnemyTag`, `ref int ModifiedValue` (allows relics to modify damage in-place)
- Implement all hooks listed in T2.1 `TriggerHook` values
- Hook insertion points:
  - `on_kill` → call from `DeathResolver.cs` after enemy death
  - `on_hit` → call from `CombatResolver.cs` after attacker deals damage
  - `on_damaged` → call from `CombatResolver.cs` after defender takes damage
  - `on_floor_enter` → call from `GameManager.LoadWorld`
  - `on_poison_tick` → call from `StatusEffectProcessor.cs` inside Poisoned tick handler
  - `on_rest` → call from `GameManager.RestPlayerUntilHealed` each tick
  - `on_low_hp` → call from `CombatResolver.cs` when player HP drops below 25%

#### T2.4 — `Content/relics.json` — author 20 relics minimum
```json
[
  { "relic_id": "vampire_fang", "display_name": "Vampire Fang", "description": "Heal 1 HP per kill.", "rarity": "common", "trigger_hook": "on_kill", "effect_type": "heal", "effect_value": 1 },
  { "relic_id": "glass_cannon", "display_name": "Glass Cannon", "description": "Double attack, half defense.", "rarity": "rare", "trigger_hook": "on_floor_enter", "effect_type": "stat_mod", "effect_value": 0 },
  { "relic_id": "toxic_core", "display_name": "Toxic Core", "description": "Poison ticks deal +3 bonus damage.", "rarity": "uncommon", "trigger_hook": "on_poison_tick", "effect_type": "damage_bonus", "effect_value": 3 },
  { "relic_id": "gold_tooth", "display_name": "Gold Tooth", "description": "Earn +2 gold per kill.", "rarity": "common", "trigger_hook": "on_kill", "effect_type": "gold_bonus", "effect_value": 2 },
  { "relic_id": "iron_skin", "display_name": "Iron Skin", "description": "Start each floor with a 5 HP shield.", "rarity": "uncommon", "trigger_hook": "on_floor_enter", "effect_type": "shield", "effect_value": 5 },
  { "relic_id": "berserker_heart", "display_name": "Berserker Heart", "description": "+4 attack when below 25% HP.", "rarity": "rare", "trigger_hook": "on_low_hp", "effect_type": "stat_mod", "effect_value": 4 },
  { "relic_id": "echo_shard", "display_name": "Echo Shard", "description": "Earn +5 bonus Echoes when floor is fully cleared.", "rarity": "uncommon", "trigger_hook": "on_floor_enter", "effect_type": "echo_bonus", "effect_value": 5 },
  { "relic_id": "thorn_wrap", "display_name": "Thorn Wrap", "description": "Reflect 1 damage to attacker when hit.", "rarity": "common", "trigger_hook": "on_damaged", "effect_type": "damage_bonus", "effect_value": 1 },
  { "relic_id": "lucky_coin", "display_name": "Lucky Coin", "description": "10% chance to negate damage taken.", "rarity": "uncommon", "trigger_hook": "on_damaged", "effect_type": "shield", "effect_value": 0 },
  { "relic_id": "rest_stone", "display_name": "Rest Stone", "description": "Heal +1 extra HP per rest tick.", "rarity": "common", "trigger_hook": "on_rest", "effect_type": "heal", "effect_value": 1 },
  { "relic_id": "warlord_crest", "display_name": "Warlord's Crest", "description": "+2 attack per floor cleared (max +10).", "rarity": "legendary", "trigger_hook": "on_floor_enter", "effect_type": "stat_mod", "effect_value": 2 },
  { "relic_id": "bone_amulet", "display_name": "Bone Amulet", "description": "+5 max HP per 3 enemies killed.", "rarity": "uncommon", "trigger_hook": "on_kill", "effect_type": "stat_mod", "effect_value": 5 },
  { "relic_id": "shadow_step", "display_name": "Shadow Step", "description": "+3 evasion for 2 turns after killing an enemy.", "rarity": "rare", "trigger_hook": "on_kill", "effect_type": "stat_mod", "effect_value": 3 },
  { "relic_id": "merchant_badge", "display_name": "Merchant Badge", "description": "Shop items cost 15% less.", "rarity": "uncommon", "trigger_hook": "on_floor_enter", "effect_type": "stat_mod", "effect_value": 15 },
  { "relic_id": "cursed_blade", "display_name": "Cursed Blade", "description": "+8 attack, but take 1 damage per turn.", "rarity": "rare", "trigger_hook": "on_floor_enter", "effect_type": "stat_mod", "effect_value": 8 },
  { "relic_id": "cartographer_lens", "display_name": "Cartographer's Lens", "description": "Reveal full floor map on enter.", "rarity": "uncommon", "trigger_hook": "on_floor_enter", "effect_type": "stat_mod", "effect_value": 0 },
  { "relic_id": "phoenix_ash", "display_name": "Phoenix Ash", "description": "Once per run: survive a lethal hit with 1 HP.", "rarity": "legendary", "trigger_hook": "on_low_hp", "effect_type": "shield", "effect_value": 1, "is_unique": true },
  { "relic_id": "predator_mark", "display_name": "Predator's Mark", "description": "First hit on an enemy each floor deals double damage.", "rarity": "rare", "trigger_hook": "on_hit", "effect_type": "damage_bonus", "effect_value": 0 },
  { "relic_id": "leech_stone", "display_name": "Leech Stone", "description": "Poisoned enemies take +2 bonus damage from all attacks.", "rarity": "uncommon", "trigger_hook": "on_hit", "effect_type": "damage_bonus", "effect_value": 2, "condition_tag": "poisoned" },
  { "relic_id": "death_mask", "display_name": "Death Mask", "description": "+20% damage for 5 turns after taking damage.", "rarity": "rare", "trigger_hook": "on_damaged", "effect_type": "damage_bonus", "effect_value": 20 }
]
```

#### T2.5 — Relic choice events (3 per run)
- **On boss kill (every 3rd floor):** fire `EventBus.EmitRelicChoiceReady(List<RelicTemplate> choices)` with 3 randomly selected relics filtered to not include already-owned relics
- **On shrine room interaction (Track 4):** same event type
- **On rare chest open:** 20% chance to replace loot with relic choice instead
- Add `EmitRelicChoiceReady` signal to `EventBus.cs`
- Add `ProcessRelicChoice(string relicId)` to `GameManager.cs` which calls `RelicProcessor.AddRelic`

#### T2.6 — Relic item integration with loot tables
- Add `"relic"` as an item category in `loot_tables.json`
- Relic items are NOT stored in inventory — they go directly to `RelicComponent` on pickup
- In `GameManager.ProcessPlayerAction` for `ActionType.PickupItem`, detect if item is relic type and route to `RelicProcessor.AddRelic` instead of inventory

---

## Track 3 — Archetype Overhaul

**Owns:** `Core/Simulation/ArchetypeDefinitions.cs` (new), `Core/Simulation/Actions/RangedAttackAction.cs` (new), modifications to `GameManager.CreatePlayer`, `Core/Simulation/ProgressionService.cs`, `Content/abilities.json`, `Content/perks.json`

### Goal
Make the `Archetype` field actually differentiate characters with unique stat spreads, starting abilities, perk pools, and one signature mechanic each.

### Tasks

#### T3.1 — `ArchetypeDefinition` model
- Create `Core/Simulation/ArchetypeDefinitions.cs`
- Define a `static readonly Dictionary<string, ArchetypeDefinition>` keyed by archetype ID (lowercase)
- `ArchetypeDefinition` fields:
  - `string Id`
  - `Stats BaseStats` — unique starting stat block per archetype
  - `string[] StartingItemIds` — archetype-specific starting items
  - `string[] StartingAbilityIds` — from `abilities.json`
  - `string SignatureMechanicId` — string key to identify special behavior
  - `string[] ExclusivePerkIds` — perks only available to this archetype

```csharp
// Vanguard — tanky frontliner
new ArchetypeDefinition("vanguard",
    baseStats: new Stats { HP=50, MaxHP=50, Attack=8, Accuracy=75, Defense=5, Evasion=8, Speed=90, ViewRadius=7 },
    startingItems: new[]{"potion_health","potion_health","item_shield_basic"},
    startingAbilities: new[]{"shield_bash"},
    signatureMechanic: "shield_bash_on_floor_enter",
    exclusivePerks: new[]{"perk_fortress","perk_undying"}
);

// Ranger — mobile, accurate, ranged
new ArchetypeDefinition("ranger",
    baseStats: new Stats { HP=35, MaxHP=35, Attack=10, Accuracy=95, Defense=2, Evasion=18, Speed=110, ViewRadius=10 },
    startingItems: new[]{"potion_health","item_arrows_bundle"},
    startingAbilities: new[]{"ranged_shot"},
    signatureMechanic: "ranged_attack_enabled",
    exclusivePerks: new[]{"perk_eagle_eye","perk_volley"}
);

// Trickster — fast, slippery, kill streak bonuses
new ArchetypeDefinition("trickster",
    baseStats: new Stats { HP=30, MaxHP=30, Attack=9, Accuracy=85, Defense=1, Evasion=22, Speed=120, ViewRadius=9 },
    startingItems: new[]{"potion_health","item_smoke_bomb"},
    startingAbilities: new[]{"backstab"},
    signatureMechanic: "kill_streak_double_turn",
    exclusivePerks: new[]{"perk_phantom_step","perk_death_dance"}
);

// Arcanist — fragile but powerful ability user
new ArchetypeDefinition("arcanist",
    baseStats: new Stats { HP=28, MaxHP=28, Attack=6, Accuracy=90, Defense=1, Evasion=12, Speed=100, ViewRadius=9 },
    startingItems: new[]{"scroll_fireball","scroll_frost_nova","potion_mana"},
    startingAbilities: new[]{"arcane_bolt","mana_shield"},
    signatureMechanic: "ability_charges_not_potions",
    exclusivePerks: new[]{"perk_arcane_surge","perk_spell_echo"}
);
```

#### T3.2 — Wire `ArchetypeDefinitions` into `GameManager.CreatePlayer`
- Replace hardcoded `stats.HP = 40` etc. with `ArchetypeDefinitions.Get(character.Archetype).BaseStats.Clone()`
- Apply `CharacterCreationOptions` bonuses on top of archetype base stats (keep existing bonus system)
- Merge archetype `StartingItemIds` with `CharacterCreationOptions.StartingItemTemplateIds`
- Attach archetype starting abilities to player via `AbilitiesComponent`
- Default archetype `"vanguard"` if unknown string passed (matches current default)

#### T3.3 — `RangedAttackAction` for Ranger
- Create `Core/Simulation/Actions/RangedAttackAction.cs`
- Behaves like `AttackAction` but has range 3–5 tiles, requires line-of-sight (use existing FOV data), requires `item_arrows_bundle` in inventory (consume 1 arrow per use)
- Add `ActionType.RangedAttack` to the `ActionType` enum
- Register in `GameManager.ProcessPlayerAction` action routing

#### T3.4 — Kill streak mechanic for Trickster
- Add `KillStreakComponent.cs` to `Core/Simulation/`:
```csharp
public sealed class KillStreakComponent
{
    public int CurrentStreak { get; set; }
    public int HighestStreak { get; set; }
    public int BonusXpAwarded { get; set; }
}
```
- In `DeathResolver.cs`: if attacker has `KillStreakComponent`, increment streak. If streak hits 3/5/10, award bonus XP via `ProgressionService`
- Reset streak when player takes damage
- Trickster signature: if `kill_streak_double_turn` and streak >= 3 and no adjacent enemy after a kill, award a free bonus action (set player energy to `EnergyThreshold` again)

#### T3.5 — Author archetype-exclusive perks in `Content/perks.json`
Add the following perks (merge into existing perks.json):
```json
{ "perk_id": "perk_fortress", "display_name": "Fortress", "description": "+3 Defense, +10 Max HP.", "archetype_restriction": "vanguard", "stat_modifiers": {"defense": 3, "max_hp": 10} },
{ "perk_id": "perk_undying", "display_name": "Undying", "description": "Survive lethal damage once per floor at 1 HP.", "archetype_restriction": "vanguard", "effect": "floor_undying" },
{ "perk_id": "perk_eagle_eye", "display_name": "Eagle Eye", "description": "+15 Accuracy, +2 View Radius.", "archetype_restriction": "ranger", "stat_modifiers": {"accuracy": 15, "view_radius": 2} },
{ "perk_id": "perk_volley", "display_name": "Volley", "description": "Ranged attacks hit all enemies in a line.", "archetype_restriction": "ranger", "effect": "ranged_pierce" },
{ "perk_id": "perk_phantom_step", "display_name": "Phantom Step", "description": "+5 Evasion, +10 Speed.", "archetype_restriction": "trickster", "stat_modifiers": {"evasion": 5, "speed": 10} },
{ "perk_id": "perk_death_dance", "display_name": "Death Dance", "description": "Kill streak threshold reduced by 1.", "archetype_restriction": "trickster", "effect": "streak_threshold_minus1" },
{ "perk_id": "perk_arcane_surge", "display_name": "Arcane Surge", "description": "Abilities deal +30% damage.", "archetype_restriction": "arcanist", "effect": "ability_damage_bonus_pct", "value": 30 },
{ "perk_id": "perk_spell_echo", "display_name": "Spell Echo", "description": "10% chance to cast an ability again for free.", "archetype_restriction": "arcanist", "effect": "spell_echo_pct", "value": 10 }
```
- Update `ProgressionService.GetAvailablePerkChoices` to filter by `archetype_restriction` matching the player's archetype (or null/empty = universal)

---

## Track 4 — Floor Events & Special Rooms

**Owns:** `Core/Generation/FloorEventResolver.cs` (new), `Core/Simulation/Actions/InteractShrineAction.cs` (new), `Content/floor_events.json` (new), modifications to `GameManager.PopulateWorld`, `Content/room_prefabs.json`, `Content/loot_tables.json`

### Goal
Break the monotony of "every floor is the same" by adding scripted special rooms on a deterministic cadence.

### Tasks

#### T4.1 — Floor type classification
- Create `Core/Generation/FloorEventResolver.cs`
- `static FloorType ResolveFloorType(int depth, int seed)` returns:
  - `depth % 5 == 0` → `SafeFloor` (merchant only, no enemies)
  - `depth % 3 == 0 && depth % 5 != 0` → `BossFloor` (mini-boss guaranteed)
  - All others → `StandardFloor`
- Also resolve special room injection:
  - Every `StandardFloor`: 40% chance of one `ShrineRoom`
  - Every `StandardFloor`: 25% chance of one `CurseRoom`
  - `BossFloor`: always includes one `BossRoom` with guaranteed relic drop post-kill

#### T4.2 — `SafeFloor` implementation
- On `SafeFloor`, skip enemy spawning entirely in `GameManager.PopulateWorld`
- Spawn 2–3 merchants (use existing `SpawnNpcs` with a higher `remainingSlots` cap)
- Add a `"floor_type": "safe"` indicator to `WorldState` (new property `FloorType FloorType`)
- Log: `"You have reached a sanctuary floor. No enemies lurk here."`

#### T4.3 — `ShrineRoom` — spend HP for a perk
- Add a `shrine` entity type with `ShrineComponent.cs`:
```csharp
public sealed class ShrineComponent
{
    public string ShrineType { get; set; } = "perk"; // perk | stat | relic
    public int HPCost { get; set; } = 10;
    public bool IsUsed { get; set; }
}
```
- Create `InteractShrineAction.cs` — validates player has > HPCost HP, deducts HP, fires `EventBus.EmitPerkChoiceReady` or `EmitRelicChoiceReady` depending on `ShrineType`
- Shrine room injection: in `GameManager.PopulateWorld`, if `FloorEventResolver` returns shrine, place one shrine entity in a dedicated room from `room_prefabs.json` tagged `"type": "shrine"`
- Add shrine room prefab to `room_prefabs.json`

#### T4.4 — `CurseRoom` — take a debuff, get extra loot
- `CurseRoom` is a room tagged in `room_prefabs.json` as `"type": "curse"`
- On room entry (detected via tile tag or entity proximity), fire `EventBus.EmitCurseRoomEntered`
- Apply one random `StatusEffectType` (`Poisoned`, `Weakened`, `Slowed`) for 10 turns
- Spawn 2–3 extra chest entities in the room
- Show log: `"Dark power fills this room. You feel cursed, but treasure glitters around you."`

#### T4.5 — `BossRoom` — mini-boss + guaranteed relic
- On `BossFloor`, force one enemy spawn with `IsBoss = true` in a dedicated boss room
- Wire `DeathResolver.cs`: when `IsBoss` enemy dies, call `EventBus.EmitRelicChoiceReady` with 3 relic choices
- Add `"boss_floor_loot"` loot table to `loot_tables.json` with high-value items
- Log: `"A powerful guardian blocks your path. Defeat it for a powerful reward."`

#### T4.6 — `Content/floor_events.json`
Author the floor event catalogue:
```json
[
  { "event_id": "shrine_perk", "type": "shrine", "shrine_type": "perk", "hp_cost": 10, "description": "A glowing altar offers power at a price." },
  { "event_id": "shrine_stat", "type": "shrine", "shrine_type": "stat", "hp_cost": 8, "description": "Runes pulse on the walls, offering a stat boon." },
  { "event_id": "shrine_relic", "type": "shrine", "shrine_type": "relic", "hp_cost": 15, "description": "A sacred relic rests on a sacrificial stone." },
  { "event_id": "curse_room", "type": "curse", "description": "Shadow seeps from the walls. Great danger, greater reward." },
  { "event_id": "treasure_vault", "type": "vault", "description": "A locked vault. Break it open for exceptional loot." }
]
```

---

## Track 5 — Refactor & Bug Fixes

**Owns:** `Scripts/Autoloads/GameManager.cs` (split), new service files under `Scripts/Services/`, `Core/Simulation/` bug fixes

### Goal
Start breaking up the 100KB God Object, fix identified bugs, and improve code quality without changing behavior. The first extraction pass should create safe service seams, but the full under-500-line target is a follow-up architecture backlog item and must not block Track 7 UI integration.

### Tasks

#### T5.1 — Extract `FloorTransitionService`
- Create `Scripts/Services/FloorTransitionService.cs`
- Move: `TryTravelToFloor`, `CreateGeneratedWorld`, `PlacePlayerInWorld`, `ResolveArrivalPosition`, `EnsureArrivalTile`, `TryPlacePlayerAt`, `EnumerateArrivalFallbacks`, `ResolveFloorEntrances`, `RememberFloorEntrances`, `DiscoverFloorEntrances`, `FindArrivalPosition`, `FindArrivalPositionOrInvalid`, `MixSeed`
- `FloorTransitionService` takes `GameManager` as a constructor dependency (or better: inject `EventBus`, `WorldState` ref, and floor cache via interfaces)

#### T5.2 — Extract `SpawnService`
- Create `Scripts/Services/SpawnService.cs`
- Move: `PopulateWorld`, `SpawnAuthoredNpcs`, `SpawnNpcs`, `SpawnTraps`, `SpawnKeys`, `CreateEnemyEntity`, `CreatePlayer`, `CreateChestEntity`, `CreateNpcEntity`, `CreateTrapEntity`, `EquipStartingLoadout`, `CreateFloorItems`, `ResolveEnemyTemplate`, `SelectEnemyTemplate`, `TryFindNpcSpawn`, `TryResolveSpawnPosition`, `IsValidSpawnPosition`, `FindNpcSpawnCandidates`, `IsEligibleNpcSpawnTile`, `CountWalkableNeighbors`, `IsAdjacentToDoor`, `IsNearStairs`
- Cache stair positions on world load to fix O(W×H) `IsNearStairs` bug

#### T5.3 — Extract `AutoplayService`
- Create `Scripts/Services/AutoplayService.cs`
- Move: `RunPlayerUntilBlocked`, `RestPlayerUntilHealed`, `AutoExplorePlayer`, `CanRunOneMoreStep`, `CanRestOneMoreTurn`, `CanAutoExploreOneMoreStep`, `TryFindAutoExploreStep`, `CanAutoExploreEnter`, `IsAutoExplorePointOfInterestTarget`, `IsAutoExploreFrontier`, `HasDangerousRestStatus`, `ShouldStopRunAtPointOfInterest`, `HasVisibleOrAdjacentHostile`

#### T5.4 — Fix `catch {}` in `CreateFloorItems`
```csharp
// BEFORE:
catch { }

// AFTER:
catch (Exception ex)
{
    GD.PushWarning($"[LootTable] Resolution failed for depth {effectiveDepth}: {ex.Message}");
}
```

#### T5.5 — Fix NPC spawn cap
- Replace `var remainingSlots = Math.Max(0, 2 - existingTemplates.Count)` with:
```csharp
var maxNpcs = world.Depth >= 5 ? 3 : 2; // or drive from ContentDatabase
var remainingSlots = Math.Max(0, maxNpcs - existingTemplates.Count);
```
- Add `MaxNpcsPerFloor` field to generator config

#### T5.6 — `IsNearStairs` O(W×H) fix
```csharp
// Add to WorldState or pass as parameter:
private static HashSet<Position> CacheStairPositions(WorldState world)
{
    var positions = new HashSet<Position>();
    for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var p = new Position(x, y);
            if (world.GetTile(p) is TileType.StairsUp or TileType.StairsDown)
                positions.Add(p);
        }
    return positions;
}

// IsNearStairs becomes O(stairs_count) with cached set
private static bool IsNearStairs(HashSet<Position> stairCache, Position position, int maxDistance)
    => stairCache.Any(s => position.DistanceTo(s) <= maxDistance);
```

#### T5.7 — Null safety in `TryResolveMerchantInteraction`
- Replace all `= null!` assignments with proper null checks and early returns
- Add `ArgumentNullException.ThrowIfNull` guards at method entry

#### T5.8 — `GameManager._Ready` robustness
- Wrap `EnsureRuntimeServices()` in a try/catch that logs to both GD and the EventBus
- Add a `bool _initialized` guard to prevent double-init

#### T5.9 — Backlog remaining `GameManager` under-500-line refactor
- Do not require the full under-500-line target before Track 7 UI work.
- Preserve the existing `GameManager` facade methods consumed by UI: new game, load/save, player action processing, floor travel, relic choice, character creation options, and runtime state accessors.
- Create or update the architecture backlog item that tracks the remaining extractions after the first Track 5 service pass.
- Recommended remaining extraction targets: `RuntimeServiceFactory`, `WorldSessionService`, `TurnOrchestrator`, `ProgressionEventBridge`, `EntityFactory`, `FloorTransitionService`, `SpawnService`, and `AutoplayService`.
- Acceptance: roadmap/backlog clearly states that Track 7 can continue while the refactor is open.

---

## Track 6 — Content Authoring

**Owns:** All files under `Content/` (json data only — no C# changes)

### Goal
Expand the content library to support all new systems and provide enough variety for meaningful run-to-run diversity.

### Tasks

#### T6.1 — Expand `enemies.json`
Add minimum 8 new enemy templates:
- `boss_stone_guardian` — IsBoss, high HP/Defense, slow, used on floor 3, 6, 9
- `boss_shadow_wraith` — IsBoss, high evasion, applies Burning on hit, used on floor 6+
- `venomspitter` — ranged poison attack, SpawnWeight favored on floors 2–5
- `armored_grunt` — high defense, low evasion, drops extra gold
- `shadow_thief` — steals gold on hit (implement via DeathResolver gold drop)
- `healer_sprite` — heals adjacent enemies each turn (new brain behavior)
- `explosive_golem` — explodes on death dealing AoE damage
- `spectral_hound` — ignores doors, high speed, low HP

For each: define `template_id`, `display_name`, `base_stats`, `faction`, `xp_value`, `spawn_weight`, `min_depth`, `abilities[]`

#### T6.2 — Expand `items.json` with tagged items
Add `"tags": []` field to all existing items. Tag existing items:
- Swords: `["weapon", "melee", "heavy"]`
- Daggers: `["weapon", "melee", "light"]`
- Bows: `["weapon", "ranged"]`
- Armor: `["armor", "heavy"]`
- Robes: `["armor", "light", "arcane"]`
- Potions: `["consumable", "healing"]`
- Scrolls: `["consumable", "arcane"]`

Add new items:
- `item_arrows_bundle` — consumable, stackable 20, required for Ranger ranged attack
- `item_smoke_bomb` — consumable, creates fog tile blocking sight for 3 turns
- `item_shield_basic` — offhand equipment, +3 Defense, `["armor", "shield"]`
- `scroll_fireball` — consumable, deals 12 AoE damage in 2-tile radius
- `scroll_frost_nova` — consumable, applies Slowed to all visible enemies for 5 turns
- `potion_mana` — consumable, restores all ability cooldowns
- `relic_item_slot` — special item type that routes to RelicComponent (coordinate with Track 2)

#### T6.3 — Expand `loot_tables.json`
- Add `"boss_floor_loot"` table: guaranteed rare/legendary item + relic_item_slot chance 100%
- Add `"safe_floor_merchant_stock"` table: higher quality items, lower prices
- Add `"shrine_reward_table"` table: stat scrolls, rare equipment
- Add `"curse_room_chest_loot"` table: 2x normal item value, higher rarity
- Update `"deep_floor_loot"` (depth 4+) to include relic item slots at 15% chance

#### T6.4 — Expand `perks.json`
Add universal (non-archetype) perks:
```json
{ "perk_id": "perk_tough", "display_name": "Tough", "description": "+15 Max HP.", "stat_modifiers": {"max_hp": 15} },
{ "perk_id": "perk_swift", "display_name": "Swift", "description": "+15 Speed, +5 Evasion.", "stat_modifiers": {"speed": 15, "evasion": 5} },
{ "perk_id": "perk_savage", "display_name": "Savage", "description": "+5 Attack.", "stat_modifiers": {"attack": 5} },
{ "perk_id": "perk_sharpshooter", "display_name": "Sharpshooter", "description": "+20 Accuracy.", "stat_modifiers": {"accuracy": 20} },
{ "perk_id": "perk_survivalist", "display_name": "Survivalist", "description": "Potions heal +5 bonus HP.", "effect": "potion_heal_bonus", "value": 5 },
{ "perk_id": "perk_treasure_sense", "display_name": "Treasure Sense", "description": "Chests revealed on minimap on floor enter.", "effect": "reveal_chests" },
{ "perk_id": "perk_iron_gut", "display_name": "Iron Gut", "description": "Immune to Poison.", "effect": "immune_poison" },
{ "perk_id": "perk_berserker", "display_name": "Berserker", "description": "+3 Attack per 10 HP missing.", "effect": "missing_hp_attack_bonus" }
```

#### T6.5 — Add `abilities.json` entries for new archetypes
- `shield_bash`: melee, stuns target for 1 turn, 4-turn cooldown
- `ranged_shot`: ranged, range 4, accuracy 90, 2-turn cooldown
- `backstab`: melee, +150% damage if target has not acted this round, 5-turn cooldown
- `arcane_bolt`: ranged, deals 15 magic damage (ignores defense), 3-turn cooldown
- `mana_shield`: self-buff, absorbs next 15 damage, 8-turn cooldown

#### T6.6 — Add `traps.json` entries
Expand from current minimal set:
- `trap_spike` — 5–8 damage, revealed by high Perception perk
- `trap_poison_gas` — applies Poisoned for 8 turns
- `trap_alarm` — alerts all enemies on floor
- `trap_teleport` — randomly teleports player
- `trap_gold_drain` — steals 20–50 gold

---

## Track 7 — Integration, Wiring & Polish (Run After Tracks 1–6)

**Owns:** All `Scenes/`, all `Scripts/UI/`, `Scripts/Autoloads/EventBus.cs`, `Scripts/Autoloads/GameManager.cs` final pass, `project.godot`

### Goal
Wire all track outputs together, implement missing UI, register autoloads, and validate the complete game loop from main menu → character creation → run → death → meta screen → new run.

### Tasks

#### T7.1 — Register new autoloads in `project.godot` — done
- `MetaProgressionManager` is present in the autoloads section.
- `EventBus` loads first, followed by `ContentDatabase`, `MetaProgressionManager`, and `GameManager`.

#### T7.2 — Character creation screen — archetype picker — done
- Update character creation UI to show 4 archetype cards (Vanguard, Ranger, Trickster, Arcanist)
- Lock archetypes not yet unlocked (Ranger, Trickster, Arcanist require meta unlocks from Track 1)
- Show archetype base stats preview, starting items, and signature mechanic description
- Pass selected archetype to `GameManager.SetCharacterCreationOptions`

#### T7.3 — Relic UI panel — done
- Add a relic tray to the HUD (bottom of screen, horizontal row of relic icons)
- On `EventBus.RelicChoiceReady`: show a modal panel with 3 relic cards (icon, name, description, rarity color)
- Player clicks one → call `GameManager.ProcessRelicChoice(relicId)`
- Show relic tray updates in real time

#### T7.4 — Meta-progression shop screen — done
- Add a screen accessible from the main menu: `MetaShopScene.tscn`
- Show current Echo balance
- Show upgrade grid from `meta_upgrades.json` with current level, cost, and max level
- Clicking an upgrade calls `MetaProgressionManager.TryUpgrade(upgradeId)`
- Locked archetypes shown as grayed-out cards with unlock cost

#### T7.5 — Run history / death screen — done
- Extend the game over screen to show full `RunStats`
- Add a "Recent Runs" tab showing last 5 `RunHistoryEntry` records from `MetaProgressionManager`
- Show Echoes earned this run with breakdown (floor × 2 + kills / 5 + gold / 50)

#### T7.6 — Floor event UI — partial
- On `EventBus.CurseRoomEntered`: show a centered popup banner for 3 seconds
- On `EventBus.BossRoomEntered`: show a boss health bar in the HUD
- On shrine interaction: show HP cost confirmation dialog before executing

#### T7.7 — Kill streak HUD indicator — done
- Show streak counter (flame icon + number) in the HUD when Trickster archetype and streak >= 2
- Animate with shake/glow on streak increment
- Reset animation on streak break

#### T7.8 — Final integration test checklist
Before marking Track 7 complete, manually verify:
- [x] Automated/stub verified: Vanguard/Ranger/Trickster/Arcanist UI preview and start-state plumbing.
- [x] Automated/stub verified: relic choice processing, relic tray text, boss HUD text, shrine confirmation surface, kill streak HUD text, and Track 7 component persistence.
- [ ] Manual Godot runtime verification remains required for exact room-entry cadence, boss-kill relic modal timing, safe-floor merchant population, and Trickster double-turn feel.

---

## File Ownership Reference

| File / Directory | Track |
|---|---|
| `Core/Persistence/MetaProgressionData.cs` | 1 |
| `Scripts/Autoloads/MetaProgressionManager.cs` | 1 |
| `Content/meta_upgrades.json` | 1 |
| `Core/Simulation/Relics/RelicComponent.cs` | 2 |
| `Core/Simulation/Relics/RelicProcessor.cs` | 2 |
| `Core/Content/RelicTemplate.cs` | 2 |
| `Content/relics.json` | 2 |
| `Core/Simulation/CombatResolver.cs` (relic hooks) | 2 |
| `Core/Simulation/DeathResolver.cs` (relic hooks) | 2 |
| `Core/Simulation/ArchetypeDefinitions.cs` | 3 |
| `Core/Simulation/Actions/RangedAttackAction.cs` | 3 |
| `Core/Simulation/KillStreakComponent.cs` | 3 |
| `Core/Generation/FloorEventResolver.cs` | 4 |
| `Core/Simulation/Actions/InteractShrineAction.cs` | 4 |
| `Core/Simulation/ShrineComponent.cs` | 4 |
| `Content/floor_events.json` | 4 |
| `Scripts/Services/FloorTransitionService.cs` | 5 |
| `Scripts/Services/SpawnService.cs` | 5 |
| `Scripts/Services/AutoplayService.cs` | 5 |
| `Content/enemies.json` | 6 |
| `Content/items.json` | 6 |
| `Content/loot_tables.json` | 6 |
| `Content/perks.json` | 6 |
| `Content/abilities.json` | 6 |
| `Content/traps.json` | 6 |
| `Content/relics.json` | 2+6 (coordinate) |
| `Scripts/Autoloads/EventBus.cs` | 7 |
| `Scripts/Autoloads/GameManager.cs` (final wiring) | 7 |
| `Scenes/UI/` | 7 |
| `project.godot` | 7 |

---

## Completion Criteria

The project is **done** when:
1. All 7 tracks are complete and their tasks checked off
2. The T7.8 integration checklist passes entirely
3. No `catch {}` (bare) swallowing exists in the codebase
4. `GameManager.cs` has a documented extraction backlog and no new Track 7 UI responsibilities are added to it; the full under-500-line target is a post-Track-7 architecture acceptance goal.
5. At least 20 relics exist in `relics.json`
6. All 4 archetypes are playable and meaningfully different
7. Meta shop persists across process restarts (read/write `user://meta_progress.json`)
8. Floor events fire on correct cadence (safe every 5, boss every 3)
9. The game compiles with `dotnet build` producing 0 errors

---

## Post-Track-7 Architecture Backlog

- Complete the remaining `GameManager.cs` facade refactor until the file is under 500 lines.
- Keep all deterministic gameplay mutation in `Core/`.
- Keep `GameManager` as a thin Godot autoload facade over focused services.
- Do not break public UI-facing methods while Track 7 polish is active.

---

*Last updated: 2026-07-01 | Plan version: 1.0.0*
