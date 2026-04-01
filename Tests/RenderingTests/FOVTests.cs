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
        registry.Add("Rendering.WorldArtCatalog selects contextual wall art for exposed edges", WorldArtCatalogSelectsContextualWallArt);
        registry.Add("Rendering.WorldArtCatalog caps top-left room corners clearly", WorldArtCatalogCapsTopLeftRoomCornersClearly);
        registry.Add("Rendering.WorldArtCatalog resolves sprites for the current enemy roster", WorldArtCatalogResolvesSpritesForCurrentEnemyRoster);
        registry.Add("Rendering.WorldArtCatalog gives each current monster a distinct sprite", WorldArtCatalogUsesDistinctSpritesForCurrentEnemyRoster);
        registry.Add("Rendering.WorldView marks interactive tiles clearly", WorldViewMarksInteractiveTilesClearly);
        registry.Add("Rendering.WorldView animates movement and attacks via events", WorldViewAnimatesMovementAndAttacks);
        registry.Add("Rendering.WorldView rerenders dirty tiles without rebuilding the map", WorldViewRerendersDirtyTiles);
        registry.Add("Rendering.WorldView snaps severely desynced actors back to their logical tiles", WorldViewSnapsSeverelyDesyncedActorsBackToTheirLogicalTiles);
        registry.Add("Rendering.WorldView adds wall covers above entities along north edges", WorldViewAddsNorthWallCoverLayer);
        registry.Add("Rendering.WorldView keeps textured corner covers above same-row actors", WorldViewKeepsTexturedCornerCoversAboveSameRowActors);
        registry.Add("Rendering.WorldView keeps entity layer above trim accents", WorldViewKeepsEntityLayerAboveTrimAccents);
        registry.Add("Rendering.WorldView clears damage popups after their lifetime", WorldViewExpiresDamagePopups);
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

    private static void WorldViewRerendersDirtyTiles()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var view = CreateWorldView(world, bus);

        world.SetTile(new Position(3, 3), TileType.Wall);
        bus.EmitTileChanged(new Position(3, 3));

        Expect.True(view.WallLayer.TryGetCell(new Vector2I(3, 3), out var wallCell), "Dirty tile refresh should update the changed wall cell.");
        Expect.Equal(new Vector2I(1, 0), wallCell.AtlasCoords, "Dirty tile refresh should reuse the correct wall atlas cell.");
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

    private static void WorldArtCatalogSelectsContextualWallArt()
    {
        var world = CreateRoomWorld();

        var topWallLayers = WorldArtCatalog.GetTileArtLayers(world, new Position(3, 0), TileType.Wall);

        Expect.True(topWallLayers.Count >= 2, "Exposed wall tiles should render as layered floor-plus-wall art.");
        Expect.Equal("res://Assets/Tilesets/0x72/Wall_Top_Mid.png", topWallLayers[^1].ResourcePath,
            "Top-edge walls should use the dedicated top wall sprite instead of the generic mid block.");
    }

    private static void WorldArtCatalogCapsTopLeftRoomCornersClearly()
    {
        var world = CreateRoomWorld();

        var topLeftTopWallLayers = WorldArtCatalog.GetTileArtLayers(world, new Position(1, 0), TileType.Wall);
        var topLeftSideWallLayers = WorldArtCatalog.GetTileArtLayers(world, new Position(0, 1), TileType.Wall);

        Expect.Equal("res://Assets/Tilesets/0x72/Wall_Corner_Top_Left.png", topLeftTopWallLayers[^1].ResourcePath,
            "Top walls beside the left room boundary should use an explicit corner cap so the tile does not read like an opening.");
        Expect.Equal("res://Assets/Tilesets/0x72/Wall_Side_Top_Left.png", topLeftSideWallLayers[^1].ResourcePath,
            "Left walls beneath the top room boundary should use the dedicated side-top cap so the corner reads as sealed instead of jagged.");
    }

    private static void WorldArtCatalogResolvesSpritesForCurrentEnemyRoster()
    {
        var enemyNames = new[]
        {
            "Giant Rat",
            "Skeleton Warrior",
            "Goblin Archer",
            "Orc Brute",
            "Spectral Wraith",
            "Acid Slime",
            "Cave Spider",
            "Skeleton Knight",
            "Goblin Shaman",
            "Dark Mage",
            "Shadow Stalker",
            "Bone Lord",
            "Flame Elemental",
        };

        foreach (var enemyName in enemyNames)
        {
            var enemy = new StubEntity(enemyName, new Position(1, 1), Faction.Enemy);
            Expect.NotNull(WorldArtCatalog.GetEntityTexture(enemy),
                $"Enemy '{enemyName}' should resolve to an imported texture instead of falling back to a placeholder square.");
        }
    }

    private static void WorldArtCatalogUsesDistinctSpritesForCurrentEnemyRoster()
    {
        var enemyNames = new[]
        {
            "Giant Rat",
            "Skeleton Warrior",
            "Goblin Archer",
            "Orc Brute",
            "Spectral Wraith",
            "Acid Slime",
            "Cave Spider",
            "Skeleton Knight",
            "Goblin Shaman",
            "Dark Mage",
            "Shadow Stalker",
            "Bone Lord",
            "Flame Elemental",
        };

        var resourcePaths = enemyNames
            .Select(enemyName => WorldArtCatalog.GetEntityTexture(new StubEntity(enemyName, new Position(1, 1), Faction.Enemy))?.ResourcePath ?? string.Empty)
            .ToArray();

        Expect.Equal(enemyNames.Length, resourcePaths.Distinct(System.StringComparer.Ordinal).Count(),
            "Each monster in the current roster should map to a distinct imported sprite path.");
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

    private static void WorldViewSnapsSeverelyDesyncedActorsBackToTheirLogicalTiles()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var view = CreateWorldView(world, bus);

        world.MoveEntity(world.Player.Id, new Position(5, 2));
        bus.EmitTurnCompleted();

        Expect.Equal(WorldView.ToCanvasPosition(new Position(5, 2)), view.EntityRenderer.GetSprite(world.Player.Id)!.Position,
            "If the logical player position outruns the active move animation by multiple tiles, the renderer should resync the sprite instead of leaving it stranded behind the camera.");
    }

    private static void WorldViewAddsNorthWallCoverLayer()
    {
        var world = CreateRoomWorld();
        var view = CreateWorldView(world);

        Expect.True(view.WallCoverLayerNode.GetChildren().Count > 0,
            "Walkable tiles beneath north-facing walls should add a dedicated cover layer so actors do not draw through the wall lip.");
        Expect.True(view.WallCoverLayerNode.GetChildren().OfType<Node2D>().Any(node => node.GetChildren().OfType<Sprite2D>().Any()),
            "Corner front walls should still carry textured cap art above entities so the wall shapes read correctly.");

        var straightNorthWallCover = view.WallCoverLayerNode.GetChildren().OfType<Node2D>().FirstOrDefault(node => node.Name == "WallCover_3_0");
        Expect.True(straightNorthWallCover is null,
            "Straight north walls should stop duplicating the transparent top-cap texture above actors, because that texture is only a thin fascia line.");

        var straightFloorCover = view.WallCoverLayerNode.GetChildren().OfType<Node2D>().FirstOrDefault(node => node.Name == "WallCover_3_1");
        Expect.NotNull(straightFloorCover,
            "The walkable tile beneath a straight north wall should restore the solid front fascia that hides the actor body behind the wall face.");
        Expect.True(straightFloorCover!.GetChildren().Any(child => child.Name == "NorthCoverFace"),
            "Straight north wall cover should use the opaque front face on the floor tile below the wall.");
        Expect.True(straightFloorCover.GetChildren().Any(child => child.Name == "NorthCoverShadow"),
            "Straight north wall cover should keep the shadow under that fascia so the wall still reads with depth.");
    }

    private static void WorldViewExpiresDamagePopups()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var enemy = new StubEntity("Goblin", new Position(3, 2), Faction.Enemy);
        world.AddEntity(enemy);
        var view = CreateWorldView(world, bus);

        bus.EmitDamageDealt(new DamageResult(world.Player.Id, enemy.Id, 4, 4, DamageType.Physical, false, false, false));
        Expect.True(view.EntityLayerNode.GetChildren().OfType<DamagePopup>().Any(),
            "Damage events should create a transient popup.");

        view._Process(1.0d);

        Expect.False(view.EntityLayerNode.GetChildren().OfType<DamagePopup>().Any(),
            "Damage popups should expire and free themselves after their lifetime elapses.");
    }

    private static void WorldViewKeepsTexturedCornerCoversAboveSameRowActors()
    {
        var world = CreateRoomWorld(playerPosition: new Position(1, 1));
        var view = CreateWorldView(world);

        var playerSprite = view.EntityRenderer.GetSprite(world.Player.Id)!;
        var cornerCover = view.WallCoverLayerNode.GetChildren()
            .OfType<Node2D>()
            .FirstOrDefault(node => node.Name == "WallCover_1_0");

        Expect.NotNull(cornerCover, "A north-wall corner should create a dedicated cover node above the tile below it.");
        var cornerStrip = cornerCover!.GetChildren().OfType<Sprite2D>().FirstOrDefault();
        Expect.NotNull(cornerStrip,
            "Corner covers should keep their textured front strip so the wall cap reads correctly.");
        Expect.Equal(4f, ((AtlasTexture)cornerStrip!.Texture!).Region.Size.Y,
            "Corner covers should preserve the full-height textured strip that was already reading correctly.");
        Expect.True(cornerCover.ZIndex > playerSprite.ZIndex,
            "Textured corner covers should sort above actors on the same row so corner wall caps occlude correctly.");
    }

    private static void WorldViewKeepsEntityLayerAboveTrimAccents()
    {
        var world = CreateRoomWorld(playerPosition: new Position(3, 1));
        var view = CreateWorldView(world);

        Expect.True(view.EntityLayerNode.ZIndex > 8,
            "EntityLayer should sort above the tile trim accents so floor and wall boundary lines do not slice through actors.");
        Expect.True(view.WallCoverLayerNode.ZIndex > view.EntityLayerNode.ZIndex,
            "WallCoverLayer should still sort above EntityLayer so dedicated wall occluders keep hiding actors behind wall caps.");
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