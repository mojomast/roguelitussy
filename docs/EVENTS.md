# Events

## Game Over

| Event Name | Parameters | Emitted When | Subscribers |
|---|---|---|---|
| `GameOverWithStats` | `RunStats stats` | The player dies and the current run summary has been finalized. | `UIRoot` opens `GameOverScreen`. |
| `FloorSummaryReady` | `FloorStats stats` | The player leaves a floor and the floor-local counters have been finalized, just before the destination floor is loaded. | `FloorSummaryUI` opens the summary panel. |
| `FloorTransitionConfirmed` | none | The floor summary is dismissed by Enter/Space or its auto-dismiss timer. | `UIRoot` refreshes modal input gating. |

`GameOverWithStats` is additive. The existing `GameOver(int finalDepth, int turnsSurvived)` event remains for compatibility.
