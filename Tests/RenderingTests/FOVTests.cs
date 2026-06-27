using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;
using System.Reflection;

namespace Roguelike.Tests.RenderingTests;

public sealed class FOVTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Rendering.FOV blocks tiles behind walls", FovBlocksTilesBehindWalls);
        registry.Add("Rendering.FOV remains symmetric in open space", FovRemainsSymmetric);
        registry.Add("Rendering.WorldView renders map and fog from world state", WorldViewRendersMapAndFog);
        registry.Add("Rendering.WorldView renders textured tile art", WorldViewRendersTexturedTileArt);
        registry.Add("Rendering.WorldView scene includes runtime art layers", WorldViewSceneIncludesRuntimeArtLayers);
        registry.Add("Rendering.WorldView binding does not mutate world fog flags", WorldViewBindingDoesNotMutateWorldFogFlags);
        registry.Add("Rendering.WorldView applies the default zoomed-out camera framing", WorldViewAppliesDefaultCameraZoom);
        registry.Add("Rendering.WorldArtCatalog uses the 0x72 wall tile", WorldArtCatalogUses0x72WallArt);
        registry.Add("Rendering.WorldArtCatalog falls back when import cache is missing", WorldArtCatalogFallsBackWhenImportCacheIsMissing);
        registry.Add("Rendering.WorldArtCatalog falls back before imported loads fail", WorldArtCatalogFallsBackBeforeImportedLoadsFail);
        registry.Add("Rendering.WorldArtCatalog selects contextual wall art for exposed edges", WorldArtCatalogSelectsContextualWallArt);
        registry.Add("Rendering.WorldArtCatalog caps top-left room corners clearly", WorldArtCatalogCapsTopLeftRoomCornersClearly);
        registry.Add("Rendering.WorldArtCatalog resolves sprites for the current enemy roster", WorldArtCatalogResolvesSpritesForCurrentEnemyRoster);
        registry.Add("Rendering.WorldArtCatalog gives each current monster a distinct sprite", WorldArtCatalogUsesDistinctSpritesForCurrentEnemyRoster);
        registry.Add("Rendering.WorldView marks interactive tiles clearly", WorldViewMarksInteractiveTilesClearly);
        registry.Add("Rendering.WorldView animates movement and attacks via events", WorldViewAnimatesMovementAndAttacks);
        registry.Add("Rendering.WorldView keeps damage flash visible until timed restore", WorldViewKeepsDamageFlashVisibleUntilTimedRestore);
        registry.Add("Rendering.WorldView renders trap tiles with floor and marker", WorldViewRendersTrapTiles);
        registry.Add("Rendering.WorldArtCatalog resolves trap marker", WorldArtCatalogResolvesTrapMarker);
        registry.Add("Rendering.Minimap colors trap tiles distinctly", MinimapColorsTrapTilesDistinctly);
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
        world.SetVisible(world.Player.Position, true);
        var view = CreateWorldView(world);

        Expect.True(view.FloorLayer.TryGetCell(new Vector2I(2, 2), out var floorCell), "Floor tile should render to the floor layer.");
        Expect.Equal(new Vector2I(0, 0), floorCell.AtlasCoords, "Floor atlas coordinates should match the floor tile.");
        Expect.True(view.WallLayer.TryGetCell(new Vector2I(3, 3), out var wallCell), "Wall tile should render to the wall layer.");
        Expect.Equal(new Vector2I(1, 0), wallCell.AtlasCoords, "Wall atlas coordinates should match the wall tile.");
        Expect.Equal(FogTileState.Visible, view.GetFogState(world.Player.Position), "Player tile should be visible after initial render.");
        Expect.Equal(WorldView.ToCanvasPosition(world.Player.Position), view.Camera.Position, "Camera should center on the player after rendering.");
    }

    private static void WorldViewRendersTexturedTileArt()
    {
        var world = CreateRoomWorld();
        var view = CreateWorldView(world);

        Expect.True(view.TileArtLayerNode.GetChildren().Count > 0,
            "WorldView should populate the runtime TileArtLayer instead of relying on hidden legacy TileMap layers.");
        Expect.True(view.TileArtLayerNode.GetChildren()
            .OfType<Node2D>()
            .Any(tile => tile.GetChildren().OfType<Sprite2D>().Any(sprite => sprite.Texture is Texture2D texture && !string.IsNullOrWhiteSpace(texture.ResourcePath))),
            "World tile art should contain Sprite2D nodes with imported texture resources.");
    }

    private static void WorldViewSceneIncludesRuntimeArtLayers()
    {
        var scenePath = System.IO.Path.Combine("Scenes", "World", "WorldView.tscn");
        var scene = System.IO.File.ReadAllText(scenePath);

        Expect.True(scene.Contains("name=\"TileArtLayer\"", System.StringComparison.Ordinal),
            "WorldView scene should declare TileArtLayer so textured tiles render consistently in editor-spawned scenes.");
        Expect.True(scene.Contains("name=\"WallCoverLayer\"", System.StringComparison.Ordinal),
            "WorldView scene should declare WallCoverLayer so wall occluders are not dependent on runtime fallback nodes.");
    }

    private static void WorldViewBindingDoesNotMutateWorldFogFlags()
    {
        var world = CreateRoomWorld();
        var exploredOnly = new Position(1, 1);
        var visible = world.Player.Position;
        var hidden = new Position(5, 5);

        world.SetVisible(exploredOnly, true);
        world.ClearVisibility();
        world.SetVisible(visible, true);

        var exploredOnlyWasVisible = world.IsVisible(exploredOnly);
        var exploredOnlyWasExplored = world.IsExplored(exploredOnly);
        var visibleWasVisible = world.IsVisible(visible);
        var visibleWasExplored = world.IsExplored(visible);
        var hiddenWasVisible = world.IsVisible(hidden);
        var hiddenWasExplored = world.IsExplored(hidden);

        var view = CreateWorldView(world);
        view.RenderFullMap(world);

        Expect.Equal(exploredOnlyWasVisible, world.IsVisible(exploredOnly),
            "Binding/rendering WorldView must not recalculate authoritative visible flags.");
        Expect.Equal(exploredOnlyWasExplored, world.IsExplored(exploredOnly),
            "Binding/rendering WorldView must preserve authoritative explored flags.");
        Expect.Equal(visibleWasVisible, world.IsVisible(visible),
            "WorldView should mirror an already-visible tile without rewriting world state.");
        Expect.Equal(visibleWasExplored, world.IsExplored(visible),
            "WorldView should leave explored state for visible tiles owned by WorldState.");
        Expect.Equal(hiddenWasVisible, world.IsVisible(hidden),
            "WorldView must not reveal hidden tiles during render.");
        Expect.Equal(hiddenWasExplored, world.IsExplored(hidden),
            "WorldView must not mark hidden tiles explored during render.");

        Expect.Equal(FogTileState.Explored, view.GetFogState(exploredOnly),
            "WorldView should still render explored-only fog from WorldState.");
        Expect.Equal(FogTileState.Visible, view.GetFogState(visible),
            "WorldView should still render visible fog from WorldState.");
        Expect.Equal(FogTileState.Hidden, view.GetFogState(hidden),
            "WorldView should still render hidden fog from WorldState.");
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

    private static void WorldViewKeepsDamageFlashVisibleUntilTimedRestore()
    {
        var bus = new EventBus();
        var world = CreateRoomWorld();
        var enemy = new StubEntity("Goblin", new Position(5, 2), Faction.Enemy);
        world.AddEntity(enemy);

        var view = CreateWorldView(world, bus);
        var defenderSprite = view.EntityRenderer.GetSprite(enemy.Id)!;

        bus.EmitDamageDealt(new DamageResult(world.Player.Id, enemy.Id, 5, 5, DamageType.Physical, false, false, false));

        Expect.NotEqual(Colors.White, defenderSprite.Modulate, "Damage flash should tint the defender immediately after the damage event.");
        Expect.True(view.Animations.IsDamageFlashing(enemy.Id), "Damage flash state should stay active after the initial event.");
        Expect.Equal(1, view.Animations.ActiveFlashCount, "Exactly one defender flash should be active.");

        view.EntityRenderer.UpsertEntity(enemy);
        Expect.NotEqual(Colors.White, defenderSprite.Modulate, "Refreshing an entity during an active flash should not clobber the tint.");

        view._Process(0.05d);
        Expect.NotEqual(Colors.White, defenderSprite.Modulate, "Damage flash should remain visible during the short flash window.");

        view._Process(0.10d);
        Expect.Equal(Colors.White, defenderSprite.Modulate, "Damage flash should restore the defender exactly to white after it completes.");
        Expect.False(view.Animations.IsDamageFlashing(enemy.Id), "Damage flash state should be cleared after completion.");
        Expect.Equal(0, view.Animations.ActiveFlashCount, "No damage flashes should remain after completion.");
    }

    private static void WorldViewRendersTrapTiles()
    {
        var world = CreateRoomWorld();
        var trapPosition = new Position(3, 3);
        world.SetTile(trapPosition, TileType.Trap);
        var view = CreateWorldView(world);

        Expect.Equal("^", view.GetTileMarkerText(trapPosition), "Trap tiles should expose a spike marker.");
        Expect.True(view.FloorLayer.TryGetCell(new Vector2I(trapPosition.X, trapPosition.Y), out var floorCell), "Trap tile should render a floor cell.");
        Expect.Equal(new Vector2I(0, 0), floorCell.AtlasCoords, "Trap tile should use the floor atlas coordinate.");
        Expect.False(view.WallLayer.TryGetCell(new Vector2I(trapPosition.X, trapPosition.Y), out _), "Trap tile should not render to the wall layer.");
    }

    private static void WorldArtCatalogResolvesTrapMarker()
    {
        var marker = WorldArtCatalog.GetTileMarker(TileType.Trap, false);
        Expect.Equal("^", marker ?? string.Empty, "Trap tiles should resolve a caret marker.");
    }

    private static void MinimapColorsTrapTilesDistinctly()
    {
        var world = CreateRoomWorld();
        var trapPosition = new Position(3, 3);
        world.SetTile(trapPosition, TileType.Trap);
        world.SetVisible(trapPosition, true);

        var minimap = new Minimap();
        var gameManager = new GameManager();
        gameManager.LoadToolWorld(world);
        var bus = new EventBus();
        minimap.Bind(gameManager, bus);

        var resolveColor = typeof(Minimap).GetMethod("ResolveTileColor", BindingFlags.NonPublic | BindingFlags.Static);
        if (resolveColor is null)
        {
            throw new InvalidOperationException("Minimap.ResolveTileColor method not found.");
        }

        var trapColor = (Color)resolveColor.Invoke(null, new object[] { world, trapPosition })!;
        var floorPosition = new Position(2, 2);
        world.SetVisible(floorPosition, true);
        var floorColor = (Color)resolveColor.Invoke(null, new object[] { world, floorPosition })!;

        Expect.NotEqual(floorColor, trapColor, "Trap tiles should render a color distinct from floor tiles on the minimap.");
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

    private static void WorldArtCatalogFallsBackWhenImportCacheIsMissing()
    {
        const string path = "res://Assets/Tilesets/0x72/Wall_Mid.png";
        WorldArtCatalog.ClearTextureCachesForTests();
        GD.MissingResourcePaths.Add(path);
        try
        {
            var wallTexture = WorldArtCatalog.GetTileTexture(TileType.Wall, false);

            Expect.True(wallTexture is ImageTexture,
                "World art should fall back to source image loading when the generated Godot import cache is missing on first run.");
            Expect.Equal(path, wallTexture!.ResourcePath,
                "Fallback world art should preserve the source resource path for rendering diagnostics.");
        }
        finally
        {
            GD.MissingResourcePaths.Remove(path);
            WorldArtCatalog.ClearTextureCachesForTests();
        }
    }

    private static void WorldArtCatalogFallsBackBeforeImportedLoadsFail()
    {
        const string path = "res://Assets/Tilesets/0x72/Wall_Mid.png";
        var importMetadataPath = System.IO.Path.Combine("Assets", "Tilesets", "0x72", "Wall_Mid.png.import");
        var importMetadata = System.IO.File.ReadAllText(importMetadataPath);
        var importedPath = importMetadata.Split('\n')
            .First(line => line.StartsWith("path=\"", System.StringComparison.Ordinal))[6..]
            .TrimEnd('"', '\r');
        var importedFullPath = ProjectSettings.GlobalizePath(importedPath);
        var backupPath = importedFullPath + ".testbak";

        WorldArtCatalog.ClearTextureCachesForTests();
        GD.MissingResourcePaths.Add(path);
        if (System.IO.File.Exists(importedFullPath))
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backupPath)!);
            System.IO.File.Move(importedFullPath, backupPath, overwrite: true);
        }

        try
        {
            var wallTexture = WorldArtCatalog.GetTileTexture(TileType.Wall, false);

            Expect.True(wallTexture is ImageTexture,
                "When .import metadata points at a missing .ctex, world art should bypass imported resource loading and use the source image fallback.");
            Expect.Equal(path, wallTexture!.ResourcePath,
                "Missing .ctex fallback should preserve the source resource path.");
        }
        finally
        {
            GD.MissingResourcePaths.Remove(path);
            if (System.IO.File.Exists(backupPath))
            {
                System.IO.File.Move(backupPath, importedFullPath, overwrite: true);
            }

            WorldArtCatalog.ClearTextureCachesForTests();
        }
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

    private static void WorldViewRefreshesEntityStatusOverlaysOnEvents()
    {
        var content = new StubContentDatabase();
        var bus = new EventBus();
        var world = CreateRoomWorld();
        world.ContentDatabase = content;
        var enemy = new StubEntity("Goblin", new Position(3, 2), Faction.Enemy);
        enemy.SetComponent(new StatusEffectsComponent());
        world.AddEntity(enemy);
        var view = CreateWorldView(world, bus);

        var spriteRoot = view.EntityRenderer.GetSprite(enemy.Id)!;
        var overlayBefore = FindChild<Node2D>(spriteRoot, "StatusOverlay");
        Expect.True(overlayBefore?.GetChildren().Count == 0, "Enemy sprite should start with no status overlays.");

        StatusEffectProcessor.ApplyEffect(enemy, StatusEffectType.Poisoned, 3, 1);
        bus.EmitStatusEffectApplied(enemy.Id, StatusEffectProcessor.GetEffect(enemy, StatusEffectType.Poisoned)!);

        var overlayAfter = FindChild<Node2D>(spriteRoot, "StatusOverlay");
        Expect.NotNull(FindChild<Sprite2D>(overlayAfter!, "poisoned"), "WorldView should refresh status overlays when StatusEffectApplied is emitted.");
    }

    private static T? FindChild<T>(Node parent, string name) where T : Node
    {
        for (var i = 0; i < parent.Children.Count; i++)
        {
            if (parent.Children[i] is T typed && typed.Name == name)
            {
                return typed;
            }
        }

        return null;
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
