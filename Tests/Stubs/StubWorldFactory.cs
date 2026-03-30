using Roguelike.Core;

namespace Roguelike.Tests.Stubs;

public static class StubWorldFactory
{
    public static WorldState CreateSmallRoom(int width = 10, int height = 10)
    {
        var world = new WorldState();
        world.InitGrid(width, height);
        world.Seed = 12345;
        world.TurnNumber = 1;
        world.Depth = 0;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
            world.SetTile(new Position(x, y), isBorder ? TileType.Wall : TileType.Floor);
        }

        return world;
    }

    public static (WorldState world, IEntity player, IEntity enemy) CreateWithEntities()
    {
        var world = CreateSmallRoom();

        var player = new StubEntity
        {
            Id = EntityId.New(),
            Name = "Player",
            Position = new Position(5, 5),
            Stats = new Stats
            {
                HP = 100, MaxHP = 100,
                Attack = 10, Defense = 5,
                Accuracy = 80, Evasion = 10,
                Speed = 100, ViewRadius = 8
            },
            Faction = Faction.Player,
            BlocksMovement = true,
        };

        var enemy = new StubEntity
        {
            Id = EntityId.New(),
            Name = "Test Goblin",
            Position = new Position(7, 5),
            Stats = new Stats
            {
                HP = 30, MaxHP = 30,
                Attack = 5, Defense = 2,
                Accuracy = 70, Evasion = 5,
                Speed = 80, ViewRadius = 6
            },
            Faction = Faction.Enemy,
            BlocksMovement = true,
        };

        world.AddEntity(player);
        world.AddEntity(enemy);
        world.Player = player;

        return (world, player, enemy);
    }
}
