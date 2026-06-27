# Keybinds

This page reflects the current repository wiring in `InputHandler` and the active UI panels. Older references to `Shift`+direction for running and `R` for rest are stale: running is `R` then a direction, and rest-until-healed is `Z`.

## Normal Gameplay

| Action | Keys | Notes |
|---|---|---|
| Move / melee adjacent target | Arrow keys, `WASD` | Cardinal movement only. Moving into an adjacent hostile submits the normal attack path. |
| Run until blocked | `R`, then arrow key or `WASD` | `Escape` or `R` cancels the run prefix before a direction is chosen. |
| Wait one turn | `Space`, `.` | Submits a normal wait action. |
| Rest until healed | `Z` | Repeats waits until healed, unsafe, interrupted, or safety-capped. |
| Autoexplore | `O` | Repeats movement toward points of interest or frontier tiles until interrupted. |
| Quick-use item | `1`-`5` | Implemented. Uses the first five derived usable inventory entries shown by the quick-slot hotbar when safe. Aimed items must be targeted from inventory. |
| Pick up | `G` | Picks up items from the current tile. |
| Use stairs | `Enter`, `KpEnter` | Uses stairs at the player position. |
| Inventory | `I` | Opens inventory. |
| Character sheet | `C` | Opens character details. |
| Pause | `Escape` | Opens or closes the pause menu depending on modal state. |
| Minimap | `M`, `Tab` | Toggles minimap visibility. |
| Minimap legend | `U` | Toggles the minimap legend independently while normal gameplay input is active. |
| Examine | `X` | Enters examine mode at the player position. |
| Interact | `E`, `F` | Interacts with nearby NPCs, chests, or contextual objects. |
| Help | `H` | Opens help. |
| Dev tools | `T` | Opens the development tools workbench. |
| Debug console | `` ` `` | Opens debug console where available. |
| Debug overlay | `Q` | Toggles debug overlay where available. |

## Examine Mode

| Action | Keys | Notes |
|---|---|---|
| Move examine cursor | Arrow keys, `WASD` | Moves the cursor without submitting gameplay actions. |
| Exit examine mode | `X`, `Escape` | Returns to normal input. |

## Modal Basics

| Modal | Common Keys | Notes |
|---|---|---|
| Inventory | Arrow keys/selection keys, `Enter`, `Escape` | Use/equip/drop behavior follows the inventory footer hints. Aimed scrolls enter targeting instead of being consumed blindly. |
| Targeting | Arrow keys/`WASD`, `Enter`, `Escape` | Move cursor, confirm target, or cancel safely. |
| Chest | Arrow keys/selection keys, `Enter`, `Escape` | Chest UI is functional but still planned for a `MenuBase` refactor. |
| Dialog / shop | Arrow keys/selection keys, `Enter`, `Escape` | Follow the panel footer hints for selection, purchase, and close behavior. |
| Floor summary | `Enter`, `Space` | Continues after a floor transition summary. |
| Game over | `Enter`, `Escape` | Starts a new run or returns to menu, depending on screen state. |
