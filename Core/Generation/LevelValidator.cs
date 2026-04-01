using System.Collections.Generic;

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

        var reachable = FloodFill(world, data.PlayerSpawn);
        if (reachable.Count == 0)
        {
            errors.Add("Player spawn is not on a traversable tile.");
            return errors;
        }

        if (!reachable.Contains(data.StairsDown))
        {
            errors.Add("Stairs down is not reachable from player spawn.");
        }

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

                if (IsTraversable(world.GetTile(position)) && !reachable.Contains(position))
                {
                    errors.Add($"Disconnected walkable tile found at {position}.");
                    return errors;
                }
            }
        }

        return errors;
    }

    public static HashSet<Position> FloodFill(IWorldState world, Position start)
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
            for (var i = 0; i < Position.Cardinals.Length; i++)
            {
                var next = current + Position.Cardinals[i];
                if (!world.InBounds(next) || reachable.Contains(next) || !IsTraversable(world.GetTile(next)))
                {
                    continue;
                }

                reachable.Add(next);
                queue.Enqueue(next);
            }
        }

        return reachable;
    }

    public static bool IsTraversable(TileType tile)
    {
        return tile is TileType.Floor or TileType.Door or TileType.StairsDown or TileType.StairsUp or TileType.Water;
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