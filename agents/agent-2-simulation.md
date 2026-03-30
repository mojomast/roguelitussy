# Agent 2: Simulation Agent — Detailed Specification

## Mission
Implement the complete turn-based simulation engine: WorldState (grid + entities), the action system, energy-based turn scheduler, combat resolver, and status effect system. All pure C# with no Godot dependencies. Fully testable in isolation.

---

## 1. Files to Create

| File | Purpose |
|------|---------|
| `Core/Simulation/WorldState.cs` | Grid storage, entity registry, spatial queries |
| `Core/Simulation/TurnScheduler.cs` | Energy-based turn ordering |
| `Core/Simulation/CombatResolver.cs` | Hit/damage/armor formulas |
| `Core/Simulation/StatusEffectProcessor.cs` | Tick, stack, expire effects |
| `Core/Simulation/Actions/MoveAction.cs` | Movement with collision |
| `Core/Simulation/Actions/AttackAction.cs` | Melee attack |
| `Core/Simulation/Actions/PickUpAction.cs` | Pick up ground item |
| `Core/Simulation/Actions/UseItemAction.cs` | Consume/apply item |
| `Core/Simulation/Actions/WaitAction.cs` | Skip turn |
| `Core/Simulation/Actions/DropItemAction.cs` | Drop inventory item |
| `Core/Simulation/Actions/UseStairsAction.cs` | Floor transition |
| `Core/Simulation/Actions/OpenDoorAction.cs` | Open/close doors |
| `Tests/SimulationTests/WorldStateTests.cs` | Unit tests |
| `Tests/SimulationTests/TurnSchedulerTests.cs` | Unit tests |
| `Tests/SimulationTests/CombatResolverTests.cs` | Unit tests |
| `Tests/SimulationTests/ActionTests.cs` | Unit tests |
| `Tests/SimulationTests/StatusEffectTests.cs` | Unit tests |

---

## 2. WorldState Implementation

### 2.1 Grid Storage

```csharp
public class WorldState : IWorldState
{
    private TileType[,] _tiles;       // [x, y] indexed
    private bool[,] _explored;        // fog-of-war memory
    private readonly Dictionary<int, EntityData> _entities = new();
    private readonly Dictionary<Vec2I, int> _positionIndex = new();       // pos → entityId (one entity per tile)
    private readonly Dictionary<Vec2I, List<string>> _groundItems = new(); // pos → item IDs on ground
    private int _nextEntityId = 1;

    public int Width { get; }
    public int Height { get; }
    public int CurrentFloor { get; set; }
    public Vec2I StairsDownPos { get; set; }
    public Vec2I StairsUpPos { get; set; }
}
```

**Grid rules:**
- Map dimensions: configurable, default 80×50.
- Coordinate system: (0,0) is top-left. X increases right, Y increases down.
- Every cell holds exactly one `TileType`.
- At most one entity may occupy a cell at a time (`_positionIndex` enforces this).
- Multiple ground items can share a cell.

### 2.2 Entity Registry

```csharp
public int SpawnEntity(EntityData data, Vec2I pos)
{
    if (!IsInBounds(pos)) throw new ArgumentOutOfRangeException(nameof(pos));
    if (!IsWalkable(pos)) throw new InvalidOperationException($"Cannot spawn at non-walkable {pos}");
    if (_positionIndex.ContainsKey(pos)) throw new InvalidOperationException($"Tile {pos} already occupied");

    data.Id = _nextEntityId++;
    data.Position = pos;
    _entities[data.Id] = data;
    _positionIndex[pos] = data.Id;
    return data.Id;
}

public void RemoveEntity(int entityId)
{
    if (_entities.TryGetValue(entityId, out var entity))
    {
        _positionIndex.Remove(entity.Position);
        _entities.Remove(entityId);
    }
}

public void MoveEntity(int entityId, Vec2I newPos)
{
    var entity = _entities[entityId];
    _positionIndex.Remove(entity.Position);
    entity.Position = newPos;
    _positionIndex[newPos] = entityId;
}
```

### 2.3 Spatial Queries

```csharp
public EntityData? GetEntityAt(Vec2I pos)
{
    return _positionIndex.TryGetValue(pos, out var id) ? _entities[id] : null;
}

public IEnumerable<EntityData> GetEntitiesInRadius(Vec2I center, int radius)
{
    // Use Chebyshev distance for grid-based radius
    foreach (var entity in _entities.Values)
    {
        if (center.ChebyshevDistance(entity.Position) <= radius)
            yield return entity;
    }
}

public bool IsWalkable(Vec2I pos)
{
    if (!IsInBounds(pos)) return false;
    var tile = _tiles[pos.X, pos.Y];
    return tile is TileType.Floor or TileType.StairsDown or TileType.StairsUp or TileType.DoorOpen;
}

public bool IsOpaque(Vec2I pos)
{
    if (!IsInBounds(pos)) return true; // Out of bounds = opaque
    var tile = _tiles[pos.X, pos.Y];
    return tile is TileType.Wall or TileType.DoorClosed;
}
```

### 2.4 Initialization from LevelBlueprint

```csharp
public void LoadFromBlueprint(LevelBlueprint bp)
{
    _tiles = bp.Tiles;
    _explored = new bool[bp.Width, bp.Height];
    Width = bp.Width;  // store via backing field
    Height = bp.Height;
    StairsDownPos = bp.StairsDown;
    StairsUpPos = bp.StairsUp;
    CurrentFloor = bp.Floor;

    _entities.Clear();
    _positionIndex.Clear();
    _groundItems.Clear();
    _nextEntityId = 1;
}
```

---

## 3. Action System

Every game action implements `IAction`. All mutation goes through actions — no direct state modification from outside.

### 3.1 MoveAction

```csharp
public class MoveAction : IAction
{
    public ActionType Type => ActionType.Move;
    public int ActorId { get; }
    public Direction Dir { get; }
    public int EnergyCost => 1000; // base move cost

    public bool Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null) return false;
        var target = actor.Position + Vec2I.FromDirection(Dir);
        if (!world.IsInBounds(target)) return false;
        if (!world.IsWalkable(target)) return false;
        if (world.GetEntityAt(target) != null) return false; // blocked by entity
        return true;
    }

    public ActionResult Execute(IWorldState world, ICombatResolver combat)
    {
        var actor = world.GetEntity(ActorId)!;
        var from = actor.Position;
        var target = from + Vec2I.FromDirection(Dir);
        world.MoveEntity(ActorId, target);
        return new ActionResult(true, $"{actor.Name} moves {Dir}", EnergyCost);
    }
}
```

**Diagonal movement**: costs same energy as cardinal. Diagonal movement blocked if BOTH adjacent cardinal tiles are walls (no corner-cutting).

```csharp
// Diagonal corner-cutting check (add to Validate):
if (Dir is Direction.NE or Direction.NW or Direction.SE or Direction.SW)
{
    var (dx, dy) = (Vec2I.FromDirection(Dir).X, Vec2I.FromDirection(Dir).Y);
    var adjX = actor.Position + new Vec2I(dx, 0);
    var adjY = actor.Position + new Vec2I(0, dy);
    if (!world.IsWalkable(adjX) && !world.IsWalkable(adjY)) return false;
}
```

### 3.2 AttackAction

```csharp
public class AttackAction : IAction
{
    public ActionType Type => ActionType.Attack;
    public int ActorId { get; }
    public int TargetId { get; }
    public int EnergyCost => 1000;

    public bool Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        var target = world.GetEntity(TargetId);
        if (actor == null || target == null) return false;
        if (actor.Faction == target.Faction) return false; // no friendly fire
        if (actor.Position.ChebyshevDistance(target.Position) > 1) return false; // melee range
        return true;
    }

    public ActionResult Execute(IWorldState world, ICombatResolver combat)
    {
        var actor = world.GetEntity(ActorId)!;
        var target = world.GetEntity(TargetId)!;
        var dmgEvent = combat.ResolveMeleeAttack(actor, target);

        target.HP -= dmgEvent.FinalDamage;

        var events = new List<DamageEvent> { dmgEvent };
        string msg;

        if (target.HP <= 0)
        {
            world.RemoveEntity(TargetId);
            msg = $"{actor.Name} kills {target.Name} for {dmgEvent.FinalDamage} damage!";
        }
        else if (dmgEvent.Hit)
        {
            msg = $"{actor.Name} hits {target.Name} for {dmgEvent.FinalDamage} damage.";
        }
        else
        {
            msg = $"{actor.Name} misses {target.Name}.";
        }

        return new ActionResult(true, msg, EnergyCost, events);
    }
}
```

**Bump-to-attack**: When player moves into an enemy tile, GameManager converts the MoveAction into an AttackAction automatically:
```csharp
// In GameManager, before executing a MoveAction:
if (action is MoveAction move)
{
    var actor = World.GetEntity(move.ActorId);
    var target = actor.Position + Vec2I.FromDirection(move.Dir);
    var blocker = World.GetEntityAt(target);
    if (blocker != null && blocker.Faction != actor.Faction)
    {
        action = new AttackAction(move.ActorId, blocker.Id);
    }
}
```

### 3.3 PickUpAction

```csharp
public class PickUpAction : IAction
{
    public ActionType Type => ActionType.PickUp;
    public int ActorId { get; }
    public int EnergyCost => 500; // picking up is fast

    public bool Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null) return false;
        if (actor.Inventory.Count >= actor.MaxInventorySize) return false;
        var item = world.PickUpItem(actor.Position); // peek, don't consume during validation
        // Actually: use a separate HasItemAt check
        return world.GetGroundItems().Any(gi => gi.pos == actor.Position);
    }

    public ActionResult Execute(IWorldState world, ICombatResolver combat)
    {
        var actor = world.GetEntity(ActorId)!;
        var itemId = world.PickUpItem(actor.Position);
        if (itemId == null) return new ActionResult(false, "Nothing to pick up.", 0);
        actor.Inventory.Add(itemId);
        return new ActionResult(true, $"{actor.Name} picks up {itemId}.", EnergyCost);
    }
}
```

### 3.4 UseItemAction

```csharp
public class UseItemAction : IAction
{
    public ActionType Type => ActionType.UseItem;
    public int ActorId { get; }
    public string ItemId { get; }
    public int EnergyCost => 1000;

    public bool Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null) return false;
        return actor.Inventory.Contains(ItemId);
    }

    public ActionResult Execute(IWorldState world, ICombatResolver combat)
    {
        var actor = world.GetEntity(ActorId)!;
        // Item effects are resolved by looking up ItemData from ContentDatabase
        // For simulation purposes, item effects are encoded in ItemData:
        // - HealAmount > 0: restore HP
        // - AppliesEffect != null: apply status effect
        // - Equippable: equip to weapon/armor slot
        // - Consumable: remove from inventory after use

        // This method needs IContentDB passed in or available.
        // DESIGN: Pass item data as a parameter or resolve via a static/injected registry.
        // For testability, accept ItemData directly:

        return new ActionResult(true, $"{actor.Name} uses {ItemId}.", EnergyCost);
    }
}
```

**Item resolution pattern**: `UseItemAction` constructor takes `ItemData` resolved from `ContentDatabase` at creation time (in GameManager), so the action itself is pure.

### 3.5 WaitAction

```csharp
public class WaitAction : IAction
{
    public ActionType Type => ActionType.Wait;
    public int ActorId { get; }
    public int EnergyCost => 1000;

    public bool Validate(IWorldState world) => world.GetEntity(ActorId) != null;

    public ActionResult Execute(IWorldState world, ICombatResolver combat)
    {
        return new ActionResult(true, "Waiting...", EnergyCost);
    }
}
```

### 3.6 DropItemAction

```csharp
public class DropItemAction : IAction
{
    public ActionType Type => ActionType.DropItem;
    public int ActorId { get; }
    public string ItemId { get; }
    public int EnergyCost => 500;

    public bool Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null) return false;
        return actor.Inventory.Contains(ItemId);
    }

    public ActionResult Execute(IWorldState world, ICombatResolver combat)
    {
        var actor = world.GetEntity(ActorId)!;
        actor.Inventory.Remove(ItemId);
        world.PlaceItem(ItemId, actor.Position);
        return new ActionResult(true, $"{actor.Name} drops {ItemId}.", EnergyCost);
    }
}
```

### 3.7 UseStairsAction

```csharp
public class UseStairsAction : IAction
{
    public ActionType Type => ActionType.UseStairs;
    public int ActorId { get; }
    public int EnergyCost => 0; // floor transition is free

    public bool Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null) return false;
        var tile = world.GetTile(actor.Position);
        return tile is TileType.StairsDown or TileType.StairsUp;
    }

    public ActionResult Execute(IWorldState world, ICombatResolver combat)
    {
        var actor = world.GetEntity(ActorId)!;
        var tile = world.GetTile(actor.Position);
        var direction = tile == TileType.StairsDown ? "down" : "up";
        // GameManager handles actual floor transition
        return new ActionResult(true, $"{actor.Name} takes the stairs {direction}.", EnergyCost);
    }
}
```

### 3.8 OpenDoorAction

```csharp
public class OpenDoorAction : IAction
{
    public ActionType Type => ActionType.OpenDoor;
    public int ActorId { get; }
    public Vec2I DoorPos { get; }
    public int EnergyCost => 500;

    public bool Validate(IWorldState world)
    {
        var actor = world.GetEntity(ActorId);
        if (actor == null) return false;
        if (actor.Position.ChebyshevDistance(DoorPos) > 1) return false;
        var tile = world.GetTile(DoorPos);
        return tile == TileType.DoorClosed;
    }

    public ActionResult Execute(IWorldState world, ICombatResolver combat)
    {
        world.SetTile(DoorPos, TileType.DoorOpen);
        return new ActionResult(true, "Door opened.", EnergyCost);
    }
}
```

---

## 4. Energy-Based Turn Scheduler

### 4.1 Algorithm (pseudocode)

```
CONSTANTS:
    ENERGY_THRESHOLD = 1000   # energy needed to act
    BASE_ENERGY_GAIN = 100    # gained per "tick"

DATA:
    actors: Dict<entityId, { energy: int, speed: int }>

FUNCTION GetNextActor() -> entityId:
    LOOP:
        # Find actor with energy >= ENERGY_THRESHOLD
        ready = [a for a in actors if a.energy >= ENERGY_THRESHOLD]

        IF ready is not empty:
            # Highest energy goes first. Ties broken by entityId (lower = first, gives player priority)
            SORT ready by (energy DESC, entityId ASC)
            RETURN ready[0].entityId

        # No one ready — advance time
        FOR each actor in actors:
            actor.energy += BASE_ENERGY_GAIN * (actor.speed / 100)

FUNCTION ConsumeEnergy(entityId, cost):
    actors[entityId].energy -= cost

NOTES:
    - Player always has entityId = 1 (first spawned), so wins ties
    - Speed 100 = normal, 200 = double speed, 50 = half speed
    - Haste effect sets speed to 150
    - Freeze effect sets speed to 50
    - Status effects tick when ConsumeEnergy is called (after an actor takes an action)
```

### 4.2 Implementation

```csharp
public class TurnScheduler : ITurnScheduler
{
    private const int EnergyThreshold = 1000;
    private const int BaseEnergyGain = 100;

    private readonly Dictionary<int, (int energy, int speed)> _actors = new();

    public void RegisterActor(int entityId, int speed)
    {
        _actors[entityId] = (0, speed);
    }

    public void UnregisterActor(int entityId)
    {
        _actors.Remove(entityId);
    }

    public int GetNextActor()
    {
        while (true)
        {
            // Find ready actors
            var ready = _actors
                .Where(a => a.Value.energy >= EnergyThreshold)
                .OrderByDescending(a => a.Value.energy)
                .ThenBy(a => a.Key) // lower ID first = player priority
                .Select(a => a.Key)
                .ToList();

            if (ready.Count > 0)
                return ready[0];

            // Advance time
            foreach (var id in _actors.Keys.ToList())
            {
                var (energy, speed) = _actors[id];
                _actors[id] = (energy + BaseEnergyGain * speed / 100, speed);
            }
        }
    }

    public void ConsumeEnergy(int entityId, int cost)
    {
        var (energy, speed) = _actors[entityId];
        _actors[entityId] = (energy - cost, speed);
    }

    public int GetEnergy(int entityId)
    {
        return _actors.TryGetValue(entityId, out var data) ? data.energy : 0;
    }

    public void TickStatusEffects(int entityId, IWorldState world)
    {
        var entity = world.GetEntity(entityId);
        if (entity == null) return;

        for (int i = entity.StatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = entity.StatusEffects[i];
            // Apply tick damage
            if (effect.TickDamage > 0)
            {
                entity.HP -= effect.TickDamage * effect.Stacks;
            }
            // Apply tick healing (Regeneration)
            if (effect.Type == StatusType.Regeneration)
            {
                entity.HP = Math.Min(entity.HP + 2 * effect.Stacks, entity.MaxHP);
            }
            // Decrement duration
            effect.RemainingTurns--;
            if (effect.RemainingTurns <= 0)
            {
                entity.StatusEffects.RemoveAt(i);
            }
        }

        // Update speed based on active effects
        int speedMod = 100;
        if (entity.StatusEffects.Any(e => e.Type == StatusType.Haste)) speedMod = 150;
        if (entity.StatusEffects.Any(e => e.Type == StatusType.Freeze)) speedMod = 50;
        if (_actors.ContainsKey(entityId))
        {
            var (energy, _) = _actors[entityId];
            _actors[entityId] = (energy, speedMod);
        }
    }
}
```

---

## 5. Status Effect System

### 5.1 Rules

| Rule | Detail |
|------|--------|
| **Tick timing** | Effects tick AFTER the affected entity acts (in `ConsumeEnergy`/`TickStatusEffects`) |
| **Stacking: same type** | If `Stackable=true`, increment `Stacks` up to `MaxStacks`. If false, refresh duration only |
| **Stacking: different types** | All different types coexist independently |
| **Duration** | Counted in actor-turns (not global turns) |
| **Removal** | Automatic when `RemainingTurns` hits 0 |
| **Death from DoT** | Entity dies if HP ≤ 0 after tick. Remove from world. |

### 5.2 Applying Effects

```csharp
public static class StatusEffectProcessor
{
    public static void ApplyEffect(EntityData entity, StatusType type, int duration, int tickDamage, bool stackable, int maxStacks)
    {
        var existing = entity.StatusEffects.FirstOrDefault(e => e.Type == type);
        if (existing != null)
        {
            if (stackable && existing.Stacks < maxStacks)
            {
                existing.Stacks++;
                existing.RemainingTurns = Math.Max(existing.RemainingTurns, duration);
            }
            else
            {
                // Refresh duration
                existing.RemainingTurns = Math.Max(existing.RemainingTurns, duration);
            }
        }
        else
        {
            entity.StatusEffects.Add(new ActiveStatusEffect
            {
                Type = type,
                RemainingTurns = duration,
                TickDamage = tickDamage,
                Stacks = 1
            });
        }
    }

    public static bool HasEffect(EntityData entity, StatusType type)
    {
        return entity.StatusEffects.Any(e => e.Type == type);
    }
}
```

### 5.3 Effect Behaviors

| Effect | Tick Behavior | Speed Impact | Special |
|--------|--------------|--------------|---------|
| Poison | 2 damage/tick/stack | None | Stackable, max 5 |
| Burn | 3 damage/tick | None | Not stackable, refreshes |
| Freeze | 0 damage | Speed = 50 | Not stackable |
| Haste | 0 damage | Speed = 150 | Not stackable |
| Shield | 0 damage | None | +3 defense/stack, max 3 |
| Confusion | 0 damage | None | Random movement direction |
| Blind | 0 damage | None | View radius = 1 |
| Regeneration | Heal 2/tick/stack | None | Stackable, max 3 |

---

## 6. Combat Formulas

### 6.1 Hit Chance

```
BASE_HIT_CHANCE = 80%
hit_chance = BASE_HIT_CHANCE + (attacker.Attack - defender.Defense) * 2
hit_chance = clamp(hit_chance, 5%, 95%)  // always 5-95%

IF attacker has Blind: hit_chance -= 30%
IF defender has Freeze: hit_chance += 20%

Roll: random(0..99) < hit_chance → HIT
```

### 6.2 Damage Calculation

```
base_damage = attacker.Attack + weapon_bonus
variance = random(-2, +2)  // small random variance
raw_damage = max(1, base_damage + variance)

// Weapon damage type from equipped weapon's DamageType (default Physical)
```

### 6.3 Armor Reduction

```
effective_armor = defender.Defense + armor_bonus + shield_stacks * 3
reduction = effective_armor / 2  (integer division)

// Type resistances (future-proofing, not in MVP):
// Fire vs Ice armor = 50% reduction
// Lightning ignores 50% of armor

final_damage = max(1, raw_damage - reduction)   // always at least 1 damage on hit
```

### 6.4 CombatResolver Implementation

```csharp
public class CombatResolver : ICombatResolver
{
    private readonly Random _rng;

    public CombatResolver(int seed) { _rng = new Random(seed); }
    public CombatResolver(Random rng) { _rng = rng; }

    public DamageEvent ResolveMeleeAttack(EntityData attacker, EntityData defender)
    {
        bool hit = RollHitChance(attacker, defender);
        if (!hit)
        {
            return new DamageEvent(attacker.Id, defender.Id, 0, 0, DamageType.Physical, false, false);
        }

        var weaponType = DamageType.Physical; // TODO: resolve from equipped weapon
        int damage = CalculateDamage(attacker, defender, weaponType);
        bool killed = defender.HP - damage <= 0;

        return new DamageEvent(attacker.Id, defender.Id, damage + ApplyArmor(damage, defender.Defense, weaponType) - damage, damage, weaponType, true, killed);
    }

    public bool RollHitChance(EntityData attacker, EntityData defender)
    {
        int hitChance = 80 + (attacker.Attack - defender.Defense) * 2;

        if (StatusEffectProcessor.HasEffect(attacker, StatusType.Blind))
            hitChance -= 30;
        if (StatusEffectProcessor.HasEffect(defender, StatusType.Freeze))
            hitChance += 20;

        hitChance = Math.Clamp(hitChance, 5, 95);
        return _rng.Next(100) < hitChance;
    }

    public int CalculateDamage(EntityData attacker, EntityData defender, DamageType type)
    {
        int weaponBonus = 0; // resolved from equipped weapon ItemData
        int baseDamage = attacker.Attack + weaponBonus;
        int variance = _rng.Next(-2, 3); // -2 to +2
        int rawDamage = Math.Max(1, baseDamage + variance);
        return Math.Max(1, rawDamage - ApplyArmor(rawDamage, defender.Defense, type));
    }

    public int ApplyArmor(int rawDamage, int armor, DamageType type)
    {
        int shieldBonus = 0; // resolved from entity status effects externally
        int effectiveArmor = armor + shieldBonus;
        int reduction = effectiveArmor / 2;
        return reduction;
    }
}
```

**Important**: `CombatResolver` takes a seeded `Random` for deterministic replays.

---

## 7. Unit Test Cases (20 scenarios)

All tests use xUnit or NUnit. Each test constructs a `WorldState`, scheduler, and combat resolver with a fixed seed.

### WorldState Tests

| # | Test Scenario | Expected |
|---|---------------|----------|
| T1 | Spawn entity on floor tile | Entity exists, position correct, ID = 1 |
| T2 | Spawn entity on wall tile | Throws `InvalidOperationException` |
| T3 | Spawn two entities on same tile | Throws `InvalidOperationException` |
| T4 | Move entity to empty floor | Position updates, old pos empty |
| T5 | Remove entity | `GetEntity` returns null, position freed |
| T6 | `GetEntityAt` on empty tile | Returns null |
| T7 | `GetEntitiesInRadius(center, 2)` with 3 entities, 1 out of range | Returns 2 entities |
| T8 | `IsWalkable` for each TileType | Floor/Stairs/DoorOpen = true, Wall/DoorClosed/Water/Lava = false |
| T9 | `IsOpaque` for each TileType | Wall/DoorClosed = true, all others = false |
| T10 | Place and pick up ground item | Item placed, then picked up, ground empty |

### Action Tests

| # | Test Scenario | Expected |
|---|---------------|----------|
| T11 | MoveAction to empty floor | Success, entity at new position |
| T12 | MoveAction into wall | Validate returns false |
| T13 | MoveAction into entity | Validate returns false |
| T14 | Diagonal move when both adjacent walls | Validate returns false (corner-cutting) |
| T15 | Diagonal move when one adjacent wall | Validate returns true |
| T16 | AttackAction on adjacent enemy | Damage dealt, HP reduced |
| T17 | AttackAction kills enemy (HP → 0) | Entity removed from world |
| T18 | AttackAction on same faction | Validate returns false |
| T19 | PickUpAction with full inventory | Validate returns false |
| T20 | UseStairsAction not on stairs | Validate returns false |

### TurnScheduler Tests

| # | Test Scenario | Expected |
|---|---------------|----------|
| T21 | Two actors speed 100, player ID=1 | Player acts first (tie-break) |
| T22 | Actor speed 200 vs speed 100 | Fast actor gets 2 turns per 1 slow turn |
| T23 | Consume 1000 energy | Actor needs recharge before next turn |
| T24 | Unregister actor | Never returned by `GetNextActor` |
| T25 | Three actors with speeds 100, 150, 50 | Turn order follows speed ratios |

### Combat Tests

| # | Test Scenario | Expected |
|---|---------------|----------|
| T26 | Attack=10 vs Defense=5 with seed | Hit chance = 80 + (10-5)*2 = 90% |
| T27 | Attack=1 vs Defense=20 | Hit chance clamped to 5% |
| T28 | Blind attacker | Hit chance reduced by 30% |
| T29 | Frozen defender | Hit chance increased by 20% |
| T30 | Damage always at least 1 on hit | Even with high armor |

### Status Effect Tests

| # | Test Scenario | Expected |
|---|---------------|----------|
| T31 | Apply Poison, tick 3 times | 2 damage per tick, removed after duration |
| T32 | Stack Poison twice | 4 damage per tick (2 per stack) |
| T33 | Stack Poison at max (5) | 6th application refreshes duration, no new stack |
| T34 | Apply Burn then Poison | Both tick independently |
| T35 | Apply Haste | Scheduler speed set to 150 |
| T36 | Effect expires at 0 turns | Removed from entity's list |
| T37 | Poison kills entity | HP ≤ 0 after tick |

---

## 8. Determinism Requirement

All simulation code MUST be deterministic given the same seed:
- `CombatResolver` uses seeded `Random`.
- No `DateTime.Now`, no `Guid.NewGuid()`, no `HashSet` iteration order dependencies.
- Dictionary iteration in `TurnScheduler` must be replaced with `SortedDictionary` or explicit sorting.
- All `IEnumerable` results that affect game logic must be materialized in deterministic order.

---

## 9. Dependencies on Other Agents

| Dependency | Provider | What Sim Agent Needs |
|------------|----------|---------------------|
| Interfaces/DTOs/Enums | Agent 1 (Architecture) | Must exist before implementation |
| LevelBlueprint | Agent 4 (Generation) | WorldState.LoadFromBlueprint; use stub for testing |
| ItemData | Agent 9 (Content) | UseItemAction; use stub items for testing |
| AI decisions | Agent 5 (AI) | AI calls into action system; no dependency from sim |

**Stub strategy**: Create test fixture data in test files. Do not depend on other agents' implementations at test time.
