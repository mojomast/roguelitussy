# Events

## Game Over

| Event Name | Parameters | Emitted When | Subscribers |
|---|---|---|---|
| `GameOverWithStats` | `RunStats stats` | The player dies and the current run summary has been finalized. | `UIRoot` opens `GameOverScreen`. |

`GameOverWithStats` is additive. The existing `GameOver(int finalDepth, int turnsSurvived)` event remains for compatibility.
