# Events

`EventBus` is the Godot-facing notification bridge for simulation and UI state changes. Block 1 added tests that reconciled existing input wiring for rest, autoexplore, and run-prefix behavior; it did not add new `EventBus` events.

## EventBus Reference

| Event Name | Parameters | Emitted When | Typical Subscribers / Effects |
|---|---|---|---|
| `TurnStarted` | `int turnNumber` | A processed turn begins. | HUD/log refresh, turn-dependent UI. |
| `TurnCompleted` | none | A processed turn finishes. | UI refresh and input gating. |
| `EntityTurnStarted` | `EntityId entityId` | An actor begins its scheduled turn. | Presentation hooks for actor focus. |
| `PlayerActionSubmitted` | `IAction action` | UI submits a player action. | `GameManager` validates and processes the action. |
| `DamageDealt` | `DamageResult result` | Combat, ability, trap, or status damage resolves. | World animation, HUD, combat log. |
| `EntityDied` | `EntityId entityId` | An entity is killed and removed through the death pipeline. | World view cleanup, run stats, log/UI updates. |
| `StatusEffectApplied` | `EntityId entityId, StatusEffectInstance effect` | A status effect is added or refreshed. | HUD badges, entity status icons. |
| `StatusEffectRemoved` | `EntityId entityId, StatusEffectType effectType` | A status effect expires or is cleared. | HUD/entity status icon removal. |
| `FloorChanged` | `int depth` | Active floor changes. | HUD/minimap/world refresh. |
| `EntityMoved` | `EntityId entityId, Position from, Position to` | An entity changes position. | World rendering and movement animation. |
| `EntitySpawned` | `IEntity entity` | A runtime entity is created. | World renderer creates visual nodes. |
| `EntityRemoved` | `EntityId entityId` | A runtime entity is removed. | World renderer removes visual nodes. |
| `TileChanged` | `Position position` | A map tile changes, such as a door/trap state update. | World/minimap tile refresh. |
| `ItemPickedUp` | `EntityId entityId, ItemInstance item` | An entity picks up an item. | Inventory/HUD/log refresh. |
| `ItemDropped` | `EntityId entityId, ItemInstance item, Position position` | An entity drops an item. | Inventory and world item refresh. |
| `LogMessage` | `string message, LogCategory category` | Any system wants a categorized text log entry. | `CombatLog`, `MainMenu`, `DebugConsole`. |
| `InventoryChanged` | `EntityId entityId` | Inventory content changes. | HUD hotbar, inventory panel. |
| `HPChanged` | `EntityId entityId, int currentHp, int maxHp` | HP or max HP changes. | HUD bars and damage/heal presentation. |
| `SaveRequested` | `int slot` | UI requests save to a slot. | Save facade. |
| `LoadRequested` | `int slot` | UI requests load from a slot. | Load facade. |
| `SaveCompleted` | `bool success` | Save operation finishes. | UI status/log feedback. |
| `LoadCompleted` | `bool success` | Load operation finishes. | UI status/log feedback. |
| `FovRecalculated` | none | Visibility/exploration is recalculated. | World view and minimap refresh. |
| `LevelGenerated` | `int depth, int width, int height` | A level is generated. | Debug/UI diagnostics. |
| `LevelTransition` | `int fromDepth, int toDepth` | Floor travel begins. | UI transition and stats handling. |
| `EquipmentChanged` | `EntityId entityId, EquipSlot slot, ItemInstance? item` | Equipment in a slot changes. | Inventory, character sheet, HUD stat refresh. |
| `GameOver` | `int finalDepth, int turnsSurvived` | Legacy game-over notification. | Compatibility consumers. |
| `GameOverWithStats` | `RunStats stats` | Player death finalizes run stats. | `UIRoot` opens `GameOverScreen`. |
| `FloorSummaryReady` | `FloorStats stats` | Floor-local stats are finalized during travel. | `FloorSummaryUI`. |
| `FloorTransitionConfirmed` | none | Floor summary is dismissed. | Modal/input gating refresh. |
| `ExperienceGained` | `EntityId entityId, int amount, int total` | XP is awarded. | HUD/progression presentation. |
| `LeveledUp` | `EntityId entityId, int newLevel` | An entity gains a level. | Level-up overlay and progression UI. |
| `ProgressionChanged` | `EntityId entityId` | Progression state changes. | Character sheet/HUD refresh. |
| `CurrencyChanged` | `EntityId entityId, int gold` | Gold total changes. | HUD/inventory/shop refresh. |
| `TargetingModeEntered` | `string kind` | Targeting overlay opens. | World cursor/preview setup. |
| `TargetingModeExited` | `string reason` | Targeting overlay closes. | World cursor/preview cleanup. |
| `TargetingCursorMoved` | `Position position, bool isValid` | Targeting cursor moves or validity changes. | World targeting cursor. |
| `TargetingPreviewChanged` | `IReadOnlyList<Position> tiles, bool isValid` | Targeting AoE/preview changes. | World targeting preview. |
| `TrapTriggered` | `TrapTriggeredEventArgs args` | An armed trap triggers. | Animation/log/UI feedback. |

## InputHandler Routing

`InputHandler` converts keys into either direct action submissions, direct `GameManager` helper calls, or UI C# events. Rest, autoexplore, quick-use, and run-prefix execution are direct `InputHandler` methods in the current repo, not separate C# events.

| Keys | Route | Notes |
|---|---|---|
| Arrow keys, `WASD` | `HandleDirectionalInput(...)` | Submits move/attack actions, or completes run-prefix movement. |
| `R` | `EnterRunPrefix()` | Starts run-prefix mode; pressing `R` again while prefixed cancels. |
| `Escape` while run prefix active | Direct cancel | Cancels run prefix and logs cancellation. |
| `O` | `AutoExplore()` | Calls `GameManager.AutoExplorePlayer()`. |
| `Z` | `RestUntilHealed()` | Calls `GameManager.RestPlayerUntilHealed()`. |
| `Space`, `.` | `UIActionFactory.CreateWaitAction(...)` | Submits a normal wait action. |
| `G` | `UIActionFactory.CreatePickupAction(...)` | Submits pickup action. |
| `Enter`, `KpEnter` | `UIActionFactory.CreateStairsAction(...)` | Submits stairs action. |
| `1`-`5` | `HandleQuickUse(...)` | Uses HUD-derived quick-use candidates through normal item action creation. |
| `I` | `InventoryRequested` | UI event. |
| `C` | `CharacterSheetRequested` | UI event. |
| `E`, `F` | `InteractRequested` | UI event. |
| `H` | `HelpRequested` | UI event. |
| `T` | `ToolsRequested` | UI event. |
| `X` | `ExamineRequested` | UI event. |
| `Escape` | `PauseRequested` | UI event when not consumed by run-prefix/modal handling. |
| `M`, `Tab` | `MinimapToggleRequested` | UI event. |

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

## Autoexplore Movement

Autoexplore does not add a dedicated event. Each repeated step is processed as a normal `MoveAction` through `GameManager.ProcessPlayerAction`, so existing `TurnStarted`, `EntityMoved`, `DamageDealt`, `HPChanged`, `LogMessage`, `TurnCompleted`, and game-over notifications continue to describe the resulting state changes.
