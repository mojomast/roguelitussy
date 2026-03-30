# Agent 5: AI Agent — Detailed Specification

## Mission
Implement utility-based AI for enemy entities: decision-making, state management, A* pathfinding on the grid, and multiple AI profiles. Enemies must behave intelligently — chasing visible players, patrolling when idle, fleeing at low HP, and selecting appropriate actions.

---

## 1. Files to Create

| File | Purpose |
|------|---------|
| `Core/AI/AIBrain.cs` | Main AI decision engine implementing `IAIBrain` |
| `Core/AI/UtilityScorer.cs` | Utility scoring functions for each possible action |
| `Core/AI/AIStateManager.cs` | Per-entity state tracking (idle/patrol/chase/attack/flee) |
| `Core/AI/Pathfinding.cs` | A* pathfinding on WorldState grid |
| `Core/AI/AIProfiles.cs` | Profile definitions (aggressive, cautious, ranged) |
| `Tests/AITests/PathfindingTests.cs` | A* tests |
| `Tests/AITests/AIBrainTests.cs` | Decision-making tests |
| `Tests/AITests/UtilityScorerTests.cs` | Scoring function tests |

---

## 2. AI Action Selection Pipeline

```
INPUT: EntityData (the enemy), IWorldState, IFOVProvider

STEP 1: Compute entity's FOV
    visible = fov.ComputeFOV(entity.Position, entity.ViewRadius, world.IsOpaque)

STEP 2: Detect threats and targets
    targets = entities in `visible` with hostile faction
    player = targets.FirstOrDefault(t => t.IsPlayer)
    nearestTarget = closest target by pathfinding distance (not Euclidean)

STEP 3: Update AI state
    currentState = AIStateManager.GetState(entity.Id)
    newState = DetermineState(entity, nearestTarget, currentState, profile)
    AIStateManager.SetState(entity.Id, newState)

STEP 4: Score candidate actions based on state
    candidates = GenerateCandidateActions(entity, world, state, targets)
    scoredCandidates = candidates.Select(a => (a, UtilityScorer.Score(a, entity, world, state, profile)))
    bestAction = scoredCandidates.OrderByDescending(s => s.score).First().action

STEP 5: Validate and return
    if bestAction.Validate(world): return bestAction
    else: return new WaitAction(entity.Id)  // fallback

OUTPUT: IAction
```

---

## 3. AI States

### State Transitions

```
                    ┌──────────────┐
         no target  │              │  target in view
        ┌──────────►│    IDLE      ├──────────────┐
        │           │              │               │
        │           └──────┬───────┘               ▼
        │                  │ patrol timer      ┌────────┐
        │                  ▼                   │ CHASE  │
        │           ┌──────────────┐           │        │
        │           │   PATROL     │◄──────────┤        │
        │           │              │ lost sight └───┬────┘
        │           └──────────────┘                │
        │                                           │ adjacent
        │                                           ▼
        │           ┌──────────────┐           ┌────────┐
        │           │    FLEE      │◄──────────┤ ATTACK │
        │           │              │  HP < 25% │        │
        │           └──────────────┘           └────────┘
        │                  │
        └──────────────────┘  HP > 50% and no target
```

### State Behaviors

| State | Entry Condition | Behavior |
|-------|----------------|----------|
| **Idle** | Default / no target, not patrolling | Stand still. Wait action. Transition to Patrol after 3 idle turns. |
| **Patrol** | Idle for 3+ turns | Walk toward a random floor tile within 10 tiles. On arrival or after 8 steps, pick new target. |
| **Chase** | Target visible | A* pathfind toward target. Move along path. |
| **Attack** | Adjacent to target (Chebyshev ≤ 1) | Execute AttackAction on target. |
| **Flee** | HP ≤ 25% of MaxHP and target visible | Move AWAY from target. Pick direction that maximizes distance. |

### State Determination

```csharp
public static AIState DetermineState(EntityData entity, EntityData? target, AIState current, AIProfile profile)
{
    bool lowHP = entity.HP <= entity.MaxHP * profile.FleeThreshold;

    // Flee if low HP and target visible
    if (lowHP && target != null && profile.CanFlee)
        return AIState.Flee;

    // Attack if adjacent to target
    if (target != null && entity.Position.ChebyshevDistance(target.Position) <= 1)
        return AIState.Attack;

    // Chase if target visible
    if (target != null)
        return AIState.Chase;

    // Patrol if was chasing (lost sight) or idle too long
    if (current == AIState.Chase)
        return AIState.Patrol; // continue moving to last known position

    // Default
    return current == AIState.Patrol ? AIState.Patrol : AIState.Idle;
}
```

---

## 4. Utility-Based Decision Scoring

### Decision Factors

| Factor | Description | Weight Range |
|--------|-------------|-------------|
| `distanceToTarget` | Path distance to nearest hostile | 0.0 - 1.0 (closer = higher for aggro, lower for flee) |
| `hpRatio` | entity.HP / entity.MaxHP | 0.0 - 1.0 |
| `targetHPRatio` | target.HP / target.MaxHP | 0.0 - 1.0 (lower = more tempting) |
| `adjacentToTarget` | Is target adjacent? | 0 or 1 |
| `fleeDirection` | Does this move increase distance? | 0 or 1 |
| `pathExists` | Can we pathfind to target? | 0 or 1 |

### Scoring Functions by Action Type

```csharp
public static class UtilityScorer
{
    public static float ScoreAction(IAction action, EntityData entity, EntityData? target,
                                     IWorldState world, AIState state, AIProfile profile)
    {
        return action.Type switch
        {
            ActionType.Attack => ScoreAttack(entity, target, profile),
            ActionType.Move => ScoreMove(action, entity, target, state, profile),
            ActionType.Wait => ScoreWait(state),
            _ => 0f
        };
    }

    private static float ScoreAttack(EntityData entity, EntityData? target, AIProfile profile)
    {
        if (target == null) return 0f;
        float score = profile.AggressionWeight * 0.8f;
        // Bonus for finishing low-HP targets
        float targetHPRatio = (float)target.HP / target.MaxHP;
        score += (1f - targetHPRatio) * 0.3f;
        return score;
    }

    private static float ScoreMove(IAction action, EntityData entity, EntityData? target,
                                    AIState state, AIProfile profile)
    {
        if (action is not MoveAction move) return 0f;

        var newPos = entity.Position + Vec2I.FromDirection(move.Dir);

        switch (state)
        {
            case AIState.Chase:
                if (target == null) return 0.1f;
                // Higher score if move gets closer to target
                int currentDist = entity.Position.ManhattanDistance(target.Position);
                int newDist = newPos.ManhattanDistance(target.Position);
                return newDist < currentDist ? 0.7f * profile.AggressionWeight : 0.1f;

            case AIState.Flee:
                if (target == null) return 0.3f;
                int curDist = entity.Position.ManhattanDistance(target.Position);
                int nDist = newPos.ManhattanDistance(target.Position);
                return nDist > curDist ? 0.9f : 0.1f;

            case AIState.Patrol:
                return 0.4f; // moderate desire to move while patrolling

            default:
                return 0.2f;
        }
    }

    private static float ScoreWait(AIState state)
    {
        return state == AIState.Idle ? 0.5f : 0.05f; // only good when idle
    }
}
```

---

## 5. A* Pathfinding

```csharp
public static class Pathfinding
{
    /// <summary>
    /// A* pathfinding on the grid. Returns list of positions from start to goal (exclusive of start).
    /// Returns empty list if no path found.
    /// </summary>
    public static List<Vec2I> FindPath(Vec2I start, Vec2I goal, IWorldState world, int maxSteps = 200)
    {
        var openSet = new PriorityQueue<Vec2I, int>();
        var cameFrom = new Dictionary<Vec2I, Vec2I>();
        var gScore = new Dictionary<Vec2I, int> { [start] = 0 };

        openSet.Enqueue(start, Heuristic(start, goal));
        int steps = 0;

        while (openSet.Count > 0 && steps < maxSteps)
        {
            steps++;
            var current = openSet.Dequeue();

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbor in GetNeighbors(current, world))
            {
                int tentativeG = gScore[current] + 1; // uniform cost

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    int fScore = tentativeG + Heuristic(neighbor, goal);
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }

        return new List<Vec2I>(); // no path
    }

    private static int Heuristic(Vec2I a, Vec2I b)
    {
        // Chebyshev distance for 8-directional movement
        return a.ChebyshevDistance(b);
    }

    private static IEnumerable<Vec2I> GetNeighbors(Vec2I pos, IWorldState world)
    {
        // 8-directional
        Vec2I[] dirs = {
            Vec2I.Up, Vec2I.Down, Vec2I.Left, Vec2I.Right,
            Vec2I.UpLeft, Vec2I.UpRight, Vec2I.DownLeft, Vec2I.DownRight
        };

        foreach (var dir in dirs)
        {
            var next = pos + dir;
            if (!world.IsInBounds(next)) continue;
            if (!world.IsWalkable(next)) continue;

            // Diagonal corner-cutting check
            if (dir.X != 0 && dir.Y != 0)
            {
                var adjX = pos + new Vec2I(dir.X, 0);
                var adjY = pos + new Vec2I(0, dir.Y);
                if (!world.IsWalkable(adjX) && !world.IsWalkable(adjY)) continue;
            }

            // Don't pathfind through other entities (except target)
            // Entity blocking is optional — can treat entities as passable for pathfinding
            // but validate actual move separately

            yield return next;
        }
    }

    private static List<Vec2I> ReconstructPath(Dictionary<Vec2I, Vec2I> cameFrom, Vec2I current)
    {
        var path = new List<Vec2I> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        path.RemoveAt(0); // remove start position
        return path;
    }
}
```

### Pathfinding Notes
- **Max steps**: 200 iterations to prevent infinite loops on large maps.
- **Entity blocking**: Entities are NOT blockers for pathfinding (pathfind through them). Actual move validation happens in `MoveAction.Validate()`. If blocked by entity, the AI will wait or recalculate.
- **Caching**: Path is cached per entity. Recalculate when target moves or path is blocked.

---

## 6. AI Profiles

```csharp
public class AIProfile
{
    public string Id { get; set; } = "";
    public float AggressionWeight { get; set; } = 1.0f;    // multiplier on attack/chase scores
    public float FleeThreshold { get; set; } = 0.25f;      // HP ratio to start fleeing
    public bool CanFlee { get; set; } = true;
    public int PatrolRadius { get; set; } = 10;             // max patrol wander distance
    public int IdleTurnsBeforePatrol { get; set; } = 3;
    public bool PrefersRanged { get; set; } = false;        // not used in MVP, future-proofing
    public int PreferredDistance { get; set; } = 1;         // 1 = melee, >1 = ranged (future)
}
```

### Default Profiles

| Profile | Aggression | FleeThreshold | CanFlee | PatrolRadius | Notes |
|---------|-----------|---------------|---------|-------------|-------|
| `aggressive` | 1.2 | 0.15 | false | 8 | Fights to the death, chases hard |
| `cautious` | 0.7 | 0.35 | true | 6 | Flees earlier, less aggressive |
| `ranged` | 0.9 | 0.25 | true | 12 | Tries to keep distance (future: ranged attacks) |
| `cowardly` | 0.4 | 0.50 | true | 4 | Flees at half HP, low aggression |
| `berserker` | 1.5 | 0.0 | false | 15 | Never flees, maximum aggression |

### Profile Assignment
- Stored in `EnemyData.AIProfile` field.
- Resolved at AI decision time from a static registry:

```csharp
public static class AIProfiles
{
    private static readonly Dictionary<string, AIProfile> _profiles = new()
    {
        ["aggressive"] = new AIProfile { Id = "aggressive", AggressionWeight = 1.2f, FleeThreshold = 0.15f, CanFlee = false, PatrolRadius = 8 },
        ["cautious"] = new AIProfile { Id = "cautious", AggressionWeight = 0.7f, FleeThreshold = 0.35f, CanFlee = true, PatrolRadius = 6 },
        ["ranged"] = new AIProfile { Id = "ranged", AggressionWeight = 0.9f, FleeThreshold = 0.25f, CanFlee = true, PatrolRadius = 12, PrefersRanged = true, PreferredDistance = 4 },
        ["cowardly"] = new AIProfile { Id = "cowardly", AggressionWeight = 0.4f, FleeThreshold = 0.50f, CanFlee = true, PatrolRadius = 4 },
        ["berserker"] = new AIProfile { Id = "berserker", AggressionWeight = 1.5f, FleeThreshold = 0.0f, CanFlee = false, PatrolRadius = 15 },
    };

    public static AIProfile Get(string id) => _profiles.GetValueOrDefault(id, _profiles["aggressive"]);
}
```

---

## 7. AIBrain Implementation

```csharp
public class AIBrain : IAIBrain
{
    private readonly AIStateManager _stateManager = new();
    private readonly Dictionary<int, List<Vec2I>> _cachedPaths = new();
    private readonly Dictionary<int, Vec2I> _patrolTargets = new();
    private readonly Dictionary<int, int> _idleCounters = new();

    public IAction DecideAction(EntityData entity, IWorldState world, IFOVProvider fov)
    {
        var profile = AIProfiles.Get(entity.AIProfileId ?? "aggressive");

        // Step 1: FOV
        var visible = fov.ComputeFOV(entity.Position, entity.ViewRadius, pos => world.IsOpaque(pos));

        // Step 2: Find targets
        EntityData? target = null;
        int bestDist = int.MaxValue;
        foreach (var other in world.GetAllEntities())
        {
            if (other.Id == entity.Id) continue;
            if (other.Faction == entity.Faction) continue;
            if (!visible.Contains(other.Position)) continue;
            int dist = entity.Position.ManhattanDistance(other.Position);
            if (dist < bestDist)
            {
                bestDist = dist;
                target = other;
            }
        }

        // Step 3: State
        var currentState = _stateManager.GetState(entity.Id);
        var newState = DetermineStateWithIdleTracking(entity, target, currentState, profile);
        _stateManager.SetState(entity.Id, newState);

        // Step 4: Generate and score candidates
        var candidates = GenerateCandidates(entity, target, world, newState, profile);
        var best = candidates
            .Select(a => (action: a, score: UtilityScorer.ScoreAction(a, entity, target, world, newState, profile)))
            .OrderByDescending(x => x.score)
            .FirstOrDefault();

        if (best.action != null && best.action.Validate(world))
            return best.action;

        return new WaitAction(entity.Id);
    }

    private List<IAction> GenerateCandidates(EntityData entity, EntityData? target,
                                              IWorldState world, AIState state, AIProfile profile)
    {
        var candidates = new List<IAction>();

        // Always can wait
        candidates.Add(new WaitAction(entity.Id));

        // Attack if adjacent to target
        if (target != null && entity.Position.ChebyshevDistance(target.Position) <= 1)
        {
            candidates.Add(new AttackAction(entity.Id, target.Id));
        }

        // Movement in all 8 directions
        foreach (Direction dir in Enum.GetValues<Direction>())
        {
            if (dir == Direction.None) continue;
            var move = new MoveAction(entity.Id, dir);
            if (move.Validate(world))
                candidates.Add(move);
        }

        return candidates;
    }

    private AIState DetermineStateWithIdleTracking(EntityData entity, EntityData? target,
                                                    AIState current, AIProfile profile)
    {
        // Track idle turns
        if (current == AIState.Idle)
        {
            _idleCounters.TryGetValue(entity.Id, out int count);
            _idleCounters[entity.Id] = count + 1;
            if (_idleCounters[entity.Id] >= profile.IdleTurnsBeforePatrol)
            {
                _idleCounters[entity.Id] = 0;
                if (target == null) return AIState.Patrol;
            }
        }
        else
        {
            _idleCounters[entity.Id] = 0;
        }

        return AIStateManager.DetermineState(entity, target, current, profile);
    }
}
```

---

## 8. Confusion Effect Integration

When an entity has `StatusType.Confusion`:
```csharp
// In AIBrain.DecideAction, after choosing a MoveAction:
if (StatusEffectProcessor.HasEffect(entity, StatusType.Confusion))
{
    // 50% chance to replace direction with random
    if (rng.Next(2) == 0)
    {
        var randomDir = (Direction)rng.Next(8); // 0-7
        return new MoveAction(entity.Id, randomDir);
    }
}
```

---

## 9. Test Scenarios (12)

| # | Test Scenario | Expected |
|---|---------------|----------|
| AI1 | A* path in open room, 10 tiles apart | Path found, length ~10 |
| AI2 | A* path around L-shaped wall | Path goes around wall, not through |
| AI3 | A* no path (fully enclosed) | Returns empty list |
| AI4 | A* max steps reached | Returns empty list (no infinite loop) |
| AI5 | Enemy sees player adjacent → Attack | Returns AttackAction |
| AI6 | Enemy sees player 5 tiles away → Chase | Returns MoveAction toward player |
| AI7 | Enemy HP at 20% with aggressive profile → no flee (CanFlee=false) | Does not flee |
| AI8 | Enemy HP at 30% with cautious profile → Flee | Returns MoveAction away from player |
| AI9 | No target visible → Idle then Patrol | After 3 idle turns, begins patrolling |
| AI10 | Berserker profile never flees at 1 HP | Still attacks |
| AI11 | Confused enemy: movement may be random | With confusion effect, move direction randomized |
| AI12 | Two enemies, both see player | Each independently selects action, no conflicts |

---

## 10. Dependencies

| Dependency | Provider | Notes |
|------------|----------|-------|
| `IWorldState` | Agent 2 | Query tiles, entities |
| `IFOVProvider` | Agent 3 | FOV calculation for enemy vision |
| `IAction` implementations | Agent 2 | MoveAction, AttackAction, WaitAction |
| `EntityData` | Agent 1 | Enemy data with AIProfileId |
| `StatusEffectProcessor` | Agent 2 | Check confusion/blind |
| `EnemyData.AIProfile` | Agent 9 | Profile assignment per enemy type |

**Stub strategy**: Tests create a small WorldState grid (10×10) with manually placed walls and entities. FOV uses a simple "all visible" stub for pathfinding tests.
