# Keybinds

## Diagonal Movement And Combat

- `V`, then two perpendicular arrows or WASD directions: one diagonal move/attack. Example: `V`, `Up`, `Right` attacks or moves northeast without spending a preparatory turn.
- `V` or `Escape`: cancel a pending diagonal; another command clears it before opening its screen.
- `Home`, `Page Up`, `End`, `Page Down`: northwest, northeast, southwest, southeast.
- Numpad `1`-`9`: eight-direction movement; `5` waits. Number-row `1`-`5` remain quick-use slots.
- `R` then a cardinal direction still runs; diagonal shortcuts do not start diagonal autoplay.
- `F` beside an NPC opens conversation. Arrows/number-row choices and `Enter` navigate branches; `Escape`/`F` leaves. Field dressing clearly shows gold, healing cap, and turn cost before confirmation.

## Techniques

- `B`: open the owned-technique palette during normal gameplay.
- `1`-`4`: select a listed technique; `Enter` selects the highlighted technique; `B`/`Escape` closes.
- Self techniques execute through a normal turn. Aimed techniques reuse the targeting cursor: arrows/WASD move, `Enter` confirms, `Escape` cancels without consuming an action.
- The palette lists only the player’s class and heritage ability slots. Item scrolls remain inventory/quick-use items and are not injected into this list.

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
| Pick up | `G` | Picks up the item under the player. |
| Use stairs | `Enter`, `KpEnter` | Uses stairs at the player position. |
| Combat log filter | `L` | Cycles visible log categories: All, Combat, Loot, System. Stored messages are preserved. |
| Inventory | `I` | Opens inventory. |
| Character sheet | `C` | Opens character details. |
| Pause | `Escape` | Opens or closes the pause menu depending on modal state. |
| Minimap | `M`, `Tab` | Toggles minimap visibility. |
| Minimap legend | `U` | Toggles the minimap legend independently while normal gameplay input is active. |
| Examine | `X` | Enters examine mode at the player position. |
| Interact / pick up | `F` | Interacts with a nearby NPC, chest, or shrine when one has priority; otherwise, with `[F] Pick Up` shown in the HUD, submits the same pickup action as `G` for the item under the player. `E` is not a normal-gameplay interact key. |
| Help | `H` | Opens help. |
| Dev tools | `T` | Opens the development tools workbench. |
| Debug console | `` ` `` | Opens the debug console in `DEBUG` builds only. |
| Debug overlay | `Q` | Toggles the debug overlay in `DEBUG` builds only. |

## Main Menu / Character Creation

| Action | Keys | Notes |
|---|---|---|
| Deploy / confirm selected action | `Enter`, `KpEnter` | Starts a run when Start Expedition is selected, or activates the selected menu action. |
| Move selection | Arrow keys, `WASD` | Moves through character creation and system options. |
| Adjust highlighted field | Left/Right, `+`, `-` | Cycles name, archetype, origin, trait, identity options, and training points where applicable. Seed uses typed entry. |
| Starter Kit tooltip | `Tab` | Toggles the starter-kit tooltip with Equipped/Pack explanations, content-backed item names/descriptions, stack counts, and targeting notes. |
| Help | `H` | Opens main-menu help. |
| Dev tools | `T` | Opens the development tools workbench. |

## Developer Workshop

| Action | Keys | Notes |
|---|---|---|
| Switch tab | `Tab` | Cycle Rooms, Items, Enemies, and Commands. |
| Select row | Up/Down, `W`/`S` | Move through fields and actions. |
| Adjust value | Left/Right, `A`/`D`, `+`/`-` | Fine-adjust the selected value. |
| Apply/edit | `Enter`, `KpEnter` | Runs an action; seed and export-depth rows enter exact numeric editing. |
| Edit number | Digits, `Backspace`, `Delete`, `Enter`, `Escape` | Type, erase, clear, commit, or cancel seed/depth input. |
| Close | `Escape`, `T` | Escape cancels numeric editing first; otherwise closes the workshop. |

## Examine Mode

| Action | Keys | Notes |
|---|---|---|
| Move examine cursor | Arrow keys, `WASD` | Moves the cursor without submitting gameplay actions. |
| Exit examine mode | `X`, `Escape` | Returns to normal input. |

## Modal Basics

| Modal | Common Keys | Notes |
|---|---|---|
| Inventory | Arrow keys, `Enter`/`U` use, `Enter`/`E` equip, `D` drop, `A` auto-equip toggle, `Tab` sort, `I`/`Escape` close | Use/equip/drop behavior follows the inventory footer hints. Aimed scrolls enter targeting instead of being consumed blindly. |
| Targeting | Arrow keys/`WASD`, `Enter`, `Escape` | Move cursor, confirm target, or cancel safely. |
| Chest | Arrow keys/selection keys, `Enter`, `F`, `Escape` | `Enter` takes all from the chest; `F` or `Escape` closes the chest panel. |
| Dialog / shop | Arrow keys/selection keys, `Enter`, `Escape` | Follow the panel footer hints for selection, purchase, and close behavior. |
| Level up | Up/Down, `Enter`/Right, `Escape` | Choose a perk, confirm it, or close while keeping pending choices intact. |
| Floor summary | `Enter`, `Space`, `Escape` | Continues after a floor transition summary. Other keys stop the auto-countdown without closing it. |
| Game over | `Enter`, `Escape` | Starts a new run or returns to menu, depending on screen state. |
