using Xunit;
using Roguelike.Core;
using Roguelike.Tests.Stubs;

namespace Roguelike.Tests;

public class WorldStateTests
{
    [Fact]
    public void InitGrid_SetsWidthAndHeight()
    {
        var world = new WorldState();
        world.InitGrid(20, 15);

        Assert.Equal(20, world.Width);
        Assert.Equal(15, world.Height);
    }

    [Fact]
    public void SetTile_And_GetTile_RoundTrips()
    {
        var world = StubWorldFactory.CreateSmallRoom();
        var pos = new Position(3, 3);

        world.SetTile(pos, TileType.Water);
        Assert.Equal(TileType.Water, world.GetTile(pos));
    }

    [Fact]
    public void GetTile_OutOfBounds_ReturnsVoid()
    {
        var world = StubWorldFactory.CreateSmallRoom();

        Assert.Equal(TileType.Void, world.GetTile(new Position(-1, 0)));
        Assert.Equal(TileType.Void, world.GetTile(new Position(100, 100)));
    }

    [Fact]
    public void AddEntity_And_GetEntity_Works()
    {
        var world = StubWorldFactory.CreateSmallRoom();
        var entity = new StubEntity
        {
            Id = EntityId.New(),
            Name = "Test",
            Position = new Position(5, 5),
            BlocksMovement = true,
        };

        world.AddEntity(entity);

        Assert.Same(entity, world.GetEntity(entity.Id));
        Assert.Same(entity, world.GetEntityAt(new Position(5, 5)));
        Assert.Contains(entity, world.Entities);
    }

    [Fact]
    public void RemoveEntity_ClearsFromAllLookups()
    {
        var (world, _, enemy) = StubWorldFactory.CreateWithEntities();
        var enemyPos = enemy.Position;

        world.RemoveEntity(enemy.Id);

        Assert.Null(world.GetEntity(enemy.Id));
        Assert.Null(world.GetEntityAt(enemyPos));
        Assert.DoesNotContain(enemy, world.Entities);
    }

    [Fact]
    public void IsWalkable_BlockedByEntity_ReturnsFalse()
    {
        var (world, player, _) = StubWorldFactory.CreateWithEntities();

        Assert.False(world.IsWalkable(player.Position));
        Assert.True(world.IsWalkable(new Position(3, 3))); // empty floor
        Assert.False(world.IsWalkable(new Position(0, 0))); // wall
    }
}
