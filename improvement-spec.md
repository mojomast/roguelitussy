# GPT Branch Improvement Spec — Incorporating Opus Strengths

## Purpose

This spec defines **six targeted improvements** to the `gpt` branch. Each task is independent and can be assigned to a subagent in parallel. The goal is to harden the GPT branch's already-strong feature set with the architectural advantages demonstrated by the Opus branch — without breaking existing tests or features.

**Ground rules for all agents:**
- Do NOT delete existing working code unless this spec explicitly says to replace it.
- All 83 existing tests must continue passing after your changes.
- Use namespace `Roguelike.Core` for all pure C# (Core/) files. Use namespace `Godotussy` for all Scripts/ files.
- The Godot SDK reference is `Godot.NET.Sdk/4.4.0`. Scripts/ files may reference Godot types. Core/ files must NOT.
- Run `dotnet run --project Tests/godotussy.Tests.csproj` to verify tests after changes.

---

## Task 1 — Extract Pure C# GameLoop from GameManager

**Problem:** The turn loop is currently embedded in `Scripts/Autoloads/GameManager.cs` via `ProcessPlayerAction()`. This couples simulation logic to Godot, making the core untestable in isolation and violating the spec's "pure C# simulation core" pillar.

**Deliverable:** Create `Core/Simulation/GameLoop.cs`

### Requirements

1. Create a new file `Core/Simulation/GameLoop.cs` with a `GameLoop` class in namespace `Roguelike.Core`.

2. The class must have this signature:
   ```csharp
   public sealed class GameLoop
   {
       public ActionOutcome ProcessRound(
           WorldState world,
           ITurnScheduler scheduler,
           Func<IEntity, IAction> getAction)
   }
   ```

3. `ProcessRound()` orchestrates one full round:
   - Call `scheduler.BeginRound(world)`
   - Loop: while `scheduler.HasNextActor()`, get actor, call `getAction(actor)` to get the action, validate it, execute it, call `scheduler.ConsumeEnergy()`.
   - Accumulate all `CombatEvents`, `LogMessages`, and `DirtyPositions` from each action's outcome into a single aggregate `ActionOutcome`.
   - After all actors have acted, tick status effects for every living entity.
   - Call `scheduler.EndRound(world)`.
   - Return the aggregate `ActionOutcome`.

4. Failed validations should still consume energy and log a message (`"{actor.Name}: action {action.Type} failed ({validation})"`), but should NOT execute the action.

5. **Refactor `GameManager.ProcessPlayerAction()`** to delegate to `GameLoop.ProcessRound()`. GameManager becomes a thin Godot-side coordinator that:
   - Calls `_gameLoop.ProcessRound(World, Scheduler, GetActionForEntity)` where `GetActionForEntity` returns the player's action for the player entity and AI brain decisions for NPCs.
   - Emits all EventBus signals from the returned `ActionOutcome` (damage, death, movement, inventory changes, etc.) — the same signals it emits today.
   - The EventBus emission code that currently lives in `ProcessPlayerAction` stays in GameManager, just operating on the returned outcome rather than inline.

6. **Do NOT change any EventBus signatures or event names.**

### New Tests

Add `Tests/SimulationTests/GameLoopTests.cs`:
- `GameLoop_ProcessRound_ExecutesAllActors` — 3 entities (speed 100), each gets a WaitAction, verify all 3 consumed energy.
- `GameLoop_ProcessRound_FailedValidation_StillConsumesEnergy` — give an actor an invalid MoveAction (into wall), verify energy consumed and failure logged.
- `GameLoop_ProcessRound_AggregatesOutcomes` — one actor attacks another, verify returned outcome contains the CombatEvent and DirtyPositions.
- `GameLoop_ProcessRound_TicksStatusEffects` — apply poison to an entity, run a round, verify HP decreased.

---

## Task 2 — Fix EntityId Determinism

**Problem:** `EntityId.New()` calls `Guid.NewGuid()` which is non-deterministic. Every entity spawned during dungeon generation gets a random GUID, meaning the same seed produces different entity IDs across runs. This breaks save/load consistency and replay determinism.

**Deliverable:** Modify `Core/Contracts/Types/EntityId.cs` and entity creation callsites.

### Requirements

1. Add a new factory method to `EntityId`:
   ```csharp
   /// <summary>
   /// Creates a deterministic EntityId from a seeded Random instance.
   /// Use this for all entity spawning during gameplay/generation.
   /// </summary>
   public static EntityId NewSeeded(Random rng)
   {
       Span<byte> bytes = stackalloc byte[16];
       rng.NextBytes(bytes);
       return new EntityId(new Guid(bytes));
   }
   ```

2. **Keep `EntityId.New()` as-is** — it's still useful for test stubs and non-simulation contexts (UI placeholders, etc.). Add a comment marking it as non-deterministic:
   ```csharp
   /// <summary>
   /// Creates a non-deterministic EntityId. Do NOT use in simulation paths.
   /// Use <see cref="NewSeeded"/> for gameplay entity creation.
   /// </summary>
   public static EntityId New() => new(Guid.NewGuid());
   ```

3. Update all entity creation in **simulation paths** to use `NewSeeded(rng)`:
   - `Core/Generation/DungeonGenerator.cs` — when spawning player entity, enemy entities, and item entities during level generation. The `Random rng` is already available (seeded from `seed ^ depth`).
   - `Core/Simulation/Entity.cs` — if the constructor calls `EntityId.New()`, change it to accept an `EntityId` parameter instead.
   - `Core/Simulation/Actions/DropItemAction.cs` — if it creates a ground entity with a new ID.
   - Any other file in `Core/` that calls `EntityId.New()`.

4. **Do NOT change** EntityId usage in:
   - Test stubs (`Tests/Stubs/`)
   - Scripts/ files (Godot layer)
   - Deserialization paths (`EntityId.From()`)

### Verification

- Existing `DungeonGeneratorTests.SameSeedProducesSameLevel` should now also produce identical entity IDs (add an assertion for this).
- Add test: `EntityId_NewSeeded_SameSeed_ProducesSameId` — two calls to `NewSeeded` with identically-seeded Random instances produce the same EntityId.

---

## Task 3 — Add DamagePopup.cs to Scripts/World

**Problem:** The spec requires floating damage numbers. The GPT branch is missing `Scripts/World/DamagePopup.cs`.

**Deliverable:** Create `Scripts/World/DamagePopup.cs` and integrate it.

### Requirements

1. Create `Scripts/World/DamagePopup.cs`:
   ```csharp
   using Godot;

   namespace Godotussy;

   public partial class DamagePopup : Label
   {
       private const float Duration = 0.8f;
       private const float RiseDistance = 24f;

       private static readonly Color DamageColor = new(1f, 0.2f, 0.2f);
       private static readonly Color HealColor = new(0.2f, 1f, 0.3f);
       private static readonly Color CritColor = new(1f, 1f, 0f);

       public void Setup(int amount, bool isCrit, bool isHeal)
       {
           Text = isHeal ? $"+{amount}" : $"{amount}";

           if (isHeal)
               AddThemeColorOverride("font_color", HealColor);
           else if (isCrit)
               AddThemeColorOverride("font_color", CritColor);
           else
               AddThemeColorOverride("font_color", DamageColor);

           if (isCrit)
               Scale = Vector2.One * 1.5f;

           HorizontalAlignment = HorizontalAlignment.Center;
           VerticalAlignment = VerticalAlignment.Center;
           ZIndex = 100;

           var tween = CreateTween();
           tween.SetParallel(true);
           tween.TweenProperty(this, "position:y", Position.Y - RiseDistance, Duration)
               .SetEase(Tween.EaseType.Out);
           tween.TweenProperty(this, "modulate:a", 0f, Duration)
               .SetEase(Tween.EaseType.In)
               .SetDelay(Duration * 0.4f);
           tween.SetParallel(false);
           tween.TweenCallback(Callable.From(QueueFree));
       }
   }
   ```

2. Create `Scenes/UI/DamagePopup.tscn` if it doesn't already exist — a minimal scene with a `DamagePopup` (Label) root node. The scene should have the script attached.

3. **Integrate with `Scripts/World/AnimationController.cs`:** Add a method to spawn a DamagePopup at a world position when damage is dealt. The AnimationController should:
   - Accept a `PackedScene` reference for the DamagePopup scene.
   - On `AnimateDamage()` or equivalent, instantiate the popup, position it at the entity's canvas position, call `Setup()`, and add it to the scene tree.

4. **Wire EventBus:** In `Scripts/World/WorldView.cs`, when `DamageDealt` fires, spawn a DamagePopup at the defender's position showing the damage amount (or "MISS" for misses, with a different color).

---

## Task 4 — Add Missing EventBus Signals

**Problem:** The Opus branch has a more comprehensive EventBus with signals the GPT branch lacks, particularly around turn lifecycle and level transitions.

**Deliverable:** Extend `Scripts/Autoloads/EventBus.cs`

### Requirements

Add these missing events to the existing EventBus (the GPT branch already has 20 events — add only what's missing):

```csharp
// --- Turn lifecycle (GPT has TurnCompleted but not these) ---
public event Action<int>? TurnStarted;               // turnNumber
public event Action<EntityId>? EntityTurnStarted;     // entityId

// --- Level transitions (GPT has FloorChanged but not these) ---
public event Action<int, int, int>? LevelGenerated;   // depth, width, height
public event Action<int, int>? LevelTransition;       // fromDepth, toDepth

// --- Equipment ---
public event Action<EntityId, EquipSlot, ItemInstance?>? EquipmentChanged;  // entityId, slot, item (null = unequipped)

// --- Game over ---
public event Action<int, int>? GameOver;              // finalDepth, turnsSurvived
```

Add corresponding `Emit*()` methods for each.

**Wiring:**
- `TurnStarted` — emit at the beginning of `GameManager`'s round processing (after `scheduler.BeginRound()`), passing `world.TurnNumber`.
- `EntityTurnStarted` — emit inside the GameLoop callback (or in GameManager) before each entity's action is processed.
- `LevelGenerated` — emit after dungeon generation completes.
- `LevelTransition` — emit when `TransitionFloor()` is called, passing old and new depth.
- `GameOver` — emit when player dies, passing depth and turn number.
- `EquipmentChanged` — emit when inventory equipment changes.

**Do NOT rename or remove any existing events.**

---

## Task 5 — Optimize Save Serialization (Bit-Packed Explored State)

**Problem:** The GPT branch's `SaveSerializer.EncodeFlags()` uses 1 byte per boolean for the explored/visible arrays. For an 80×50 map, that's 4000 bytes base64-encoded. Bit-packing (8 bools per byte) reduces this to 500 bytes — an 8× compression improvement.

**Deliverable:** Update `Core/Persistence/SaveSerializer.cs`

### Requirements

1. Replace `EncodeFlags()` with bit-packed encoding:
   ```csharp
   private static string EncodeFlags(IReadOnlyList<bool> flags)
   {
       int byteCount = (flags.Count + 7) / 8;
       var bytes = new byte[byteCount];
       for (int i = 0; i < flags.Count; i++)
       {
           if (flags[i])
               bytes[i / 8] |= (byte)(1 << (i % 8));
       }
       return Convert.ToBase64String(bytes);
   }
   ```

2. Replace `DecodeFlags()` with bit-packed decoding:
   ```csharp
   private static bool[] DecodeFlags(string base64, int width, int height)
   {
       var bytes = Convert.FromBase64String(base64);
       int totalCells = checked(width * height);
       var flags = new bool[totalCells];
       for (int i = 0; i < totalCells; i++)
       {
           flags[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
       }
       return flags;
   }
   ```

3. Update `DecodeFlagBytes()` validation to expect the bit-packed length:
   ```csharp
   int expectedLength = (checked(width * height) + 7) / 8;
   ```

4. **Bump `SaveSerializer.CurrentVersion`** from 2 to 3.

5. **Add migration in `SaveMigrator.cs`:** Version 2 saves use 1-byte-per-bool encoding. When loading a version 2 save, convert the old `Explored` and `Visible` fields to the new bit-packed format before processing. The migration should:
   - Read the old byte-per-bool base64 strings.
   - Re-encode them as bit-packed base64.
   - Update the version number to 3.

### New Tests

Add to `Tests/PersistenceTests/`:
- `BitPackedFlags_RoundTrip` — encode a bool array with mixed true/false, decode it, verify identical.
- `BitPackedFlags_AllFalse` — encode all-false array, verify decodes correctly.
- `BitPackedFlags_AllTrue` — encode all-true array, verify decodes correctly.
- `SaveMigration_V2ToV3` — create a V2 save string (old encoding), load it, verify world state matches.

---

## Task 6 — Add DamagePopup Scene and Minimap Signal

**Problem (minor):** While Task 3 creates the DamagePopup script, we also need the `.tscn` scene file and should verify Minimap has the `LevelGenerated` signal it needs (from Task 4).

**This task is OPTIONAL and only needed if Task 3's scene creation wasn't fully completed.**

---

## Execution Order

Tasks 1–5 are independent and can run in parallel. Task 6 is a cleanup pass.

| Task | Files Created/Modified | Risk |
|------|----------------------|------|
| 1 — GameLoop extraction | Create `Core/Simulation/GameLoop.cs`, modify `Scripts/Autoloads/GameManager.cs`, create `Tests/SimulationTests/GameLoopTests.cs` | Medium — touches core loop |
| 2 — EntityId determinism | Modify `Core/Contracts/Types/EntityId.cs`, `Core/Generation/DungeonGenerator.cs`, `Core/Simulation/Entity.cs`, possibly `Actions/DropItemAction.cs` | Low — additive change |
| 3 — DamagePopup | Create `Scripts/World/DamagePopup.cs`, `Scenes/UI/DamagePopup.tscn`, modify `Scripts/World/AnimationController.cs`, `Scripts/World/WorldView.cs` | Low — new feature, no breakage |
| 4 — EventBus signals | Modify `Scripts/Autoloads/EventBus.cs`, `Scripts/Autoloads/GameManager.cs` | Low — additive |
| 5 — Save compression | Modify `Core/Persistence/SaveSerializer.cs`, `Core/Persistence/SaveMigrator.cs`, create tests | Medium — changes save format |

## Validation Checklist

After all tasks complete, verify:
- [ ] `dotnet build godotussy.csproj` — 0 errors, 0 warnings
- [ ] `dotnet run --project Tests/godotussy.Tests.csproj` — all tests pass (original 83 + new tests)
- [ ] `Core/Simulation/GameLoop.cs` exists and is used by GameManager
- [ ] `EntityId.NewSeeded(rng)` exists and is used in DungeonGenerator
- [ ] `Scripts/World/DamagePopup.cs` exists with tween animations
- [ ] EventBus has `TurnStarted`, `EntityTurnStarted`, `LevelGenerated`, `LevelTransition`, `GameOver`, `EquipmentChanged`
- [ ] Save format version is 3 with bit-packed flags
- [ ] V2 → V3 save migration works
