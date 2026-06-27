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
