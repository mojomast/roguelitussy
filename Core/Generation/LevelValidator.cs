using System.Collections.Generic;

namespace Roguelike.Core.Generation;

public static class LevelValidator
{
    public static IReadOnlyList<string> Validate(IWorldState world, LevelData data)
    {
        var errors = new List<string>();

        if (!world.InBounds(data.PlayerSpawn))
            errors.Add("PlayerSpawn is out of bounds.");
        else if (!IsFloorLike(world.GetTile(data.PlayerSpawn)))
            errors.Add("PlayerSpawn is not on a walkable tile.");

        if (!world.InBounds(data.StairsDown))
            errors.Add("StairsDown is out of bounds.");
        else if (world.GetTile(data.StairsDown) != TileType.StairsDown)
            errors.Add("StairsDown position does not have StairsDown tile.");

        var reachable = FloodFill(world, data.PlayerSpawn);

        if (!reachable.Contains(data.StairsDown))
            errors.Add("StairsDown is not reachable from PlayerSpawn.");

        foreach (var room in data.Rooms)
        {
            if (!reachable.Contains(room.Center))
                errors.Add($"Room at ({room.X},{room.Y}) center is not reachable from PlayerSpawn.");
        }

        foreach (var spawn in data.EnemySpawns)
        {
            if (!world.InBounds(spawn) || !IsFloorLike(world.GetTile(spawn)))
                errors.Add($"Enemy spawn ({spawn.X},{spawn.Y}) is not on a walkable tile.");
            else if (!reachable.Contains(spawn))
                errors.Add($"Enemy spawn ({spawn.X},{spawn.Y}) is not reachable from PlayerSpawn.");
        }

        foreach (var spawn in data.ItemSpawns)
        {
            if (!world.InBounds(spawn) || !IsFloorLike(world.GetTile(spawn)))
                errors.Add($"Item spawn ({spawn.X},{spawn.Y}) is not on a walkable tile.");
            else if (!reachable.Contains(spawn))
                errors.Add($"Item spawn ({spawn.X},{spawn.Y}) is not reachable from PlayerSpawn.");
        }

        if (data.Rooms.Count == 0)
            errors.Add("Level has no rooms.");

        return errors;
    }

    private static HashSet<Position> FloodFill(IWorldState world, Position start)
    {
        var visited = new HashSet<Position>();
        if (!world.InBounds(start) || !IsFloorLike(world.GetTile(start)))
            return visited;

        var queue = new Queue<Position>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in GetNeighbors(current))
            {
                if (!visited.Contains(neighbor) && world.InBounds(neighbor) && IsFloorLike(world.GetTile(neighbor)))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited;
    }

    private static Position[] GetNeighbors(Position pos)
    {
        return new[]
        {
            pos.Offset(0, -1),
            pos.Offset(0, 1),
            pos.Offset(-1, 0),
            pos.Offset(1, 0),
        };
    }

    private static bool IsFloorLike(TileType tile)
    {
        return tile is TileType.Floor or TileType.Door or TileType.StairsDown
            or TileType.StairsUp or TileType.Water;
    }
}
