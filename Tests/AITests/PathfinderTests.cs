using Xunit;
using Roguelike.Core;
using Roguelike.Core.AI;
using Roguelike.Tests.Stubs;

namespace Roguelike.Tests;

public class PathfinderTests
{
    [Fact]
    public void FindPath_SamePosition_ReturnsEmpty()
    {
        var world = StubWorldFactory.CreateSmallRoom();
        var pf = new Pathfinder();

        var path = pf.FindPath(new Position(5, 5), new Position(5, 5), world);

        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_StraightLine_ReturnsCorrectLength()
    {
        var world = StubWorldFactory.CreateSmallRoom();
        var pf = new Pathfinder();

        var start = new Position(1, 1);
        var goal = new Position(5, 1);
        var path = pf.FindPath(start, goal, world);

        // Manhattan distance = 4, path should be 4 steps (start excluded)
        Assert.Equal(4, path.Count);
        Assert.Equal(goal, path[^1]);
    }

    [Fact]
    public void FindPath_AroundWall_FindsDetour()
    {
        var world = StubWorldFactory.CreateSmallRoom(12, 12);
        // Place a wall barrier from (5,2) to (5,8), leaving gaps at top/bottom
        for (int y = 2; y <= 8; y++)
            world.SetTile(new Position(5, y), TileType.Wall);

        var pf = new Pathfinder();
        var path = pf.FindPath(new Position(3, 5), new Position(7, 5), world);

        Assert.NotEmpty(path);
        Assert.Equal(new Position(7, 5), path[^1]);
        // Path must go around the wall, so longer than manhattan distance of 4
        Assert.True(path.Count > 4);
    }

    [Fact]
    public void FindPath_NoPath_ReturnsEmpty()
    {
        var world = StubWorldFactory.CreateSmallRoom();
        // Completely wall off position (1,1)
        world.SetTile(new Position(2, 1), TileType.Wall);
        world.SetTile(new Position(1, 2), TileType.Wall);

        var pf = new Pathfinder();
        var path = pf.FindPath(new Position(1, 1), new Position(8, 8), world);

        Assert.Empty(path);
    }

    [Fact]
    public void GetReachable_ReturnsCorrectRange()
    {
        var world = StubWorldFactory.CreateSmallRoom();
        var pf = new Pathfinder();

        var reachable = pf.GetReachable(new Position(5, 5), 2, world);

        Assert.Contains(new Position(5, 5), reachable.Keys);
        Assert.Equal(0, reachable[new Position(5, 5)]);
        Assert.Contains(new Position(5, 4), reachable.Keys);
        Assert.Equal(1, reachable[new Position(5, 4)]);
        // Manhattan distance 2 tiles should be reachable
        Assert.Contains(new Position(5, 3), reachable.Keys);
        Assert.Equal(2, reachable[new Position(5, 3)]);
        // Distance 3 should NOT be reachable
        Assert.DoesNotContain(new Position(5, 2), reachable.Keys);
    }

    [Fact]
    public void HasPath_ReturnsTrueForReachableGoal()
    {
        var world = StubWorldFactory.CreateSmallRoom();
        var pf = new Pathfinder();

        Assert.True(pf.HasPath(new Position(1, 1), new Position(8, 8), world));
        Assert.False(pf.HasPath(new Position(1, 1), new Position(8, 8), world, maxLength: 1));
    }
}
