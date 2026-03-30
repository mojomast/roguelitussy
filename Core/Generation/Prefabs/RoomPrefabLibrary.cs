using System.Collections.Generic;

namespace Roguelike.Core;

public static class RoomPrefabLibrary
{
    private static readonly IReadOnlyList<RoomPrefab> DefaultPrefabs = new[]
    {
        new RoomPrefab(
            "small_square",
            new[]
            {
                "#####",
                "#...#",
                "#...#",
                "#...#",
                "#####",
            }),
        new RoomPrefab(
            "long_hall",
            new[]
            {
                "#######",
                "#.....#",
                "#######",
            }),
        new RoomPrefab(
            "wide_hall",
            new[]
            {
                "#####",
                "#...#",
                "#...#",
                "#...#",
                "#...#",
                "#...#",
                "#####",
            }),
        new RoomPrefab(
            "octagon",
            new[]
            {
                "#########",
                "###...###",
                "##.....##",
                "#.......#",
                "##.....##",
                "###...###",
                "#########",
            }),
        new RoomPrefab(
            "pillars",
            new[]
            {
                "#######",
                "#.....#",
                "#.#.#.#",
                "#.....#",
                "#.#.#.#",
                "#.....#",
                "#######",
            }),
        new RoomPrefab(
            "chapel",
            new[]
            {
                "#########",
                "#.......#",
                "#.##.##.#",
                "#.......#",
                "#...+...#",
                "#########",
            }),
    };

    public static IReadOnlyList<RoomPrefab> GetDefaultPrefabs() => DefaultPrefabs;
}