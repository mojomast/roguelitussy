using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.RenderingTests;

public sealed class ItemVisualTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("Rendering.ItemVisualCatalog loads every authored item path", LoadsEveryAuthoredItemPath);
        registry.Add("Rendering.ItemVisualCatalog uses source images when imports are absent", UsesSourceImageWhenImportIsAbsent);
        registry.Add("Rendering.ItemVisualCatalog caches successful and missing paths", CachesSuccessfulAndMissingPaths);
        registry.Add("Rendering.WorldView renders visible ground item piles beneath entities", RendersVisibleGroundItemPiles);
        registry.Add("Rendering.WorldView hides and cleans ground item visuals across updates", HidesAndCleansGroundItemVisuals);
    }

    private static void LoadsEveryAuthoredItemPath()
    {
        ItemVisualCatalog.ClearTextureCacheForTests();
        var content = ContentLoader.LoadFromRepository();
        foreach (var template in content.ItemTemplates.Values)
        {
            var texture = ItemVisualCatalog.GetTexture(template);
            Expect.NotNull(texture, $"'{template.TemplateId}' should load its authored item art.");
            Expect.Equal(template.SpritePath, texture!.ResourcePath,
                $"'{template.TemplateId}' should retain the exact authored sprite_path.");
        }
    }

    private static void UsesSourceImageWhenImportIsAbsent()
    {
        var content = ContentLoader.LoadFromRepository();
        var template = content.ItemTemplates.Values.First();
        ItemVisualCatalog.ClearTextureCacheForTests();
        GD.MissingResourcePaths.Add(template.SpritePath);
        try
        {
            var texture = ItemVisualCatalog.GetTexture(template);
            Expect.True(texture is ImageTexture, "A missing imported item texture should safely load from its source asset.");
            Expect.Equal(template.SpritePath, texture!.ResourcePath, "The source fallback should preserve the authored resource path.");
        }
        finally
        {
            GD.MissingResourcePaths.Remove(template.SpritePath);
            ItemVisualCatalog.ClearTextureCacheForTests();
        }
    }

    private static void CachesSuccessfulAndMissingPaths()
    {
        var content = ContentLoader.LoadFromRepository();
        var template = content.ItemTemplates.Values.First();
        ItemVisualCatalog.ClearTextureCacheForTests();
        var first = ItemVisualCatalog.GetTexture(template);
        GD.MissingResourcePaths.Add(template.SpritePath);
        Image.MissingImagePaths.Add(ProjectSettings.GlobalizePath(template.SpritePath));
        try
        {
            Expect.True(ReferenceEquals(first, ItemVisualCatalog.GetTexture(template)), "Item textures should be returned from the path cache.");

            ItemVisualCatalog.ClearTextureCacheForTests();
            Expect.True(ItemVisualCatalog.GetTexture(template) is null, "Missing imported and source item art should resolve to a safe null fallback.");
            Expect.True(ItemVisualCatalog.GetTexture(template) is null, "Missing item art should be cached rather than repeatedly loading.");
        }
        finally
        {
            GD.MissingResourcePaths.Remove(template.SpritePath);
            Image.MissingImagePaths.Remove(ProjectSettings.GlobalizePath(template.SpritePath));
            ItemVisualCatalog.ClearTextureCacheForTests();
        }
    }

    private static void RendersVisibleGroundItemPiles()
    {
        var content = ContentLoader.LoadFromRepository();
        var world = CreateWorld(content);
        var position = new Position(2, 2);
        world.DropItem(position, new ItemInstance { TemplateId = "potion_health" });
        world.DropItem(position, new ItemInstance { TemplateId = "sword_iron" });
        world.SetVisible(position, true);

        var view = new WorldView();
        view.BindWorld(world);

        var root = FindChild<Node2D>(view.GroundItemLayerNode, "GroundItem_2_2");
        var icon = FindChild<Sprite2D>(root!, "ItemIcon");
        Expect.NotNull(root, "A visible ground pile should have one dedicated render root.");
        Expect.Equal(10, view.GroundItemLayerNode.ZIndex, "Ground items should render above tile art but below entity roots.");
        Expect.True(view.GroundItemLayerNode.ZIndex < view.EntityLayerNode.ZIndex, "Ground items must remain beneath actors.");
        Expect.NotNull(icon, "The first item in a visible pile should render its authored texture.");
        Expect.Equal(content.ItemTemplates["potion_health"].SpritePath, icon!.Texture!.ResourcePath, "Ground pile should use the first item's exact authored path.");
        Expect.Equal("+1", FindChild<Label>(root!, "PileCount")!.Text, "Ground piles should show a compact remaining-item badge.");
    }

    private static void HidesAndCleansGroundItemVisuals()
    {
        var content = ContentLoader.LoadFromRepository();
        var world = CreateWorld(content);
        var position = new Position(2, 2);
        world.DropItem(position, new ItemInstance { TemplateId = "potion_health" });
        var bus = new EventBus();
        var view = new WorldView();
        view.BindWorld(world);
        view.BindEventBus(bus);
        Expect.True(view.GroundItemLayerNode.GetChildren().Count == 0, "Items on hidden fog cells must not be drawn.");

        world.SetVisible(position, true);
        bus.EmitFovRecalculated();
        Expect.NotNull(FindChild<Node2D>(view.GroundItemLayerNode, "GroundItem_2_2"), "Visible cells should gain item visuals on FOV refresh.");

        world.PickupItem(position);
        bus.EmitItemPickedUp(world.Player!.Id, new ItemInstance { TemplateId = "potion_health" });
        Expect.True(view.GroundItemLayerNode.GetChildren().Count == 0, "Pickup refresh should remove the consumed ground-item root.");

        var nextWorld = CreateWorld(content);
        view.BindWorld(nextWorld);
        Expect.True(view.GroundItemLayerNode.GetChildren().Count == 0, "Floor binding should clean roots from the previous floor.");
    }

    private static WorldState CreateWorld(IContentDatabase content)
    {
        var world = new WorldState { ContentDatabase = content };
        world.InitGrid(5, 5);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        world.Player = player;
        world.AddEntity(player);
        return world;
    }

    private static T? FindChild<T>(Node node, string name) where T : Node
    {
        if (node is T typed && node.Name == name)
        {
            return typed;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindChild<T>(child, name) is { } match)
            {
                return match;
            }
        }

        return null;
    }
}
