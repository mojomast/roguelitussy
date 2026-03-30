using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.AITests;

public sealed class PathfinderTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("AI.Pathfinder routes through an opening around obstacles", FindsPathAroundObstacles);
        registry.Add("AI.Pathfinder reports unreachable goals", ReturnsEmptyPathForBlockedGoal);
        registry.Add("AI.Pathfinder reachable map respects walls and range", ReachableMapRespectsConstraints);
        registry.Add("AI.Pathfinder honors max path length", MaxLengthCapsSearchResults);
    }

    private static void FindsPathAroundObstacles()
    {
        var world = CreateWorld(7, 7);
        var pathfinder = new Pathfinder();

        for (var x = 0; x < world.Width; x++)
        {
            if (x == 3)
            {
                continue;
            }

            world.SetTile(new Position(x, 3), TileType.Wall);
        }

        var path = pathfinder.FindPath(new Position(1, 1), new Position(5, 5), world, 20);

        Expect.True(path.Count > 0, "A traversable route should produce a path");
        Expect.True(path.Contains(new Position(3, 3)), "The path should route through the only opening in the wall");
        Expect.Equal(new Position(5, 5), path[^1], "The path should terminate at the goal");
    }

    private static void ReturnsEmptyPathForBlockedGoal()
    {
        var world = CreateWorld(7, 7);
        var pathfinder = new Pathfinder();
        var goal = new Position(3, 3);

        foreach (var delta in Position.AllDirections)
        {
            world.SetTile(goal + delta, TileType.Wall);
        }

        var path = pathfinder.FindPath(new Position(1, 1), goal, world, 20);

        Expect.Equal(0, path.Count, "A sealed goal should be unreachable");
        Expect.False(pathfinder.HasPath(new Position(1, 1), goal, world, 20), "HasPath should agree with an unreachable result");
    }

    private static void ReachableMapRespectsConstraints()
    {
        var world = CreateWorld(6, 6);
        var pathfinder = new Pathfinder();

        world.SetTile(new Position(2, 1), TileType.Wall);
        world.SetTile(new Position(2, 2), TileType.Wall);
        world.SetTile(new Position(2, 3), TileType.Wall);

        var reachable = pathfinder.GetReachable(new Position(1, 2), 2, world);

        Expect.True(reachable.ContainsKey(new Position(1, 2)), "Reachable map should include the origin");
        Expect.False(reachable.ContainsKey(new Position(2, 2)), "Reachable map should exclude blocking walls");
        Expect.False(reachable.ContainsKey(new Position(4, 2)), "Tiles beyond the allowed range should be excluded");
    }

    private static void MaxLengthCapsSearchResults()
    {
        var world = CreateWorld(8, 3);
        var pathfinder = new Pathfinder();
        var path = pathfinder.FindPath(new Position(0, 1), new Position(7, 1), world, 3);

        Expect.Equal(0, path.Count, "Paths longer than the cap should be rejected");
    }

    private static WorldState CreateWorld(int width, int height)
    {
        var world = new WorldState();
        world.InitGrid(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(0, 0), Faction.Player);
        world.Player = player;
        return world;
    }
}