using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Core;

public static class LevelValidator
{
    public static IReadOnlyList<string> Validate(IWorldState world, LevelData data)
    {
        var errors = new List<string>();

        if (data.Rooms.Count < 4)
        {
            errors.Add("Generated level must contain at least four rooms.");
        }

        if (!world.InBounds(data.PlayerSpawn))
        {
            errors.Add("Player spawn is out of bounds.");
            return errors;
        }

        if (world.GetTile(data.PlayerSpawn) != TileType.StairsUp)
        {
            errors.Add("Player spawn must be on a stairs up tile.");
        }

        if (!world.InBounds(data.StairsDown))
        {
            errors.Add("Stairs down position is out of bounds.");
            return errors;
        }

        if (!IsTraversable(world.GetTile(data.StairsDown)))
        {
            errors.Add("Stairs down must be on a traversable tile.");
        }

        var reachable = FloodFill(world, data.PlayerSpawn, includeLockedDoors: true);
        if (reachable.Count == 0)
        {
            errors.Add("Player spawn is not on a traversable tile.");
            return errors;
        }

        if (!reachable.Contains(data.StairsDown))
        {
            errors.Add("Stairs down is not reachable from player spawn.");
        }

        ValidateKeySolvability(world, data, errors);

        for (var i = 0; i < data.EnemySpawns.Count; i++)
        {
            if (!reachable.Contains(data.EnemySpawns[i]))
            {
                errors.Add($"Enemy spawn at {data.EnemySpawns[i]} is unreachable.");
            }
        }

        foreach (var spawn in data.EnemySpawnDetails ?? System.Array.Empty<EnemySpawnData>())
        {
            if (!reachable.Contains(spawn.Position))
            {
                errors.Add($"Enemy spawn at {spawn.Position} is unreachable.");
            }
        }

        for (var i = 0; i < data.ItemSpawns.Count; i++)
        {
            if (!reachable.Contains(data.ItemSpawns[i]))
            {
                errors.Add($"Item spawn at {data.ItemSpawns[i]} is unreachable.");
            }
        }

        foreach (var spawn in data.ItemSpawnDetails ?? System.Array.Empty<ItemSpawnData>())
        {
            if (!reachable.Contains(spawn.Position))
            {
                errors.Add($"Item spawn at {spawn.Position} is unreachable.");
            }
        }

        foreach (var spawn in data.ChestSpawnDetails ?? System.Array.Empty<ChestSpawnData>())
        {
            if (!reachable.Contains(spawn.Position))
            {
                errors.Add($"Chest spawn at {spawn.Position} is unreachable.");
            }
        }

        foreach (var spawn in data.NpcSpawns ?? System.Array.Empty<NpcSpawnData>())
        {
            if (!reachable.Contains(spawn.Position))
            {
                errors.Add($"Npc spawn at {spawn.Position} is unreachable.");
            }
        }

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.GetTile(position) == TileType.Door && !HasValidDoorwayShape(world, position))
                {
                    errors.Add($"Door at {position} is not connected to a valid doorway.");
                    return errors;
                }

                if (IsTraversable(world.GetTile(position), includeLockedDoors: true) && !reachable.Contains(position))
                {
                    errors.Add($"Disconnected walkable tile found at {position}.");
                    return errors;
                }
            }
        }

        return errors;
    }

    private static void ValidateKeySolvability(IWorldState world, LevelData data, ICollection<string> errors)
    {
        var lockedDoors = new HashSet<Position>(data.LockedDoors ?? System.Array.Empty<Position>());
        var keys = new HashSet<Position>(data.KeySpawns ?? System.Array.Empty<Position>());
        var consumedKeys = new HashSet<Position>();
        var unlockedDoors = new HashSet<Position>();
        var reachable = FloodFillWithUnlockedDoors(world, data.PlayerSpawn, unlockedDoors);

        while (true)
        {
            // Nullable results distinguish a missing candidate from the valid (0,0) position.
            var availableKey = keys
                .Where(key => reachable.Contains(key) && !consumedKeys.Contains(key))
                .OrderBy(key => key.Y)
                .ThenBy(key => key.X)
                .Select(key => (Position?)key)
                .FirstOrDefault();
            var door = lockedDoors
                .Where(candidate =>
                !unlockedDoors.Contains(candidate) &&
                Position.AllDirections.Any(delta => reachable.Contains(candidate + delta)))
                .OrderBy(candidate => candidate.Y)
                .ThenBy(candidate => candidate.X)
                .Select(candidate => (Position?)candidate)
                .FirstOrDefault();
            if (!availableKey.HasValue || !door.HasValue)
            {
                break;
            }

            consumedKeys.Add(availableKey.Value);
            unlockedDoors.Add(door.Value);
            reachable = FloodFillWithUnlockedDoors(world, data.PlayerSpawn, unlockedDoors);
        }

        if (unlockedDoors.Count != lockedDoors.Count)
        {
            errors.Add("Locked doors cannot all be opened with reachable key spawns.");
        }

        var objectives = new List<Position> { data.StairsDown };
        objectives.AddRange(data.EnemySpawns);
        objectives.AddRange(data.ItemSpawns);
        objectives.AddRange(data.EnemySpawnDetails?.Select(spawn => spawn.Position) ?? System.Array.Empty<Position>());
        objectives.AddRange(data.ItemSpawnDetails?.Select(spawn => spawn.Position) ?? System.Array.Empty<Position>());
        objectives.AddRange(data.ChestSpawnDetails?.Select(spawn => spawn.Position) ?? System.Array.Empty<Position>());
        objectives.AddRange(data.NpcSpawns?.Select(spawn => spawn.Position) ?? System.Array.Empty<Position>());
        objectives.AddRange(data.TrapSpawnDetails?.Select(spawn => spawn.Position) ?? System.Array.Empty<Position>());
        objectives.AddRange(data.ShrineSpawns?.Select(spawn => spawn.Position) ?? System.Array.Empty<Position>());
        objectives.AddRange(data.LandmarkSpawns?.Select(spawn => spawn.Position) ?? System.Array.Empty<Position>());
        if (objectives.Any(position => !reachable.Contains(position)))
        {
            errors.Add("A generated objective is unreachable after legal key consumption.");
        }
    }

    private static HashSet<Position> FloodFillWithUnlockedDoors(
        IWorldState world,
        Position start,
        ISet<Position> unlockedDoors)
    {
        var reachable = new HashSet<Position>();
        if (!world.InBounds(start) || !IsTraversable(world.GetTile(start)))
        {
            return reachable;
        }

        var queue = new Queue<Position>();
        queue.Enqueue(start);
        reachable.Add(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var delta in Position.Cardinals)
            {
                var next = current + delta;
                if (!world.InBounds(next) || reachable.Contains(next))
                {
                    continue;
                }

                var tile = world.GetTile(next);
                if (!IsTraversable(tile) && !(tile == TileType.LockedDoor && unlockedDoors.Contains(next)))
                {
                    continue;
                }

                reachable.Add(next);
                queue.Enqueue(next);
            }
        }

        return reachable;
    }

    public static HashSet<Position> FloodFill(IWorldState world, Position start, bool includeLockedDoors = false)
    {
        var reachable = new HashSet<Position>();
        if (!world.InBounds(start) || !IsTraversable(world.GetTile(start), includeLockedDoors))
        {
            return reachable;
        }

        var queue = new Queue<Position>();
        queue.Enqueue(start);
        reachable.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            for (var i = 0; i < Position.Cardinals.Length; i++)
            {
                var next = current + Position.Cardinals[i];
                if (!world.InBounds(next) || reachable.Contains(next) || !IsTraversable(world.GetTile(next), includeLockedDoors))
                {
                    continue;
                }

                reachable.Add(next);
                queue.Enqueue(next);
            }
        }

        return reachable;
    }

    public static bool IsTraversable(TileType tile) => IsTraversable(tile, includeLockedDoors: false);

    public static bool IsTraversable(TileType tile, bool includeLockedDoors)
    {
        // Mirrors WorldState.IsTileWalkable: water is scenery the player cannot enter,
        // so it must not count for connectivity. Locked doors are optionally traversable
        // for validation because their keys are obtainable elsewhere on the floor.
        if (tile is TileType.Floor or TileType.Door or TileType.StairsDown or TileType.StairsUp or TileType.Trap)
        {
            return true;
        }

        return includeLockedDoors && tile == TileType.LockedDoor;
    }

    private static bool HasValidDoorwayShape(IWorldState world, Position position)
    {
        var north = SupportsDoorway(world, position + new Position(0, -1));
        var east = SupportsDoorway(world, position + new Position(1, 0));
        var south = SupportsDoorway(world, position + new Position(0, 1));
        var west = SupportsDoorway(world, position + new Position(-1, 0));

        var verticalDoor = north && south && !east && !west;
        var horizontalDoor = east && west && !north && !south;
        return verticalDoor || horizontalDoor;
    }

    private static bool SupportsDoorway(IWorldState world, Position position)
    {
        return world.InBounds(position) && IsTraversable(world.GetTile(position));
    }
}
