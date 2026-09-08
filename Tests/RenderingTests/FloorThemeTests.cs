using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.RenderingTests;

public sealed class FloorThemeTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Rendering.FloorTheme depth boundaries and distinct palettes", Boundaries);
        registry.Add("Rendering.FloorTheme preserves geometry and terrain layering", Geometry);
        registry.Add("Rendering.FloorTheme matches covers without tinting actors or labels", Covers);
        registry.Add("Rendering.FloorTheme variants are deterministic without world mutation", Determinism);
        registry.Add("Rendering.FloorTheme rebind and door refresh use current palette", Refresh);
        registry.Add("Rendering.FloorTheme locked doors use closed art and solid neighbors", LockedDoors);
        registry.Add("Rendering.FloorTheme missing terrain uses themed fallback", Fallback);
    }

    private static void Boundaries()
    {
        foreach (var (depth, name) in new[] { (0, "Prison"), (3, "Prison"), (4, "Crypt"), (6, "Crypt"), (7, "Magma") })
        {
            Expect.Equal(name, RenderPalette.ForDepth(depth).Name, "Depth must select the authored theme band.");
        }

        Expect.Equal(RenderPalette.TileFloor, RenderPalette.ForDepth(0).FloorFallback, "Prison preserves the legacy fallback.");
        Expect.Equal(3, new[] { 0, 4, 7 }.Select(d => RenderPalette.ForDepth(d).FloorTint).Distinct().Count(), "Each theme needs a distinct floor tint.");
        Expect.Equal(3, new[] { 0, 4, 7 }.Select(d => RenderPalette.ForDepth(d).WallTint).Distinct().Count(), "Each theme needs a distinct wall tint.");
    }

    private static void Geometry()
    {
        string? baseline = null;
        foreach (var depth in new[] { 0, 3, 4, 6, 7 })
        {
            var view = View(World(depth));
            var geometry = string.Join("|", GeometryOf(view.TileArtLayerNode).Concat(GeometryOf(view.WallCoverLayerNode)));
            baseline ??= geometry;
            Expect.Equal(baseline, geometry, "Theme changes must not alter any terrain transform, region, or Z index.");
            var floor = Child<Node2D>(view.TileArtLayerNode, "Tile_3_3");
            var sprite = Child<Sprite2D>(floor, "Texture");
            Expect.Equal(new Vector2(120, 120), floor.Position, "Tile origin remains on the 40-unit grid.");
            Expect.Equal(new Vector2(20, 20), sprite.Position, "Source art remains centered.");
            Expect.Equal(new Vector2(2.5f, 2.5f), sprite.Scale, "Source art remains scaled 16 to 40.");
            Expect.Equal(RenderPalette.ForDepth(depth).FloorTint, sprite.Modulate, "Loaded floor PNGs must receive the palette.");
        }
    }

    private static IEnumerable<string> GeometryOf(Node parent)
    {
        foreach (var node in parent.GetChildren())
        {
            if (node is Node2D spatial)
            {
                yield return $"{node.Name}:{spatial.Position}:{spatial.Scale}:{spatial.ZIndex}";
            }
            if (node is Control control)
            {
                yield return $"{node.Name}:{control.Position}:{control.Size}:{control.ZIndex}";
            }
            if (node is Sprite2D { Texture: AtlasTexture atlas })
            {
                yield return $"{atlas.Region.Position}:{atlas.Region.Size}";
            }
            foreach (var value in GeometryOf(node))
            {
                yield return value;
            }
        }
    }

    private static void Covers()
    {
        foreach (var depth in new[] { 0, 4, 7 })
        {
            var world = World(depth);
            var view = View(world);
            var theme = RenderPalette.ForDepth(depth);
            var strip = Child<Sprite2D>(Child<Node2D>(view.WallCoverLayerNode, "WallCover_1_0"), "OccluderTexture");
            var wall = Child<Sprite2D>(Child<Node2D>(view.TileArtLayerNode, "Tile_1_0"), "Texture_1");
            Expect.Equal(theme.WallTint, strip.Modulate, "Textured corner strips must match wall tint.");
            Expect.Equal(wall.Modulate, strip.Modulate, "Wall and its cropped strip use identical modulation.");
            var atlas = (AtlasTexture)strip.Texture!;
            Expect.Equal(new Vector2(0, 12), atlas.Region.Position, "Keep the existing bottom strip origin.");
            Expect.Equal(new Vector2(16, 4), atlas.Region.Size, "Keep the existing four-pixel strip.");
            var fascia = Child<Node2D>(view.WallCoverLayerNode, "WallCover_3_1");
            Expect.Equal(theme.Face, Child<ColorRect>(fascia, "NorthCoverFace").Color, "Procedural fascia must match the theme.");
            Expect.Equal(theme.Trim, Child<ColorRect>(fascia, "NorthCoverTrim").Color, "Procedural trim must match the theme.");
            Expect.Equal(Colors.White, view.EntityRenderer.GetSprite(world.Player.Id)!.Modulate, "Actors remain untinted.");
            var stairs = Child<Node2D>(view.TileArtLayerNode, "Tile_4_3");
            Expect.Equal(Colors.White, Child<Label>(stairs, "Marker").Modulate, "Labels remain untinted.");
            Expect.Equal(theme.FloorTint, Child<Sprite2D>(stairs, "Texture_1").Modulate, "The ladder's baked floor must match the floor beneath it.");
            Expect.Equal(Colors.White, stairs.Modulate, "Tile containers must not propagate tints to markers.");
            Expect.Equal(Colors.White, view.Modulate, "World root must not propagate terrain tints.");
        }
    }

    private static void Determinism()
    {
        var world = World(4);
        var combat = world.CombatRandomState;
        var items = world.ItemRandomState;
        var original = Variants(world);
        var view = View(world);
        view.RenderFullMap(world);
        Expect.Equal(original, Variants(world), "Redraws must preserve visual variation.");
        Expect.Equal(combat, world.CombatRandomState, "Rendering must not consume combat RNG.");
        Expect.Equal(items, world.ItemRandomState, "Rendering must not consume item RNG.");
        Expect.False(world.IsVisible(new Position(3, 3)), "Rendering must not reveal terrain.");
        Expect.False(world.IsExplored(new Position(3, 3)), "Rendering must not explore terrain.");
        world.Depth = 5;
        Expect.False(original == Variants(world), "Individual floors within a theme need different stable wear.");
        world.Depth = 4;
        Expect.Equal(original, Variants(world), "Returning to a depth restores its wear.");
    }

    private static string Variants(WorldState world) => string.Join("|",
        Enumerable.Range(0, 49).Select(i => WorldArtCatalog.GetTileArtLayers(world, new Position(i % 7, i / 7), TileType.Floor)[0].ResourcePath));

    private static void Refresh()
    {
        var bus = new EventBus();
        var view = View(World(0), bus);
        var world = World(7);
        view.BindWorld(world);
        var position = new Position(3, 2);
        world.SetDoorOpen(position, true);
        bus.EmitTileChanged(position);
        var tile = Child<Node2D>(view.TileArtLayerNode, "Tile_3_2");
        Expect.Equal(RenderPalette.ForDepth(7).FloorTint, Child<Sprite2D>(tile, "Texture").Modulate, "Refresh uses the rebound world's theme.");
        Expect.True(Child<Sprite2D>(tile, "Texture_4").Texture!.ResourcePath.EndsWith("Door_Open.png"), "Open door art updates normally.");
        Expect.Equal("//", view.GetTileMarkerText(position), "Door refresh preserves semantic markers.");
        Expect.Equal(Colors.White, Child<Sprite2D>(tile, "Texture_4").Modulate, "Door art stays neutral.");
        view.BindWorld(World(0));
        Expect.Equal(RenderPalette.ForDepth(0).FloorTint, Child<Sprite2D>(Child<Node2D>(view.TileArtLayerNode, "Tile_3_3"), "Texture").Modulate, "Returning floors must not retain another theme.");
    }

    private static void LockedDoors()
    {
        var world = World(4);
        var position = new Position(3, 2);
        var closed = WorldArtCatalog.GetTileArtLayers(world, position, TileType.Door).Select(t => t.ResourcePath).ToArray();
        world.SetTile(position, TileType.LockedDoor);
        Expect.True(closed.SequenceEqual(WorldArtCatalog.GetTileArtLayers(world, position, TileType.LockedDoor).Select(t => t.ResourcePath)), "Locked doors reuse the exact closed-door layers.");
        Expect.True(WorldArtCatalog.GetWalkableBoundaryMask(world, new Position(3, 3), TileType.Floor).North, "Locked doors are solid boundaries.");
        Expect.False(WorldArtCatalog.GetWalkableBoundaryMask(world, position, TileType.LockedDoor).HasAny, "Locked doors are not walkable trim cells.");
        var view = View(world);
        Expect.Equal("LOCK", view.GetTileMarkerText(position), "Locked doors need an explicit non-color cue.");
        var tile = Child<Node2D>(view.TileArtLayerNode, "Tile_3_2");
        Expect.True(Child<Label>(tile, "Marker").ZIndex > Child<Sprite2D>(tile, "Texture_4").ZIndex, "Lock text must draw above opaque door art.");
        world.SetTile(new Position(3, 1), TileType.Wall);
        var lockedWall = WorldArtCatalog.GetTileArtLayers(world, new Position(3, 1), TileType.Wall).Select(t => t.ResourcePath).ToArray();
        world.SetTile(position, TileType.Door);
        Expect.True(lockedWall.SequenceEqual(WorldArtCatalog.GetTileArtLayers(world, new Position(3, 1), TileType.Wall).Select(t => t.ResourcePath)), "Locked and closed doors have identical wall-neighbor semantics.");
    }

    private static void Fallback()
    {
        var paths = Enumerable.Range(1, 7).Select(i => $"res://Assets/Tilesets/0x72/Floor_Cracks{i}.png")
            .Concat(new[] { "res://Assets/Tilesets/0x72/Wall_Mid.png", "res://Assets/Tilesets/0x72/Floor_Clean.png" }).ToArray();
        foreach (var path in paths)
        {
            GD.MissingResourcePaths.Add(path);
            Image.MissingImagePaths.Add(ProjectSettings.GlobalizePath(path));
        }
        WorldArtCatalog.ClearTextureCachesForTests();
        try
        {
            foreach (var depth in new[] { 0, 4, 7 })
            {
                var world = World(depth);
                var view = View(world);
                var wall = Child<Node2D>(view.TileArtLayerNode, "Tile_0_6");
                Expect.Equal(RenderPalette.ForDepth(depth).WallFallback, Child<ColorRect>(wall, "Fallback").Color, "Missing wall art retains theme and cell geometry.");
                var floor = Child<ColorRect>(Child<Node2D>(view.TileArtLayerNode, "Tile_3_3"), "Fallback");
                Expect.Equal(RenderPalette.ForDepth(depth).FloorFallback, floor.Color, "Missing floor art retains the floor palette.");
                Expect.Equal(new Vector2(40, 40), floor.Size, "Fallback floors preserve the cell footprint.");
            }
        }
        finally
        {
            foreach (var path in paths)
            {
                GD.MissingResourcePaths.Remove(path);
                Image.MissingImagePaths.Remove(ProjectSettings.GlobalizePath(path));
            }
            WorldArtCatalog.ClearTextureCachesForTests();
        }
    }

    private static T Child<T>(Node parent, string name) where T : Node => parent.GetChildren().OfType<T>().Single(n => n.Name == name);

    private static WorldState World(int depth)
    {
        var world = new WorldState { Depth = depth, Seed = 1337 };
        world.InitGrid(7, 7);
        for (var y = 0; y < 7; y++)
        {
            for (var x = 0; x < 7; x++)
            {
                world.SetTile(new Position(x, y), x == 0 || y == 0 || x == 6 || y == 6 ? TileType.Wall : TileType.Floor);
            }
        }
        world.SetTile(new Position(3, 2), TileType.Door);
        world.SetTile(new Position(4, 3), TileType.StairsDown);
        world.Player = new StubEntity("Player", new Position(2, 2), Faction.Player);
        world.AddEntity(world.Player);
        return world;
    }

    private static WorldView View(WorldState world, EventBus? bus = null)
    {
        var view = new WorldView();
        view.BindWorld(world);
        view.BindEventBus(bus);
        return view;
    }
}
