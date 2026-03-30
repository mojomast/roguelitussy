using System.Collections.Generic;

namespace Roguelike.Core;

public interface IWorldState
{
    int Width { get; }

    int Height { get; }

    TileType GetTile(Position pos);

    bool InBounds(Position pos);

    bool IsWalkable(Position pos);

    bool BlocksSight(Position pos);

    IEntity? GetEntity(EntityId id);

    IEntity? GetEntityAt(Position pos);

    IReadOnlyList<IEntity> Entities { get; }

    IEntity Player { get; }

    int TurnNumber { get; }

    int Depth { get; }

    bool IsVisible(Position pos);

    bool IsExplored(Position pos);
}
