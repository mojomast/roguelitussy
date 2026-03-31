using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.RenderingTests;

public sealed class FOVTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Rendering.FOV blocks tiles behind walls", FovBlocksTilesBehindWalls);
        registry.Add("Rendering.FOV remains symmetric in open space", FovRemainsSymmetric);
        registry.Add("Rendering.WorldView renders map and fog from world state", WorldViewRendersMapAndFog);
        registry.Add("Rendering.WorldView applies the default zoomed-out camera framing", WorldViewAppliesDefaultCameraZoom);
        registry.Add("Rendering.WorldArtCatalog uses the 0x72 wall tile", WorldArtCatalogUses0x72WallArt);
        registry.Add("Rendering.WorldView marks interactive tiles clearly", WorldViewMarksInteractiveTilesClearly);
        registry.Add("Rendering.WorldView animates movement and attacks via events", WorldViewAnimatesMovementAndAttacks);
    }

    private static void FovBlocksTilesBehindWalls()
    {
        var world = CreateRoomWorld();
        world.SetTile(new Position(4, 2), TileType.Wall);

        var fov = new FOVCalculator();
        var visible = fov.ComputeVisible(world.Player.Position, 6, pos => !world.InBounds(pos) || world.BlocksSight(pos));

        Expect.True(visible.Contains(new Position(4, 2)), "The blocking wall tile itself should remain visible.");
        Expect.False(visible.Contains(new Position(4, 1)), "Tiles hidden behind the wall should not be visible.");
    }

    private static void FovRemainsSymmetric()
    {
        var world = CreateRoomWorld(width: 9, height: 9, playerPosition: new Position(4, 4));
        var fov = new FOVCalculator();
        var origin = world.Player.Position;
        var visibleFromOrigin = fov.ComputeVisible(origin, 4, pos => !world.InBounds(pos) || world.BlocksSight(pos));

        var target = new Position(6, 3);
        Expect.True(visibleFromOrigin.Contains(target), "Open-room target should be visible from the origin.");

        var visibleFromTarget = fov.ComputeVisible(target, 4, pos => !world.InBounds(pos) || world.BlocksSight(pos));
        Expect.True(visibleFromTarget.Contains(origin), "Visibility should remain symmetric in open space.");
    }

    private static void WorldViewRendersMapAndFog()
    {
        var world = CreateRoomWorld();
        world.SetTile(new Position(3, 3), TileType.Wall);
        var view = CreateWorldView(world);

        Expect.True(view.FloorLayer.TryGetCell(new Vector2I(2, 2), out var floorCell), "Floor tile should render to the floor layer.");
        Expect.Equal(new Vector2I(0, 0), floorCell.AtlasCoords, "Floor atlas coordinates should match the floor tile.");
        Expect.True(view.WallLayer.TryGetCell(new Vector2I(3, 3), out var wallCell), "Wall tile should render to the wall layer.");
        Expect.Equal(new Vector2I(1, 0), wallCell.AtlasCoords, "Wall atlas coordinates should match the wall tile.");
        Expect.Equal(FogTileState.Visible, view.GetFogState(world.Player.Position), "Player tile should be visible after initial render.");
        Expect.Equal(WorldView.ToCanvasPosition(world.Player.Position), view.Camera.Position, "Camera should center on the player after rendering.");
    }

    private static void WorldViewAnimatesMovementAndAttacks()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var enemy = new StubEntity("Goblin", new Position(5, 2), Faction.Enemy);
        world.AddEntity(enemy);

        var view = CreateWorldView(world, bus);
        view.Animations.ClearHistory();

        var startingPosition = view.EntityRenderer.GetSprite(world.Player.Id)!.Position;
        world.MoveEntity(world.Player.Id, new Position(3, 2));
        bus.EmitTurnCompleted();

        Expect.Equal(1, view.Animations.History.Count, "Movement diffing should record one move animation.");
        Expect.Equal(AnimationType.Move, view.Animations.History[0].Type, "First animation should be a move.");
        Expect.Equal(startingPosition, view.EntityRenderer.GetSprite(world.Player.Id)!.Position, "Move animation should start from the previous tile instead of snapping immediately.");

        view._Process(0.06d);
        var midMovePosition = view.EntityRenderer.GetSprite(world.Player.Id)!.Position;
        Expect.True(midMovePosition.X > startingPosition.X, "Move animation should interpolate forward during the frame updates.");
        Expect.True(midMovePosition.X < WorldView.ToCanvasPosition(new Position(3, 2)).X, "Move animation should still be in-flight halfway through the smoothing window.");

        view._Process(0.10d);
        Expect.Equal(WorldView.ToCanvasPosition(new Position(3, 2)), view.EntityRenderer.GetSprite(world.Player.Id)!.Position, "Player sprite should land on the moved tile.");

        view.Animations.ClearHistory();
        bus.EmitDamageDealt(new DamageResult(world.Player.Id, enemy.Id, 5, 5, DamageType.Physical, false, false, false));

        Expect.Equal(2, view.Animations.History.Count, "Damage event should queue attack and damage animations.");
        Expect.Equal(AnimationType.Attack, view.Animations.History[0].Type, "Attack animation should play first.");
        Expect.Equal(AnimationType.Damage, view.Animations.History[1].Type, "Damage flash should play second.");
    }

    private static void WorldViewAppliesDefaultCameraZoom()
    {
        var world = CreateRoomWorld();
        var view = CreateWorldView(world);

        Expect.Equal(new Vector2(CameraController.DefaultZoom, CameraController.DefaultZoom), view.Camera.Zoom,
            "World view should apply the default zoomed-out camera framing.");
    }

    private static void WorldArtCatalogUses0x72WallArt()
    {
        var wallTexture = WorldArtCatalog.GetTileTexture(TileType.Wall, false);

        Expect.NotNull(wallTexture, "Wall art should resolve to a texture.");
        Expect.Equal("res://Assets/Tilesets/0x72/Wall_Mid.png", wallTexture!.ResourcePath,
            "Wall art should come from the imported 0x72 tileset instead of the regressed Kenney crop.");
    }

    private static void WorldViewMarksInteractiveTilesClearly()
    {
        var world = CreateRoomWorld();
        world.SetTile(new Position(3, 2), TileType.Door);
        world.SetTile(new Position(4, 2), TileType.StairsDown);
        world.SetTile(new Position(2, 3), TileType.StairsUp);

        var view = CreateWorldView(world);

        Expect.Equal("[]", view.GetTileMarkerText(new Position(3, 2)), "Closed doors should expose an explicit door marker.");
        Expect.Equal("DN", view.GetTileMarkerText(new Position(4, 2)), "Down stairs should expose an explicit down marker.");
        Expect.Equal("UP", view.GetTileMarkerText(new Position(2, 3)), "Up stairs should expose an explicit up marker.");
    }

    private static WorldState CreateRoomWorld(int width = 7, int height = 7, Position? playerPosition = null)
    {
        var world = new WorldState();
        world.InitGrid(width, height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var tile = x == 0 || y == 0 || x == width - 1 || y == height - 1
                    ? TileType.Wall
                    : TileType.Floor;
                world.SetTile(new Position(x, y), tile);
            }
        }

        var player = new StubEntity(
            "Player",
            playerPosition ?? new Position(2, 2),
            Faction.Player,
            stats: new Stats
            {
                HP = 20,
                MaxHP = 20,
                Attack = 5,
                Defense = 2,
                Accuracy = 0,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 8,
            });

        world.Player = player;
        world.AddEntity(player);
        return world;
    }

    private static WorldView CreateWorldView(WorldState world, EventBus? bus = null)
    {
        var view = new WorldView();
        view.BindWorld(world);
        view.BindEventBus(bus);
        return view;
    }
}