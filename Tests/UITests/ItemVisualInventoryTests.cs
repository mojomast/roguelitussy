using System.Linq;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class ItemVisualInventoryTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Inventory renders authored item art while retaining slot badges", InventoryRendersAuthoredItemArt);
        registry.Add("UI.Inventory renders every authored item template icon", InventoryRendersEveryAuthoredItemTemplate);
        registry.Add("UI.Inventory retains category glyph when item art is unavailable", InventoryRetainsGlyphWhenItemArtIsUnavailable);
    }

    private static void InventoryRendersAuthoredItemArt()
    {
        var context = CreateContext("potion_health", stackCount: 3);
        var inventory = new InventoryUI();
        inventory.Bind(context.Manager, context.Bus, context.Content, new Tooltip());
        inventory.Open();

        var background = FindChild<Control>(inventory, "Slot0_Background")!;
        var icon = FindChild<TextureRect>(background, "ItemIcon");
        Expect.NotNull(icon, "Each visual slot should own an ItemIcon texture child.");
        Expect.NotNull(icon!.Texture, "An authored item should render its SVG texture in the inventory.");
        Expect.Equal(context.Content.ItemTemplates["potion_health"].SpritePath, icon.Texture!.ResourcePath, "Inventory should use the exact authored item path.");
        Expect.Equal(TextureRect.ExpandModeEnum.IgnoreSize, icon.ExpandMode, "Item icon texture dimensions must not resize compact slots.");
        Expect.Equal(TextureRect.StretchModeEnum.KeepAspectCentered, icon.StretchMode, "Item icon should preserve its aspect ratio in the slot.");
        Expect.False(FindChild<Label>(inventory, "Slot0_Glyph")!.Visible, "The category glyph should only appear as a missing-art fallback.");
        Expect.True(FindChild<Label>(inventory, "Slot0_StackLabel")!.Visible, "Stack badge should remain readable above the item art.");
    }

    private static void InventoryRetainsGlyphWhenItemArtIsUnavailable()
    {
        var context = CreateContext("potion_health");
        var path = context.Content.ItemTemplates["potion_health"].SpritePath;
        ItemVisualCatalog.ClearTextureCacheForTests();
        GD.MissingResourcePaths.Add(path);
        Image.MissingImagePaths.Add(ProjectSettings.GlobalizePath(path));
        try
        {
            var inventory = new InventoryUI();
            inventory.Bind(context.Manager, context.Bus, context.Content, new Tooltip());
            inventory.Open();

            Expect.False(FindChild<TextureRect>(FindChild<Control>(inventory, "Slot0_Background")!, "ItemIcon")!.Visible,
                "Unavailable imported and source item art should not crash or leave an empty icon overlay.");
            Expect.True(FindChild<Label>(inventory, "Slot0_Glyph")!.Visible,
                "Unavailable item art should retain the readable category glyph.");
        }
        finally
        {
            GD.MissingResourcePaths.Remove(path);
            Image.MissingImagePaths.Remove(ProjectSettings.GlobalizePath(path));
            ItemVisualCatalog.ClearTextureCacheForTests();
        }
    }

    private static void InventoryRendersEveryAuthoredItemTemplate()
    {
        var content = ContentLoader.LoadFromRepository();
        foreach (var template in content.ItemTemplates.Values)
        {
            var context = CreateContext(template.TemplateId);
            var inventory = new InventoryUI();
            inventory.Bind(context.Manager, context.Bus, context.Content, new Tooltip());
            inventory.Open();

            var background = FindChild<Control>(inventory, "Slot0_Background")!;
            var icon = FindChild<TextureRect>(background, "ItemIcon");
            Expect.NotNull(icon?.Texture, $"'{template.TemplateId}' should have an inventory icon texture.");
            Expect.Equal(template.SpritePath, icon!.Texture!.ResourcePath,
                $"'{template.TemplateId}' should render its exact authored sprite path.");
        }
    }

    private static (GameManager Manager, EventBus Bus, IContentDatabase Content) CreateContext(string templateId, int stackCount = 1)
    {
        var content = ContentLoader.LoadFromRepository();
        var world = new WorldState { ContentDatabase = content };
        world.InitGrid(3, 3);
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        var items = new InventoryComponent(10);
        items.Add(new ItemInstance { TemplateId = templateId, StackCount = stackCount });
        player.SetComponent(items);
        world.Player = player;
        world.AddEntity(player);
        var manager = new GameManager();
        var bus = new EventBus();
        manager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        return (manager, bus, content);
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
