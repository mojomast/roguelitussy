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
            },
            DefinedTags: new[] { "prison", "crypt", "magma", "generic" }),
        new RoomPrefab(
            "long_hall",
            new[]
            {
                "#######",
                "#.....#",
                "#######",
            },
            DefinedTags: new[] { "prison", "crypt", "magma", "generic" }),
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
            },
            DefinedTags: new[] { "prison", "crypt", "magma", "generic" }),
        new RoomPrefab(
            "cell_block",
            new[]
            {
                "########",
                "#..#...#",
                "#..#...#",
                "#......#",
                "#..#...#",
                "########",
            },
            DefinedTags: new[] { "prison", "generic" }),
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
            },
            DefinedTags: new[] { "crypt", "magma", "generic" }),
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
            },
            DefinedTags: new[] { "crypt", "magma", "generic" }),
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
            },
            DefinedTags: new[] { "crypt", "magma", "generic" }),
    };

    public static IReadOnlyList<RoomPrefab> GetDefaultPrefabs() => DefaultPrefabs;
}
