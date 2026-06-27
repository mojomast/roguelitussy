# Features

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

The HUD shows a small runtime hotbar derived from the first five usable non-equipment inventory entries. Number keys submit the same `UseItemAction` path as inventory use for safe consumables such as potions. Aimed scrolls remain visible but marked `(aim)` and are not consumed blindly; pressing their hotkey logs a warning and instructs the player to aim them from inventory.

**Files modified:** `Scripts/UI/HUD.cs`, `Scripts/UI/InputHandler.cs`, `Scripts/UI/UIActionFactory.cs`, `Tests/UITests/UISmokeTests.cs`, `docs/SYSTEMS.md`, `docs/FEATURES.md`.

## Minimap Legend

**Block:** 7  **Status:** Implemented

**Keybinds:** `M`/`Tab` toggle minimap visibility during normal gameplay

The minimap keeps the existing HUD summary and gameplay toggle behavior, but now reserves a compact legend band inside the overlay. The legend identifies floor, door, stairs, trap, player, enemy, NPC, item, and chest markers. Visible enemies, NPCs, items, and chests draw small colored markers over explored tile colors; trap, door, and stair tiles keep distinct colors aligned with the legend.

**Files modified:** `Scripts/UI/Minimap.cs`, `Scripts/UI/UiStyle.cs`, `Tests/UITests/UISmokeTests.cs`, `docs/SYSTEMS.md`, `docs/FEATURES.md`.
