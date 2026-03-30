using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record LevelData(
    Position PlayerSpawn,
    Position StairsDown,
    IReadOnlyList<Position> EnemySpawns,
    IReadOnlyList<Position> ItemSpawns,
    IReadOnlyList<RoomData> Rooms
);
