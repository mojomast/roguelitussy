namespace Roguelike.Core;

public static class DoorSanitizer
{
    public static void Normalize(WorldState world)
    {
        var updates = new List<(Position Position, TileType TileType)>();

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.GetTile(position) != TileType.Door)
                {
                    continue;
                }

                var north = SupportsDoorway(world, position + new Position(0, -1));
                var east = SupportsDoorway(world, position + new Position(1, 0));
                var south = SupportsDoorway(world, position + new Position(0, 1));
                var west = SupportsDoorway(world, position + new Position(-1, 0));

                var verticalDoor = north && south && !east && !west;
                var horizontalDoor = east && west && !north && !south;
                if (verticalDoor || horizontalDoor)
                {
                    continue;
                }

                var openSides = (north ? 1 : 0) + (east ? 1 : 0) + (south ? 1 : 0) + (west ? 1 : 0);
                updates.Add((position, openSides >= 2 ? TileType.Floor : TileType.Wall));
            }
        }

        foreach (var update in updates)
        {
            world.SetTile(update.Position, update.TileType);
        }
    }

    private static bool SupportsDoorway(WorldState world, Position position)
    {
        if (!world.InBounds(position))
        {
            return false;
        }

        // Keep doorway support aligned with the validator's traversability rules so
        // sanitized doors are never rejected later for mismatched semantics.
        return LevelValidator.IsTraversable(world.GetTile(position));
    }
}
