# Progression

This document defines the next progression pass for the project.

The current game already supports:

- XP from kills
- deterministic level thresholds
- baseline stat growth on level-up
- unspent stat points
- level-gated equipment requirements
- save/load for run progression

That is a workable foundation, but it is still a narrow "kill things, get bigger numbers" model. The next pass should make progression shape playstyle, recovery, and decision-making across a run without collapsing the tension that makes a dungeon crawler work.

## Design Goals

- Keep runs tactically dangerous even after repeated play.
- Make level-ups feel like build choices, not only math increments.
- Let shops and NPCs correct or sharpen a build instead of only selling raw stats.
- Preserve deterministic simulation authority in `Core/Simulation/`.
- Keep long-term progression mostly horizontal. Unlock possibility, not permanent brute-force power.
- Avoid turning the game into a menu-heavy deck of tiny bonuses.

## Genre Direction

Broad recent roguelike and roguelite trends point in a consistent direction:

- Players expect dual-layer progression.
  - Strong in-run growth.
  - Light long-term unlocks.
- Early runs need agency fast.
  - A meaningful build-defining choice should arrive early.
- Horizontal unlocks land better than permanent stat inflation.
  - new perk pools
  - new NPC services
  - new item families
  - new starts and backgrounds
- Reward readability matters.
  - Players should understand why a choice matters immediately.
- Recovery tools matter.
  - Shops, trainers, and rerolls should help salvage bad luck.

For this project, that translates into: strong run-level progression first, light hub/meta later, and almost no permanent raw-stat upgrades.

## Recommended Model

Use a three-layer progression model.

### 1. Run Progression

Run progression should have three parts.

- Base level growth.
  - XP still comes from kills and later from milestone objectives.
  - Level thresholds stay deterministic.
  - Leveling grants a small automatic baseline bump so every level matters immediately.
- Level-up picks.
  - Each level grants one choice from a small set of upgrade options.
  - Options should be high-signal and archetype-shaping.
- Floor milestone rewards.
  - Every few floors, the run should deliver a guaranteed strong decision point.

The run should not depend only on random gear. Levels, perks, shops, and floor events should all contribute to build identity.

### 2. NPC And Service Progression

NPCs should become progression anchors, not only flavor and vending machines.

- Merchant:
  - run correction
  - targeted gear and consumables
  - limited reroll or reserve mechanic
- Trainer:
  - offers technique upgrades or class-weighted perk options
- Chronicler or Cartographer:
  - information rewards, map reveals, enemy intel, future floor hints
- Smith or Alchemist:
  - upgrade or reforge one item property

These services should be primarily in-run at first. Hub continuity can come later.

### 3. Light Meta Progression

Meta progression should unlock breadth rather than solve difficulty through passive stat creep.

- unlock new archetypes and origins
- unlock new perk pools
- unlock new NPC services or service tiers
- unlock new room events, rare items, or side-objectives

Avoid permanent bonuses like:

- +10% damage forever
- +25 starting HP forever
- flat permanent defense ladders

Those flatten the early game and make balance worse.

## What A Level-Up Should Look Like

Every level-up should have two outputs.

- Automatic reward:
  - small HP bump
  - maybe one secondary stat bump tied to archetype
- Choice reward:
  - one of three upgrade options

That keeps level-ups fast and still expressive.

Recommended choice categories:

- offense
- defense
- mobility
- utility
- economy

Example choices:

- `Brutal Follow-Through`: first melee hit each floor gets bonus crit chance
- `Field Dressing`: first healing consumable each floor heals more
- `Delver's Eye`: reveal traps and interactables in a small radius
- `Quickdraw`: using a consumable refunds part of its energy cost
- `Haggler`: first purchase each floor is discounted

These are more interesting than `+1 attack` because they create a verb, not only a number.

## Recommended Run Arc

The game should guarantee a recognizable progression cadence.

- Floor 1:
  - stable onboarding
  - at least one strong build signal by the end of the floor or very early on floor 2
- Floor 2-3:
  - first meaningful specialization
  - trainer, merchant, or milestone room
- Floor 4-5:
  - correction tools and stronger enemy pressure
- Floor 6:
  - specialization checkpoint or mini-boss reward
- Floor 7+:
  - fewer rewards, higher impact, stronger enemy identity

This project already has room prefabs, shops, NPCs, and floor-based generation. Those systems should carry the run arc instead of adding a separate progression minigame.

## Concrete Framework For This Repo

### Phase 1: Refactor The Current Level-Up Core

Move level-up logic out of `AttackAction`.

Current problems:

- XP grant and level-up mutation are embedded directly in combat resolution.
- baseline rewards are hard-coded.
- the level-up path is not content-driven.

Phase 1 target:

- create a simulation-side progression resolver, for example:
  - `Core/Simulation/ProgressionResolver.cs`
  - or `Core/Simulation/ProgressionService.cs`
- `AttackAction` should only award XP through that resolver.
- the resolver should:
  - add XP
  - process chained level-ups
  - apply automatic rewards
  - create pending level-up choices

### Phase 2: Add Perk Choices

Add a content-driven perk system.

Suggested content file:

- `Content/perks.json`

Suggested runtime model:

- `PerkTemplate`
- `PerkDefinition`
- `PerkComponent` or additional fields on `ProgressionComponent`

Suggested `ProgressionComponent` additions:

- chosen perk ids
- pending choice count
- maybe archetype affinity or mastery tags

Suggested rules:

- each level grants one perk pick
- present three options
- options come from:
  - common pool
  - archetype pool
  - current build tags

Keep the initial perk set small. Around 12 to 20 perks is enough for the first meaningful pass.

### Phase 3: Add Progression Events And UI

The current event bus already exposes XP and level-up hooks. Expand that into a real progression UI flow.

Needed surfaces:

- HUD:
  - pending level-up indicator
  - short XP feedback
- Character Sheet:
  - current perks
  - upcoming unlock hints
  - stat allocation if that still exists
- dedicated level-up overlay:
  - choose one of three perks
  - no need to bury build-defining choices inside the character sheet modal

Recommended interaction model:

- when a level-up happens, automatic rewards apply immediately
- perk choices become pending
- player can resolve them immediately or on the next safe turn

### Phase 4: Rework Stat Points

The current unspent stat points system is functional but weak as a main progression vector.

Recommended direction:

- keep stat points, but demote them to a secondary layer
- give fewer points and make them less central
- use them for core shape only:
  - vitality
  - power
  - precision
  - agility
  - will or focus

Do not let stat points become the only meaningful level-up output.

If a perk system lands well, the game should feel playable even if stat points are simplified.

### Phase 5: Make Shops And NPCs Part Of Progression

Progression should not stop at the level-up screen.

Use existing NPC and shop infrastructure to support build shaping.

Examples:

- Merchant sells:
  - recovery
  - build patch items
  - one or two synergy pieces
- Trainer offers:
  - archetype-weighted perk retraining or technique upgrades
- Chronicler offers:
  - map intel
  - enemy dossiers
  - objective hints

This makes your new NPC/dialog/shop work part of progression instead of a disconnected side system.

### Phase 6: Add Light Meta Unlocks

Only after run progression feels good.

Suggested meta currency model:

- one currency from run completion, milestones, boss kills, or death summary
- spend it between runs on horizontal unlocks

Unlock examples:

- new archetype starts
- new perk branches
- extra merchant stock categories
- rare room events
- additional NPC rescue candidates

Do not start with permanent damage or HP upgrades.

## Systems To Add

Recommended new simulation/content pieces:

- `Content/perks.json`
- `Core/Content/PerkDefinition` and `PerkTemplate`
- `Core/Simulation/PerkComponent`
- `Core/Simulation/ProgressionResolver`
- `Scripts/UI/LevelUpOverlay.cs`

Recommended event bus additions:

- `LevelUpChoicesOffered`
- `PerkChosen`
- maybe `ProgressionStateChanged`

## Existing Files Most Likely To Change

Core progression work:

- `Core/Simulation/ProgressionComponent.cs`
- `Core/Simulation/Actions/AttackAction.cs`
- `Core/Persistence/SaveSerializer.cs`
- `Core/Contracts/IContentDatabase.cs`
- `Core/Content/ContentLoader.cs`
- `Core/Content/ContentModels.cs`

UI work:

- `Scripts/UI/CharacterSheet.cs`
- `Scripts/UI/HUD.cs`
- `Scripts/UI/UIRoot.cs`
- new level-up overlay file under `Scripts/UI/`

Game orchestration:

- `Scripts/Autoloads/GameManager.cs`
- `Scripts/Autoloads/EventBus.cs`

Tests:

- `Tests/SimulationTests/ProgressionTests.cs`
- `Tests/PersistenceTests/ProgressionPersistenceTests.cs`
- `Tests/UITests/CharacterUXTests.cs`
- new tests for perk offering and resolution

## Content And Rule Recommendations

### Keep

- deterministic XP thresholds
- deterministic save/load behavior
- simulation authority in core
- depth-based item and enemy availability

### Change

- move level-up mutation out of combat action code
- stop relying on stat points alone for build identity
- make level-up rewards content-driven
- make NPCs and shops active progression tools

### Avoid

- many currencies
- permanent stat grind
- massive perk pools early
- weak fake choices like three tiny stat bumps
- loot spam as substitute for progression design

## Recommended First Implementation Slice

The first useful slice is not meta progression. It is a better in-run level-up loop.

Implement this first:

1. Extract XP and level-up logic into a progression resolver.
2. Add perk content and a minimal perk component.
3. Add a level-up overlay that offers one of three perks.
4. Keep the current stat-point system temporarily, but make perks the primary reward.
5. Add one NPC service that interacts with progression, ideally a trainer or chronicler.

If this slice feels good, then build the hub-level unlock layer on top of it.

## Short Version

The right model for this project is:

- levels provide structure
- perks provide build identity
- NPCs and shops provide correction and continuity
- meta unlocks provide breadth

Runs should provide power.

Meta should provide possibility.

NPCs should provide continuity.