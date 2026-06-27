# Features

## Current Status Summary

| Feature | Status | Current State | Follow-up |
|---|---|---|---|
| GameOverScreen | Implemented | Displays finalized run stats through `GameOverWithStats`. | Add death-specific learning tips if desired. |
| FloorSummaryUI | Implemented | Shows per-floor stats during floor travel. | None required for current behavior. |
| Pause menu run stats | Implemented | Shows compact current-run stats in pause menu. | None required for current behavior. |
| CombatLog category coloring | Partial | `LogCategory` colors, critical emphasis, and age fading are implemented. | Category filtering remains planned. |
| ExaminePanel | Implemented | `X` opens examine mode; cursor movement is non-mutating. | None required for current behavior. |
| RunUntilBlocked | Implemented | `R` then direction runs until blocked/interrupted. | Historical `Shift`+direction references are stale. |
| RestUntilHealed | Implemented | `Z` rests until healed/interrupted/capped. | Historical `R`-rest references are stale. |
| AutoExplore | Implemented | `O` autoexplores through normal move processing. | None required for current behavior. |
| Quick-use hotbar | Implemented | `QuickSlotHotbar` and HUD text derive the first five usable items; keys `1`-`5` use them. | None required for current behavior. |
| Minimap legend | Implemented | Legend exists inside the minimap and has an independent normal-gameplay toggle. | None required for current behavior. |
| ChestUI `MenuBase` refactor | Planned | Chest UI is functional but extends `Control` directly. | Refactor onto shared menu chrome/input conventions. |
| Animated HUD bars | Implemented | HP/energy bars interpolate and pulse on changes. | None required for current behavior. |

The current keybind source of truth is `docs/KEYBINDS.md`.

## Death Screen

**Block:** 1  **Status:** Implemented

**Keybinds:** `Enter` new run, `Escape` main menu

The death screen displays run stats from `GameManager.CurrentRunStats`, including floor reached, turn count, kills, gold, items found, damage taken, seed, cause of death, and best item. It is surfaced through `EventBus.GameOverWithStats` while preserving the older `GameOver` signal.

**Files modified:** `Scripts/UI/GameOverScreen.cs`, `Scripts/Autoloads/GameManager.cs`, `Scripts/Autoloads/EventBus.cs`, `Scripts/UI/UIRoot.cs`, `Tests/UITests/GameOverScreenTests.cs`.

## Floor Summary

**Block:** 2  **Status:** Implemented

**Keybinds:** `Enter`/`Space` continue, any key stops auto-dismiss countdown

The floor summary displays per-floor stats from `FloorStats`, including floor number, kills, items found, gold, damage taken, turns spent, opened chests, and triggered traps. It is surfaced through `EventBus.FloorSummaryReady` during floor travel and dismisses through `EventBus.FloorTransitionConfirmed`; the transition itself is not delayed, but gameplay input is blocked while the summary is visible.

**Files modified:** `Scripts/UI/FloorSummaryUI.cs`, `Scripts/Autoloads/GameManager.cs`, `Scripts/Autoloads/EventBus.cs`, `Scripts/UI/UIRoot.cs`, `Tests/UITests/FloorSummaryTests.cs`.

## Pause Menu Run Stats

**Block:** 3  **Status:** Implemented

**Keybinds:** `Escape` pause/resume, `Enter` confirm selected action, `H` help, `T` workshop

The pause menu displays a compact `CURRENT RUN` snapshot from `GameManager.CurrentRunStats` between the pause explanation and action list. It includes floor reached, turn count, enemies killed, gold collected, damage taken, items found, and seed, with numeric values formatted using thousand separators.

**Files modified:** `Scripts/UI/PauseMenu.cs`, `Tests/UITests/PauseMenuTests.cs`, `docs/FEATURES.md`.

## Categorized Combat Log

**Block:** 4  **Status:** Implemented

The combat log now renders categorized messages from `EventBus.LogMessage`. Categories color system, player, enemy, loot, status, warning, and critical entries distinctly; critical entries are bold, and older visible lines fade using BBCode alpha. The existing `EmitLogMessage(string)` overload remains supported and defaults to `System`.

**Files modified:** `Scripts/UI/CombatLog.cs`, `Scripts/Autoloads/EventBus.cs`, `Scripts/Autoloads/GameManager.cs`, `Scripts/UI/UiStyle.cs`, `Tests/UITests/CombatLogTests.cs`.

## Examine Cursor

**Block:** 5  **Status:** Implemented

**Keybinds:** `X` open/close examine mode, `WASD`/arrow keys move cursor, `Escape` close

Examine mode opens a `MenuBase` panel at the player's position and lets the player inspect visible or already explored nearby map cells without submitting gameplay actions or mutating `WorldState`. Visible cells can describe the tile, entity, chest, revealed trap, and ground items from existing state/content templates; explored-but-not-visible cells only show remembered tile information to avoid revealing current hidden details.

**Files modified:** `Scripts/UI/ExaminePanel.cs`, `Scripts/UI/InputHandler.cs`, `Scripts/UI/UIRoot.cs`, `Compat/Godot/GodotStubs.cs`, `Tests/UITests/ExaminePanelTests.cs`, `docs/SYSTEMS.md`, `docs/FEATURES.md`.

## Quick-Use Hotbar

**Block:** 6  **Status:** Implemented

**Keybinds:** `1`-`5` quick-use visible hotbar slots during normal gameplay

`QuickSlotHotbar` shows five runtime slots derived from the first five usable non-equipment inventory entries, while HUD keeps its compact text summary for compatibility. Number keys submit the same `UseItemAction` path as inventory use for safe consumables such as potions. Aimed scrolls remain visible in the derived model and are not consumed blindly; pressing their hotkey logs a warning and instructs the player to aim them from inventory. Slot assignment is intentionally derived from current inventory order and is not persisted.

**Files modified:** `Scripts/UI/QuickSlotHotbar.cs`, `Scripts/UI/UIRoot.cs`, `Scripts/UI/HUD.cs`, `Scripts/UI/InputHandler.cs`, `Scripts/UI/UIActionFactory.cs`, `Tests/UITests/QuickSlotTests.cs`, `Tests/UITests/UISmokeTests.cs`, `docs/KEYBINDS.md`, `docs/FEATURES.md`, `docs/TODO.md`.

## Minimap Legend

**Block:** 7  **Status:** Implemented

**Keybinds:** `M`/`Tab` toggle minimap visibility, `U` toggles the minimap legend during normal gameplay

The minimap keeps the existing HUD summary and gameplay toggle behavior. The legend identifies floor, door, stairs, trap, player, enemy, NPC, item, and chest markers, and is hidden by default until toggled independently. Visible enemies, NPCs, items, and chests draw small colored markers over explored tile colors; trap, door, and stair tiles keep distinct colors aligned with the legend. When the minimap itself is hidden or suppressed by modal UI, the legend stays hidden while preserving the user's legend preference.

**Files modified:** `Scripts/UI/Minimap.cs`, `Scripts/UI/InputHandler.cs`, `Scripts/UI/UIRoot.cs`, `Tests/UITests/MinimapTests.cs`, `docs/KEYBINDS.md`, `docs/FEATURES.md`.

## Animated HUD Bars

**Block:** 8  **Status:** Implemented

The HUD keeps HP and energy text values immediate, but the visible bar fills now interpolate toward their latest targets over `_Process(...)` frames. HP changes trigger a short red damage pulse or green healing pulse; energy changes trigger a warning/gold pulse. The state is exposed through HUD properties so UITests can verify target values, displayed values, and pulse behavior without relying on Godot rendering output.

**Files modified:** `Scripts/UI/HUD.cs`, `Tests/UITests/UISmokeTests.cs`, `docs/SYSTEMS.md`, `docs/FEATURES.md`.

## Run-Until-Blocked Movement

**Block:** 9  **Status:** Implemented

**Keybinds:** `R` then `WASD`/arrow direction to run, `Escape` cancels the prefix

Run mode repeats normal cardinal `MoveAction` turns until interrupted. It preserves single-step movement controls, stops before walls, closed doors, occupants, adjacent chests/NPCs/stairs/items, visible or adjacent hostiles, low HP or damage taken, game over, invalid movement, or a safety cap, and reuses existing turn/event processing for every step.

**Files modified:** `Scripts/UI/InputHandler.cs`, `Scripts/Autoloads/GameManager.cs`, `Compat/Godot/GodotStubs.cs`, `Tests/UITests/UISmokeTests.cs`, `docs/SYSTEMS.md`, `docs/EVENTS.md`, `docs/FEATURES.md`.

## Rest-Until-Healed

**Block:** 10  **Status:** Implemented

**Keybinds:** `Z` rest until healed, interrupted, or safety capped

Rest mode repeats normal `WaitAction` turns while the player is injured and safe. It preserves single wait controls (`Space`/`.`), stops before spending a turn at full HP, visible or adjacent hostiles, low HP, dangerous poison/burning/corrosion status, game over, invalid wait, or a safety cap, and does not invent passive healing when the simulation has none.

**Files modified:** `Scripts/UI/InputHandler.cs`, `Scripts/Autoloads/GameManager.cs`, `Compat/Godot/GodotStubs.cs`, `Tests/UITests/UISmokeTests.cs`, `docs/SYSTEMS.md`, `docs/EVENTS.md`, `docs/FEATURES.md`.

## Autoexplore

**Block:** 11  **Status:** Implemented

**Keybinds:** `O` autoexplore until interrupted

Autoexplore recomputes deterministic BFS each step from the current player position. It prefers visible points of interest, then the nearest reachable frontier next to unexplored walkable space, and submits each move through the same `MoveAction`/`GameManager.ProcessPlayerAction` path as manual movement. It stops for visible or adjacent hostiles, damage taken, low HP, reaching a point of interest, no reachable frontier or visible point of interest, game over, invalid movement, or the safety cap.

**Files modified:** `Scripts/UI/InputHandler.cs`, `Scripts/Autoloads/GameManager.cs`, `Compat/Godot/GodotStubs.cs`, `Tests/UITests/UISmokeTests.cs`, `docs/SYSTEMS.md`, `docs/EVENTS.md`, `docs/FEATURES.md`.

## Planned UI Follow-ups

| Feature | Status | Notes |
|---|---|---|
| ChestUI `MenuBase` refactor | Planned | Align chest UI with shared menu chrome, footer hints, and close behavior. |
| CombatLog category filtering | Planned | Category colors exist; user-controlled category visibility does not. |
| QuickSlotHotbar class extraction | Implemented | Dedicated overlay exists; quick-use still routes through the existing action path. |
