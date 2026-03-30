# Agent 4: Generation Agent — Detailed Specification

## Mission
Procedurally generate dungeon levels using BSP partitioning + room prefabs + corridor connection. Output `LevelBlueprint` consumed by WorldState. Validate connectivity. Support biome/theme variations. Fully deterministic given a seed.

---

## 1. Files to Create

| File | Purpose |
|------|---------|
| `Core/Generation/DungeonGenerator.cs` | Main generator implementing `IDungeonGenerator` |
| `Core/Generation/BSPTree.cs` | Binary Space Partition tree |
| `Core/Generation/RoomPlacer.cs` | Places rooms within BSP leaves |
| `Core/Generation/CorridorBuilder.cs` | Connects rooms with corridors |
| `Core/Generation/SpawnPlacer.cs` | Places player, stairs, enemies, items |
| `Core/Generation/RoomPrefab.cs` | Room prefab data structure |
| `Core/Generation/FloodFillValidator.cs` | Connectivity validation |
| `Core/Generation/BiomeTheme.cs` | Biome/theme configuration |
| `Content/Rooms/prefabs.json` | Room prefab definitions |
| `Tests/GenerationTests/GeneratorTests.cs` | Generation tests |
| `Tests/GenerationTests/BSPTests.cs` | BSP tests |
| `Tests/GenerationTests/ConnectivityTests.cs` | Flood fill tests |

---

## 2. Generation Pipeline

```
Input: (floor number, seed, biome name)
Output: LevelBlueprint

STEP 1: Initialize
    - Create tile grid (width × height) filled with Wall
    - Seed RNG with: seed XOR (floor * 7919)  [floor-unique but deterministic]

STEP 2: BSP Partition
    - Recursively split space into leaves
    - Minimum leaf size: 8×8
    - Split direction alternates H/V (or random with bias toward longer axis)
    - Depth limit: 5 levels (yields 16-32 leaves)

STEP 3: Place Rooms in Leaves
    - For each BSP leaf, place a room:
      a. Try to fit a random prefab from the biome's prefab list
      b. If no prefab fits, generate a rectangular room:
         - Width: random(4, leaf.Width - 2)
         - Height: random(4, leaf.Height - 2)
      c. Center room within leaf (with random offset ±1)
      d. Carve room tiles to Floor

STEP 4: Connect Rooms
    - Walk BSP tree bottom-up
    - For each pair of sibling leaves, connect their rooms with a corridor
    - Corridor type: L-shaped (preferred) or straight

STEP 5: Place Stairs
    - StairsUp: random floor tile in starting room (room closest to center if floor > 1)
    - StairsDown: random floor tile in room farthest from StairsUp (by room center distance)

STEP 6: Place Player
    - On StairsUp position (player enters from stairs)

STEP 7: Spawn Enemies
    - Count: base_enemies + floor * 2 (e.g., 3 + floor*2)
    - For each enemy:
      a. Pick random room (not player's room for first 2)
      b. Pick random floor tile in that room not occupied
      c. Select enemy type from content DB weighted by floor-appropriate spawn weights

STEP 8: Spawn Items
    - Count: base_items + floor (e.g., 2 + floor)
    - For each item:
      a. Pick random floor tile in any room
      b. Select item from loot table weighted by floor

STEP 9: Validate Connectivity
    - Flood fill from StairsUp position
    - All placed entities, items, and StairsDown must be reachable
    - If not: REGENERATE with seed+1 (retry up to 10 times, then error)

STEP 10: Apply Theme
    - Replace some floor tiles with Water/Lava per biome rules
    - Place doors at room entrances (4+ width corridors connecting to rooms)

RETURN LevelBlueprint
```

---

## 3. BSP Partitioning

```csharp
public class BSPTree
{
    public int X, Y, Width, Height;
    public BSPTree? Left, Right;
    public RoomData? Room;

    private const int MinLeafSize = 8;
    private const int MaxDepth = 5;

    public static BSPTree Generate(int width, int height, Random rng)
    {
        var root = new BSPTree { X = 0, Y = 0, Width = width, Height = height };
        Split(root, 0, rng);
        return root;
    }

    private static void Split(BSPTree node, int depth, Random rng)
    {
        if (depth >= MaxDepth) return;
        if (node.Width < MinLeafSize * 2 && node.Height < MinLeafSize * 2) return;

        // Decide split direction
        bool splitHorizontal;
        if (node.Width < MinLeafSize * 2) splitHorizontal = true;
        else if (node.Height < MinLeafSize * 2) splitHorizontal = false;
        else splitHorizontal = node.Height > node.Width ? true : (rng.Next(2) == 0);

        if (splitHorizontal)
        {
            int splitY = rng.Next(MinLeafSize, node.Height - MinLeafSize + 1);
            node.Left = new BSPTree { X = node.X, Y = node.Y, Width = node.Width, Height = splitY };
            node.Right = new BSPTree { X = node.X, Y = node.Y + splitY, Width = node.Width, Height = node.Height - splitY };
        }
        else
        {
            int splitX = rng.Next(MinLeafSize, node.Width - MinLeafSize + 1);
            node.Left = new BSPTree { X = node.X, Y = node.Y, Width = splitX, Height = node.Height };
            node.Right = new BSPTree { X = node.X + splitX, Y = node.Y, Width = node.Width - splitX, Height = node.Height };
        }

        Split(node.Left, depth + 1, rng);
        Split(node.Right, depth + 1, rng);
    }

    public IEnumerable<BSPTree> GetLeaves()
    {
        if (Left == null && Right == null)
        {
            yield return this;
        }
        else
        {
            if (Left != null) foreach (var l in Left.GetLeaves()) yield return l;
            if (Right != null) foreach (var r in Right.GetLeaves()) yield return r;
        }
    }
}
```

---

## 4. Room Prefab Format

### JSON Schema

```json
{
  "prefabs": [
    {
      "id": "small_square",
      "width": 5,
      "height": 5,
      "tiles": [
        ".....",
        ".fff.",
        ".fff.",
        ".fff.",
        "....."
      ],
      "legend": {
        ".": "Wall",
        "f": "Floor",
        "d": "DoorClosed",
        "w": "Water",
        "l": "Lava"
      },
      "tags": ["common", "dungeon", "cave"],
      "spawnPoints": [
        { "x": 2, "y": 2, "type": "enemy" },
        { "x": 3, "y": 3, "type": "item" }
      ],
      "minFloor": 1,
      "maxFloor": 99
    }
  ]
}
```

### Prefab Placement Algorithm

```
1. Get list of prefabs matching current biome tags
2. Filter by floor range (minFloor <= currentFloor <= maxFloor)
3. Filter by size (prefab must fit within BSP leaf with 1-tile wall margin)
4. Pick random matching prefab (weighted by nothing — uniform)
5. Optionally rotate 0/90/180/270 degrees (rotate tile array)
6. Place centered in leaf
7. Record as RoomData in LevelBlueprint.Rooms
```

---

## 5. Corridor Connection

### L-Shaped Corridors

```csharp
public static class CorridorBuilder
{
    /// <summary>
    /// Connect two points with an L-shaped corridor (horizontal then vertical, or vice versa).
    /// </summary>
    public static void ConnectRooms(TileType[,] tiles, Vec2I from, Vec2I to, Random rng)
    {
        // 50% chance: horizontal first, or vertical first
        if (rng.Next(2) == 0)
        {
            CarveHorizontal(tiles, from.X, to.X, from.Y);
            CarveVertical(tiles, from.Y, to.Y, to.X);
        }
        else
        {
            CarveVertical(tiles, from.Y, to.Y, from.X);
            CarveHorizontal(tiles, from.X, to.X, to.Y);
        }
    }

    private static void CarveHorizontal(TileType[,] tiles, int x1, int x2, int y)
    {
        int start = Math.Min(x1, x2);
        int end = Math.Max(x1, x2);
        for (int x = start; x <= end; x++)
        {
            if (tiles[x, y] == TileType.Wall)
                tiles[x, y] = TileType.Floor;
        }
    }

    private static void CarveVertical(TileType[,] tiles, int y1, int y2, int x)
    {
        int start = Math.Min(y1, y2);
        int end = Math.Max(y1, y2);
        for (int y = start; y <= end; y++)
        {
            if (tiles[x, y] == TileType.Wall)
                tiles[x, y] = TileType.Floor;
        }
    }
}
```

### Connection Points
- Each room's connection point = center of the room (or a random floor tile on the room's perimeter).
- When connecting BSP siblings: pick a random floor tile in each sibling's room as endpoints.

---

## 6. Spawn Placement Rules

### Player
- Always placed on `StairsUp` position.
- StairsUp is in the first room (sorted by BSP tree order).

### Stairs
- `StairsUp` and `StairsDown` must be in DIFFERENT rooms.
- `StairsDown` in the room with the greatest Manhattan distance from `StairsUp` room center.
- Both placed on floor tiles, not occupied by other spawns.

### Enemies
```
enemy_count = 3 + floor * 2
max_enemies = 20

For each enemy:
    1. Pick a random room (skip player's room for first 2 enemies)
    2. Pick a random unoccupied floor tile in that room
    3. Select enemy type:
       - Filter content DB enemies by minFloor <= floor <= maxFloor
       - Weighted random by spawnWeight
    4. Add to LevelBlueprint.EnemySpawns
```

### Items
```
item_count = 2 + floor
max_items = 10

For each item:
    1. Pick a random room
    2. Pick a random unoccupied floor tile
    3. Select item from loot table (see Content Agent)
    4. Add to LevelBlueprint.ItemSpawns
```

---

## 7. Connectivity Validation (Flood Fill)

```csharp
public static class FloodFillValidator
{
    /// <summary>
    /// Returns true if all required positions are reachable from start.
    /// </summary>
    public static bool Validate(TileType[,] tiles, int width, int height, Vec2I start, IEnumerable<Vec2I> mustReach)
    {
        var reachable = FloodFill(tiles, width, height, start);
        return mustReach.All(pos => reachable.Contains(pos));
    }

    public static HashSet<Vec2I> FloodFill(TileType[,] tiles, int width, int height, Vec2I start)
    {
        var visited = new HashSet<Vec2I>();
        var queue = new Queue<Vec2I>();
        queue.Enqueue(start);
        visited.Add(start);

        Vec2I[] dirs = { Vec2I.Up, Vec2I.Down, Vec2I.Left, Vec2I.Right };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var dir in dirs)
            {
                var next = current + dir;
                if (next.X < 0 || next.X >= width || next.Y < 0 || next.Y >= height) continue;
                if (visited.Contains(next)) continue;
                var tile = tiles[next.X, next.Y];
                if (tile is TileType.Wall) continue; // non-walkable
                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return visited;
    }
}
```

### What Must Be Reachable
- StairsDown
- All enemy spawn positions
- All item spawn positions

If validation fails: regenerate with `seed + 1`, up to 10 retries.

---

## 8. Biome/Theme System

```csharp
public class BiomeTheme
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> PrefabTags { get; set; } = new();      // filter prefabs by tag
    public float WaterChance { get; set; } = 0.0f;             // % of floor tiles to convert
    public float LavaChance { get; set; } = 0.0f;
    public bool HasDoors { get; set; } = true;
    public int MinRoomSize { get; set; } = 4;
    public int MaxRoomSize { get; set; } = 12;
    public string TilesetVariant { get; set; } = "default";    // for rendering
}
```

### Default Biomes

| Biome | Floors | WaterChance | LavaChance | HasDoors | Tags |
|-------|--------|-------------|------------|----------|------|
| `dungeon` | 1-3 | 0% | 0% | Yes | common, dungeon |
| `cave` | 4-6 | 5% | 0% | No | common, cave, organic |
| `volcano` | 7-9 | 0% | 10% | No | common, volcano |

### Floor-to-Biome Mapping

```csharp
public static string GetBiome(int floor) => floor switch
{
    <= 3 => "dungeon",
    <= 6 => "cave",
    <= 9 => "volcano",
    _ => "dungeon"  // cycle
};
```

### Theme Application
After room/corridor generation:
1. Random floor tiles (WaterChance %) → convert to Water
2. Random floor tiles (LavaChance %) → convert to Lava
3. Water/Lava tiles must not block the only path (re-validate after placement, or only place in rooms with multiple exits)
4. If HasDoors: scan room perimeters for corridor connections → place DoorClosed

---

## 9. Map Dimensions

| Floor Range | Width | Height | Notes |
|-------------|-------|--------|-------|
| 1-3 | 60 | 40 | Small starter maps |
| 4-6 | 80 | 50 | Standard |
| 7+ | 100 | 60 | Large |

---

## 10. Test Scenarios (12)

| # | Test Scenario | Expected |
|---|---------------|----------|
| G1 | Generate floor 1 with seed 42 | Valid LevelBlueprint returned, no nulls |
| G2 | Same seed produces identical output | Two calls with same params yield byte-identical tiles |
| G3 | Different seeds produce different maps | Two different seeds → different tile layouts |
| G4 | All rooms reachable from StairsUp | FloodFill from stairs reaches all rooms |
| G5 | StairsDown reachable from StairsUp | FloodFill validates path exists |
| G6 | Player spawn position is on StairsUp | blueprint.PlayerSpawn == blueprint.StairsUp |
| G7 | No enemies in player's starting room (first 2) | First 2 enemies are in different rooms |
| G8 | Enemy count matches formula | 3 + floor*2 enemies (capped at 20) |
| G9 | BSP produces at least 4 rooms | GetLeaves().Count() >= 4 for 60×40 map |
| G10 | Prefab loads from JSON correctly | Deserialized prefab has correct dimensions and tiles |
| G11 | L-shaped corridor connects two points | All tiles along corridor are Floor |
| G12 | Biome selection by floor | Floor 1 → "dungeon", floor 5 → "cave" |

---

## 11. Dependencies

| Dependency | Provider | Notes |
|------------|----------|-------|
| `LevelBlueprint` DTO | Agent 1 | Output format |
| `TileType` enum | Agent 1 | Tile values |
| `EnemyData` content | Agent 9 | For spawn weight filtering |
| `ItemData` content | Agent 9 | For loot table selection |
| `IContentDB` | Agent 1 | Query content for spawn selection |

**Stub strategy**: Generation tests use hardcoded enemy/item lists instead of ContentDatabase.
