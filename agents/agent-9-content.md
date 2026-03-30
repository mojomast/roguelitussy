# Agent 9: Content Agent — Detailed Specification

## Mission
Define all game content: items, enemies, status effects, room prefabs, loot tables, and difficulty curve. Provide balanced stats, interesting variety, and content validation rules. All content is data-driven via JSON files — no hardcoded stats in code.

---

## 1. Files to Create

| File | Purpose |
|------|---------|
| `Content/Items/items.json` | Complete item definitions |
| `Content/Enemies/enemies.json` | Complete enemy definitions |
| `Content/StatusEffects/effects.json` | Status effect definitions |
| `Content/Rooms/prefabs.json` | Room prefab layouts |
| `Content/LootTables/loot.json` | Loot drop tables |
| `Core/Content/ContentLoader.cs` | JSON loading + validation |
| `Core/Content/LootTableResolver.cs` | Weighted loot selection |
| `Core/Content/DifficultyScaler.cs` | Floor-based difficulty scaling |
| `Tests/ContentTests/ContentValidationTests.cs` | Content integrity tests |
| `Tests/ContentTests/BalanceTests.cs` | Balance sanity checks |

---

## 2. Items (15 items)

```json
[
  {
    "id": "health_potion",
    "name": "Health Potion",
    "description": "A crimson potion that restores vitality.",
    "spriteId": "items/potion_red",
    "category": "potion",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 25,
    "appliesEffect": null,
    "effectDuration": 0,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 15,
    "consumable": true,
    "equippable": false,
    "dropWeight": 150,
    "minFloor": 1
  },
  {
    "id": "large_health_potion",
    "name": "Large Health Potion",
    "description": "A large flask of restorative liquid. Heals considerably.",
    "spriteId": "items/potion_red_large",
    "category": "potion",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 50,
    "appliesEffect": null,
    "effectDuration": 0,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 40,
    "consumable": true,
    "equippable": false,
    "dropWeight": 60,
    "minFloor": 4
  },
  {
    "id": "antidote",
    "name": "Antidote",
    "description": "Cures poison immediately.",
    "spriteId": "items/potion_green",
    "category": "potion",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 5,
    "appliesEffect": "remove_poison",
    "effectDuration": 0,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 20,
    "consumable": true,
    "equippable": false,
    "dropWeight": 80,
    "minFloor": 2
  },
  {
    "id": "haste_potion",
    "name": "Potion of Haste",
    "description": "Increases movement speed for several turns.",
    "spriteId": "items/potion_yellow",
    "category": "potion",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": "Haste",
    "effectDuration": 10,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 30,
    "consumable": true,
    "equippable": false,
    "dropWeight": 50,
    "minFloor": 3
  },
  {
    "id": "iron_sword",
    "name": "Iron Sword",
    "description": "A simple but reliable blade.",
    "spriteId": "items/sword_iron",
    "category": "weapon",
    "attackBonus": 3,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": null,
    "effectDuration": 0,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 25,
    "consumable": false,
    "equippable": true,
    "dropWeight": 80,
    "minFloor": 1
  },
  {
    "id": "steel_sword",
    "name": "Steel Sword",
    "description": "A well-forged blade with a keen edge.",
    "spriteId": "items/sword_steel",
    "category": "weapon",
    "attackBonus": 6,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": null,
    "effectDuration": 0,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 60,
    "consumable": false,
    "equippable": true,
    "dropWeight": 40,
    "minFloor": 4
  },
  {
    "id": "fire_dagger",
    "name": "Fire Dagger",
    "description": "A short blade wreathed in flame. Burns on contact.",
    "spriteId": "items/dagger_fire",
    "category": "weapon",
    "attackBonus": 4,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": "Burn",
    "effectDuration": 3,
    "effectRadius": 0,
    "damageType": "Fire",
    "value": 50,
    "consumable": false,
    "equippable": true,
    "dropWeight": 30,
    "minFloor": 3
  },
  {
    "id": "leather_armor",
    "name": "Leather Armor",
    "description": "Light protection made from treated hide.",
    "spriteId": "items/armor_leather",
    "category": "armor",
    "attackBonus": 0,
    "defenseBonus": 2,
    "healAmount": 0,
    "appliesEffect": null,
    "effectDuration": 0,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 20,
    "consumable": false,
    "equippable": true,
    "dropWeight": 80,
    "minFloor": 1
  },
  {
    "id": "chain_mail",
    "name": "Chain Mail",
    "description": "Interlocking metal rings provide solid defense.",
    "spriteId": "items/armor_chain",
    "category": "armor",
    "attackBonus": 0,
    "defenseBonus": 5,
    "healAmount": 0,
    "appliesEffect": null,
    "effectDuration": 0,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 75,
    "consumable": false,
    "equippable": true,
    "dropWeight": 30,
    "minFloor": 5
  },
  {
    "id": "fire_scroll",
    "name": "Scroll of Fireball",
    "description": "Unleashes a burst of flame in a small area.",
    "spriteId": "items/scroll_red",
    "category": "scroll",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": "Burn",
    "effectDuration": 3,
    "effectRadius": 2,
    "damageType": "Fire",
    "value": 35,
    "consumable": true,
    "equippable": false,
    "dropWeight": 50,
    "minFloor": 2
  },
  {
    "id": "ice_scroll",
    "name": "Scroll of Frost",
    "description": "A wave of cold freezes enemies solid.",
    "spriteId": "items/scroll_blue",
    "category": "scroll",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": "Freeze",
    "effectDuration": 4,
    "effectRadius": 2,
    "damageType": "Ice",
    "value": 35,
    "consumable": true,
    "equippable": false,
    "dropWeight": 50,
    "minFloor": 2
  },
  {
    "id": "shield_scroll",
    "name": "Scroll of Shielding",
    "description": "Surrounds you with a magical barrier.",
    "spriteId": "items/scroll_gold",
    "category": "scroll",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": "Shield",
    "effectDuration": 8,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 40,
    "consumable": true,
    "equippable": false,
    "dropWeight": 40,
    "minFloor": 3
  },
  {
    "id": "bread",
    "name": "Bread",
    "description": "Stale but nourishing. Restores a small amount of health.",
    "spriteId": "items/bread",
    "category": "food",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 10,
    "appliesEffect": null,
    "effectDuration": 0,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 5,
    "consumable": true,
    "equippable": false,
    "dropWeight": 120,
    "minFloor": 1
  },
  {
    "id": "poison_dagger",
    "name": "Venomous Dagger",
    "description": "A dagger coated in deadly venom.",
    "spriteId": "items/dagger_poison",
    "category": "weapon",
    "attackBonus": 2,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": "Poison",
    "effectDuration": 5,
    "effectRadius": 0,
    "damageType": "Poison",
    "value": 45,
    "consumable": false,
    "equippable": true,
    "dropWeight": 35,
    "minFloor": 3
  },
  {
    "id": "regen_ring",
    "name": "Ring of Regeneration",
    "description": "Slowly heals the wearer over time. Permanent effect while equipped.",
    "spriteId": "items/ring_green",
    "category": "armor",
    "attackBonus": 0,
    "defenseBonus": 0,
    "healAmount": 0,
    "appliesEffect": "Regeneration",
    "effectDuration": 999,
    "effectRadius": 0,
    "damageType": "Physical",
    "value": 100,
    "consumable": false,
    "equippable": true,
    "dropWeight": 10,
    "minFloor": 6
  }
]
```

### Item Balance Summary

| Category | Count | Floor Range | Notes |
|----------|-------|-------------|-------|
| Potions | 4 | 1-4+ | Core survival items, common drops |
| Weapons | 4 | 1-3+ | Progressive attack upgrades |
| Armor | 3 (incl. ring) | 1-6+ | Progressive defense upgrades |
| Scrolls | 3 | 2-3+ | AoE and utility effects |
| Food | 1 | 1+ | Very common, small heal |

---

## 3. Enemies (7 enemy types)

```json
[
  {
    "id": "rat",
    "name": "Giant Rat",
    "spriteId": "enemies/rat",
    "hp": 8,
    "attack": 3,
    "defense": 1,
    "speed": 120,
    "viewRadius": 5,
    "aiProfile": "cowardly",
    "xpValue": 5,
    "lootTableId": "common_trash",
    "minFloor": 1,
    "maxFloor": 4,
    "spawnWeight": 150,
    "faction": "Enemy",
    "damageType": "Physical",
    "onDeathEffect": null
  },
  {
    "id": "skeleton",
    "name": "Skeleton",
    "spriteId": "enemies/skeleton",
    "hp": 15,
    "attack": 5,
    "defense": 3,
    "speed": 90,
    "viewRadius": 6,
    "aiProfile": "aggressive",
    "xpValue": 15,
    "lootTableId": "common_weapon",
    "minFloor": 1,
    "maxFloor": 6,
    "spawnWeight": 120,
    "faction": "Enemy",
    "damageType": "Physical",
    "onDeathEffect": null
  },
  {
    "id": "goblin",
    "name": "Goblin",
    "spriteId": "enemies/goblin",
    "hp": 12,
    "attack": 4,
    "defense": 2,
    "speed": 110,
    "viewRadius": 7,
    "aiProfile": "cautious",
    "xpValue": 12,
    "lootTableId": "common_potion",
    "minFloor": 2,
    "maxFloor": 7,
    "spawnWeight": 100,
    "faction": "Enemy",
    "damageType": "Physical",
    "onDeathEffect": null
  },
  {
    "id": "slime",
    "name": "Acid Slime",
    "spriteId": "enemies/slime",
    "hp": 20,
    "attack": 3,
    "defense": 1,
    "speed": 60,
    "viewRadius": 4,
    "aiProfile": "aggressive",
    "xpValue": 10,
    "lootTableId": "common_trash",
    "minFloor": 1,
    "maxFloor": 5,
    "spawnWeight": 100,
    "faction": "Enemy",
    "damageType": "Poison",
    "onDeathEffect": "poison_cloud"
  },
  {
    "id": "fire_imp",
    "name": "Fire Imp",
    "spriteId": "enemies/fire_imp",
    "hp": 10,
    "attack": 7,
    "defense": 2,
    "speed": 130,
    "viewRadius": 8,
    "aiProfile": "cautious",
    "xpValue": 20,
    "lootTableId": "rare_scroll",
    "minFloor": 4,
    "maxFloor": 9,
    "spawnWeight": 60,
    "faction": "Enemy",
    "damageType": "Fire",
    "onDeathEffect": null
  },
  {
    "id": "knight",
    "name": "Dark Knight",
    "spriteId": "enemies/knight",
    "hp": 35,
    "attack": 9,
    "defense": 7,
    "speed": 80,
    "viewRadius": 6,
    "aiProfile": "berserker",
    "xpValue": 40,
    "lootTableId": "rare_weapon",
    "minFloor": 5,
    "maxFloor": 99,
    "spawnWeight": 40,
    "faction": "Enemy",
    "damageType": "Physical",
    "onDeathEffect": null
  },
  {
    "id": "wraith",
    "name": "Wraith",
    "spriteId": "enemies/wraith",
    "hp": 18,
    "attack": 8,
    "defense": 4,
    "speed": 100,
    "viewRadius": 10,
    "aiProfile": "aggressive",
    "xpValue": 30,
    "lootTableId": "rare_scroll",
    "minFloor": 6,
    "maxFloor": 99,
    "spawnWeight": 35,
    "faction": "Enemy",
    "damageType": "Ice",
    "onDeathEffect": null
  }
]
```

### Enemy Design Notes

| Enemy | Role | Unique Behavior |
|-------|------|-----------------|
| **Giant Rat** | Weak early-game fodder | Fast (speed 120), cowardly (flees at 50% HP) |
| **Skeleton** | Standard melee threat | Slow (speed 90), aggressive, never flees |
| **Goblin** | Cautious mid-range | Cautious AI, flees at 35% HP, faster than average |
| **Acid Slime** | Tanky poison threat | Very slow (speed 60), high HP, poison damage, poison cloud on death |
| **Fire Imp** | Glass cannon | Very fast (130), high attack, low HP, fire damage |
| **Dark Knight** | Late-game tank | Very high HP+DEF, berserker AI (never flees), slow |
| **Wraith** | Late-game assassin | Long view radius (10), ice damage, standard speed |

### Player Starting Stats (for reference)

```
HP: 100, MaxHP: 100
Attack: 8, Defense: 4
Speed: 100, ViewRadius: 8
Inventory: empty (or 1 health_potion on easy mode)
```

---

## 4. Status Effects

```json
[
  {
    "id": "poison",
    "type": "Poison",
    "name": "Poison",
    "description": "Deals 2 damage per turn per stack.",
    "defaultDuration": 5,
    "tickDamage": 2,
    "stackable": true,
    "maxStacks": 5,
    "spriteId": "effects/poison"
  },
  {
    "id": "burn",
    "type": "Burn",
    "name": "Burn",
    "description": "Deals 3 fire damage per turn.",
    "defaultDuration": 3,
    "tickDamage": 3,
    "stackable": false,
    "maxStacks": 1,
    "spriteId": "effects/burn"
  },
  {
    "id": "freeze",
    "type": "Freeze",
    "name": "Freeze",
    "description": "Reduces movement speed by 50%.",
    "defaultDuration": 4,
    "tickDamage": 0,
    "stackable": false,
    "maxStacks": 1,
    "spriteId": "effects/freeze"
  },
  {
    "id": "haste",
    "type": "Haste",
    "name": "Haste",
    "description": "Increases movement speed by 50%.",
    "defaultDuration": 10,
    "tickDamage": 0,
    "stackable": false,
    "maxStacks": 1,
    "spriteId": "effects/haste"
  },
  {
    "id": "shield",
    "type": "Shield",
    "name": "Shield",
    "description": "Adds +3 defense per stack.",
    "defaultDuration": 8,
    "tickDamage": 0,
    "stackable": true,
    "maxStacks": 3,
    "spriteId": "effects/shield"
  },
  {
    "id": "confusion",
    "type": "Confusion",
    "name": "Confusion",
    "description": "50% chance to move in random direction.",
    "defaultDuration": 5,
    "tickDamage": 0,
    "stackable": false,
    "maxStacks": 1,
    "spriteId": "effects/confusion"
  },
  {
    "id": "blind",
    "type": "Blind",
    "name": "Blind",
    "description": "Reduces view radius to 1 tile.",
    "defaultDuration": 6,
    "tickDamage": 0,
    "stackable": false,
    "maxStacks": 1,
    "spriteId": "effects/blind"
  },
  {
    "id": "regeneration",
    "type": "Regeneration",
    "name": "Regeneration",
    "description": "Heals 2 HP per turn per stack.",
    "defaultDuration": 10,
    "tickDamage": 0,
    "stackable": true,
    "maxStacks": 3,
    "spriteId": "effects/regen"
  }
]
```

---

## 5. Room Prefabs (5 designs)

```json
{
  "prefabs": [
    {
      "id": "small_square",
      "width": 7,
      "height": 7,
      "tiles": [
        ".......",
        ".fffff.",
        ".fffff.",
        ".fffff.",
        ".fffff.",
        ".fffff.",
        "......."
      ],
      "legend": { ".": "Wall", "f": "Floor" },
      "tags": ["common", "dungeon", "cave", "volcano"],
      "spawnPoints": [
        { "x": 3, "y": 3, "type": "any" }
      ],
      "minFloor": 1,
      "maxFloor": 99
    },
    {
      "id": "large_hall",
      "width": 12,
      "height": 8,
      "tiles": [
        "............",
        ".ffffffffff.",
        ".ffffffffff.",
        ".ffffffffff.",
        ".ffffffffff.",
        ".ffffffffff.",
        ".ffffffffff.",
        "............"
      ],
      "legend": { ".": "Wall", "f": "Floor" },
      "tags": ["common", "dungeon"],
      "spawnPoints": [
        { "x": 3, "y": 3, "type": "enemy" },
        { "x": 8, "y": 5, "type": "enemy" },
        { "x": 6, "y": 4, "type": "item" }
      ],
      "minFloor": 1,
      "maxFloor": 99
    },
    {
      "id": "l_shaped",
      "width": 10,
      "height": 10,
      "tiles": [
        "..........",
        ".fffff....",
        ".fffff....",
        ".fffff....",
        ".fffff....",
        ".ffffffff.",
        ".ffffffff.",
        ".ffffffff.",
        ".ffffffff.",
        ".........."
      ],
      "legend": { ".": "Wall", "f": "Floor" },
      "tags": ["uncommon", "dungeon", "cave"],
      "spawnPoints": [
        { "x": 3, "y": 2, "type": "enemy" },
        { "x": 7, "y": 7, "type": "item" }
      ],
      "minFloor": 2,
      "maxFloor": 99
    },
    {
      "id": "pillared_room",
      "width": 9,
      "height": 9,
      "tiles": [
        ".........",
        ".fffffff.",
        ".f.f.f.f.",
        ".fffffff.",
        ".f.f.f.f.",
        ".fffffff.",
        ".f.f.f.f.",
        ".fffffff.",
        "........."
      ],
      "legend": { ".": "Wall", "f": "Floor" },
      "tags": ["uncommon", "dungeon"],
      "spawnPoints": [
        { "x": 4, "y": 4, "type": "enemy" },
        { "x": 2, "y": 2, "type": "item" },
        { "x": 6, "y": 6, "type": "item" }
      ],
      "minFloor": 3,
      "maxFloor": 99
    },
    {
      "id": "water_cave",
      "width": 9,
      "height": 7,
      "tiles": [
        ".........",
        ".fffwfff.",
        ".ffwwwff.",
        ".fwwwwwf.",
        ".ffwwwff.",
        ".fffwfff.",
        "........."
      ],
      "legend": { ".": "Wall", "f": "Floor", "w": "Water" },
      "tags": ["cave", "water"],
      "spawnPoints": [
        { "x": 1, "y": 1, "type": "any" },
        { "x": 7, "y": 5, "type": "any" }
      ],
      "minFloor": 4,
      "maxFloor": 99
    }
  ]
}
```

---

## 6. Loot Tables

```json
{
  "tables": [
    {
      "id": "common_trash",
      "entries": [
        { "itemId": "bread", "weight": 60 },
        { "itemId": "health_potion", "weight": 30 },
        { "itemId": null, "weight": 40 }
      ]
    },
    {
      "id": "common_potion",
      "entries": [
        { "itemId": "health_potion", "weight": 50 },
        { "itemId": "antidote", "weight": 30 },
        { "itemId": "haste_potion", "weight": 20 },
        { "itemId": null, "weight": 20 }
      ]
    },
    {
      "id": "common_weapon",
      "entries": [
        { "itemId": "iron_sword", "weight": 40 },
        { "itemId": "health_potion", "weight": 30 },
        { "itemId": null, "weight": 30 }
      ]
    },
    {
      "id": "rare_weapon",
      "entries": [
        { "itemId": "steel_sword", "weight": 30 },
        { "itemId": "fire_dagger", "weight": 20 },
        { "itemId": "poison_dagger", "weight": 20 },
        { "itemId": "chain_mail", "weight": 15 },
        { "itemId": "health_potion", "weight": 15 }
      ]
    },
    {
      "id": "rare_scroll",
      "entries": [
        { "itemId": "fire_scroll", "weight": 30 },
        { "itemId": "ice_scroll", "weight": 30 },
        { "itemId": "shield_scroll", "weight": 20 },
        { "itemId": "haste_potion", "weight": 20 }
      ]
    },
    {
      "id": "floor_loot",
      "entries": [
        { "itemId": "health_potion", "weight": 100 },
        { "itemId": "bread", "weight": 80 },
        { "itemId": "antidote", "weight": 40 },
        { "itemId": "iron_sword", "weight": 30 },
        { "itemId": "leather_armor", "weight": 30 },
        { "itemId": "fire_scroll", "weight": 20 },
        { "itemId": "ice_scroll", "weight": 20 },
        { "itemId": "haste_potion", "weight": 15 },
        { "itemId": "shield_scroll", "weight": 15 }
      ]
    }
  ]
}
```

### Loot Table Resolution

```csharp
public static class LootTableResolver
{
    /// <summary>
    /// Pick a random item from a loot table using weighted selection.
    /// Returns null if the "nothing" entry is selected.
    /// </summary>
    public static string? Roll(LootTable table, Random rng)
    {
        int totalWeight = table.Entries.Sum(e => e.Weight);
        int roll = rng.Next(totalWeight);

        int cumulative = 0;
        foreach (var entry in table.Entries)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry.ItemId; // null = no drop
        }

        return null; // fallback
    }

    /// <summary>
    /// Pick a floor-appropriate item for ground spawns.
    /// Filters by minFloor then uses the floor_loot table.
    /// </summary>
    public static string? RollFloorLoot(int floor, IContentDB contentDB, LootTable floorTable, Random rng)
    {
        // Filter entries by item's minFloor
        var valid = floorTable.Entries
            .Where(e => e.ItemId == null || (contentDB.GetItem(e.ItemId)?.MinFloor ?? 1) <= floor)
            .ToList();

        if (valid.Count == 0) return null;

        int totalWeight = valid.Sum(e => e.Weight);
        int roll = rng.Next(totalWeight);
        int cumulative = 0;
        foreach (var entry in valid)
        {
            cumulative += entry.Weight;
            if (roll < cumulative) return entry.ItemId;
        }
        return null;
    }
}

public class LootTable
{
    public string Id { get; set; } = "";
    public List<LootEntry> Entries { get; set; } = new();
}

public class LootEntry
{
    public string? ItemId { get; set; }  // null = no drop
    public int Weight { get; set; }
}
```

---

## 7. Difficulty Curve Design

### Floor Scaling

| Floor | Enemy Count | Item Count | Enemy Pool | Biome |
|-------|-------------|------------|------------|-------|
| 1 | 5 | 3 | Rat, Skeleton, Slime | Dungeon |
| 2 | 7 | 3 | Rat, Skeleton, Goblin, Slime | Dungeon |
| 3 | 9 | 4 | Skeleton, Goblin, Slime | Dungeon |
| 4 | 11 | 5 | Goblin, Slime, Fire Imp | Cave |
| 5 | 13 | 6 | Goblin, Fire Imp, Dark Knight | Cave |
| 6 | 15 | 7 | Fire Imp, Dark Knight, Wraith | Cave |
| 7 | 17 | 8 | Dark Knight, Wraith | Volcano |
| 8 | 19 | 9 | Dark Knight, Wraith, Fire Imp | Volcano |
| 9 | 20 | 10 | All late-game | Volcano |

### Formulas

```
enemy_count(floor) = min(20, 3 + floor * 2)
item_count(floor)  = min(10, 2 + floor)
```

### Player Power Budget

Expected player stats by floor (assuming average loot luck):

| Floor | HP | Attack (w/ weapon) | Defense (w/ armor) |
|-------|----|--------------------|-------------------|
| 1 | 100 | 8 (+0) = 8 | 4 (+0) = 4 |
| 3 | 100 | 8 (+3) = 11 | 4 (+2) = 6 |
| 5 | ~80 | 8 (+6) = 14 | 4 (+5) = 9 |
| 7 | ~60 | 8 (+6) = 14 | 4 (+5) = 9 |
| 9 | ~40 | 8 (+6) = 14 | 4 (+5) = 9 |

### Expected Damage Exchange (Player vs Enemy)

| Matchup | Turns to Kill Enemy | Turns for Enemy to Kill Player |
|---------|--------------------|-----------------------------|
| Player (8 ATK) vs Rat (8 HP, 1 DEF) | ~2 turns | ~20 turns |
| Player (11 ATK) vs Skeleton (15 HP, 3 DEF) | ~3 turns | ~12 turns |
| Player (14 ATK) vs Dark Knight (35 HP, 7 DEF) | ~7 turns | ~8 turns |
| Player (14 ATK) vs Wraith (18 HP, 4 DEF) | ~3 turns | ~8 turns |

**Design intent**: Early floors are easy (build confidence), mid floors ramp up, late floors are deadly. Player should use items strategically to survive floors 7+.

---

## 8. Content Validation Rules

```csharp
public static class ContentValidator
{
    public static List<string> ValidateItems(IReadOnlyDictionary<string, ItemData> items)
    {
        var errors = new List<string>();

        foreach (var (id, item) in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                errors.Add("Item has empty ID");
            if (id != item.Id)
                errors.Add($"Item key '{id}' doesn't match item.Id '{item.Id}'");
            if (string.IsNullOrWhiteSpace(item.Name))
                errors.Add($"Item '{id}' has no name");
            if (item.Consumable && item.Equippable)
                errors.Add($"Item '{id}' is both consumable and equippable");
            if (item.Equippable && item.AttackBonus == 0 && item.DefenseBonus == 0 && item.AppliesEffect == null)
                errors.Add($"Equippable item '{id}' has no stat bonuses or effects");
            if (item.HealAmount < 0)
                errors.Add($"Item '{id}' has negative heal amount");
            if (item.MinFloor < 1)
                errors.Add($"Item '{id}' has invalid min floor");
            if (item.DropWeight <= 0)
                errors.Add($"Item '{id}' has non-positive drop weight");
        }

        // Check for duplicate IDs (shouldn't happen with dict, but validate source JSON)
        return errors;
    }

    public static List<string> ValidateEnemies(IReadOnlyDictionary<string, EnemyData> enemies)
    {
        var errors = new List<string>();

        foreach (var (id, enemy) in enemies)
        {
            if (string.IsNullOrWhiteSpace(enemy.Id))
                errors.Add("Enemy has empty ID");
            if (enemy.HP <= 0)
                errors.Add($"Enemy '{id}' has non-positive HP");
            if (enemy.Attack <= 0)
                errors.Add($"Enemy '{id}' has non-positive attack");
            if (enemy.Speed <= 0)
                errors.Add($"Enemy '{id}' has non-positive speed");
            if (enemy.MinFloor > enemy.MaxFloor)
                errors.Add($"Enemy '{id}' minFloor ({enemy.MinFloor}) > maxFloor ({enemy.MaxFloor})");
            if (enemy.SpawnWeight <= 0)
                errors.Add($"Enemy '{id}' has non-positive spawn weight");

            // Validate AI profile exists
            var validProfiles = new[] { "aggressive", "cautious", "ranged", "cowardly", "berserker" };
            if (!validProfiles.Contains(enemy.AIProfile))
                errors.Add($"Enemy '{id}' has unknown AI profile '{enemy.AIProfile}'");
        }

        return errors;
    }

    public static List<string> ValidateLootTables(List<LootTable> tables, IReadOnlyDictionary<string, ItemData> items)
    {
        var errors = new List<string>();

        foreach (var table in tables)
        {
            if (string.IsNullOrWhiteSpace(table.Id))
                errors.Add("Loot table has empty ID");

            foreach (var entry in table.Entries)
            {
                if (entry.ItemId != null && !items.ContainsKey(entry.ItemId))
                    errors.Add($"Loot table '{table.Id}' references unknown item '{entry.ItemId}'");
                if (entry.Weight <= 0)
                    errors.Add($"Loot table '{table.Id}' has non-positive weight for '{entry.ItemId}'");
            }
        }

        return errors;
    }
}
```

---

## 9. Test Scenarios (10)

| # | Test Scenario | Expected |
|---|---------------|----------|
| C1 | All items deserialize from JSON | 15 items loaded, all fields populated |
| C2 | All enemies deserialize from JSON | 7 enemies loaded, all fields populated |
| C3 | All status effects deserialize | 8 effects loaded |
| C4 | All room prefabs deserialize | 5 prefabs loaded, tile arrays correct dimensions |
| C5 | Item validation passes | No errors for provided items.json |
| C6 | Enemy validation passes | No errors for provided enemies.json |
| C7 | Loot table validation passes | All referenced items exist |
| C8 | Invalid item caught | Item with negative HP returns validation error |
| C9 | Loot table roll returns valid item | 1000 rolls from "floor_loot" all return valid item IDs or null |
| C10 | Floor-filtered loot excludes high-floor items | Floor 1 loot never includes minFloor=4+ items |

---

## 10. Dependencies

| Dependency | Provider | Notes |
|------------|----------|-------|
| DTOs (ItemData, EnemyData, etc.) | Agent 1 | Must match content JSON schema |
| ContentDatabase autoload | Agent 1 | Loads these JSON files |
| LootTableResolver | Self | Implements resolution logic |
| AI profiles referenced | Agent 5 | Enemy AIProfile strings must match |
| Generation uses content | Agent 4 | Spawn selection queries content |
| Persistence saves item IDs | Agent 8 | IDs must remain stable across versions |
