using System.Collections.Generic;

namespace Roguelike.Core;

public sealed record LevelData(
    Position PlayerSpawn,
    Position StairsDown,
    IReadOnlyList<Position> EnemySpawns,
    IReadOnlyList<Position> ItemSpawns,
    IReadOnlyList<RoomData> Rooms,
    IReadOnlyList<Position>? ChestSpawns = null,
    IReadOnlyList<EnemySpawnData>? EnemySpawnDetails = null,
    IReadOnlyList<ItemSpawnData>? ItemSpawnDetails = null,
    IReadOnlyList<ChestSpawnData>? ChestSpawnDetails = null,
    IReadOnlyList<NpcSpawnData>? NpcSpawns = null);
