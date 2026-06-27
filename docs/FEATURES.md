# Features

## Death Screen

**Block:** 1  **Status:** Implemented

**Keybinds:** `Enter` new run, `Escape` main menu

The death screen displays run stats from `GameManager.CurrentRunStats`, including floor reached, turn count, kills, gold, items found, damage taken, seed, cause of death, and best item. It is surfaced through `EventBus.GameOverWithStats` while preserving the older `GameOver` signal.

**Files modified:** `Scripts/UI/GameOverScreen.cs`, `Scripts/Autoloads/GameManager.cs`, `Scripts/Autoloads/EventBus.cs`, `Scripts/UI/UIRoot.cs`, `Tests/UITests/GameOverScreenTests.cs`.
