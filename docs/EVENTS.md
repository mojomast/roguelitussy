# Events

## UI Log

| Event Name | Parameters | Emitted When | Subscribers |
|---|---|---|---|
| `LogMessage` | `string message, LogCategory category` | Gameplay or presentation systems need to append a textual event to the combat/debug log. `EmitLogMessage(string)` remains available and emits `LogCategory.System`. | `CombatLog`, `MainMenu`, `DebugConsole`. |

`LogCategory` values are `System`, `PlayerAction`, `EnemyAction`, `Loot`, `StatusEffect`, `Warning`, and `Critical`. The combat log uses them for color, bold critical entries, and age fading.

## Game Over

| Event Name | Parameters | Emitted When | Subscribers |
|---|---|---|---|
| `GameOverWithStats` | `RunStats stats` | The player dies and the current run summary has been finalized. | `UIRoot` opens `GameOverScreen`. |
| `FloorSummaryReady` | `FloorStats stats` | The player leaves a floor and the floor-local counters have been finalized, just before the destination floor is loaded. | `FloorSummaryUI` opens the summary panel. |
| `FloorTransitionConfirmed` | none | The floor summary is dismissed by Enter/Space or its auto-dismiss timer. | `UIRoot` refreshes modal input gating. |

`GameOverWithStats` is additive. The existing `GameOver(int finalDepth, int turnsSurvived)` event remains for compatibility.

## Run Movement

Run-until-blocked movement does not add a dedicated event. Each repeated step is processed as a normal `MoveAction` through `GameManager.ProcessPlayerAction`, so existing `TurnStarted`, `EntityMoved`, `DamageDealt`, `HPChanged`, `LogMessage`, and `TurnCompleted` notifications continue to describe the resulting state changes.

## Rest Movement

Rest-until-healed does not add a dedicated event. Each repeated wait is processed as a normal `WaitAction` through `GameManager.ProcessPlayerAction`, so existing `TurnStarted`, `DamageDealt`, `HPChanged`, `LogMessage`, `TurnCompleted`, and game-over notifications continue to describe the resulting state changes.
