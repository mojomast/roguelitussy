using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class TextContainmentTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Text containment configures real label constraints", SingleLinePolicy);
        registry.Add("UI.Text containment modal overlays suppress the HUD", ModalSuppressesHud);
        registry.Add("UI.Text containment compact gameplay separates log and status", () => WithViewport(new Vector2(640f, 360f), CompactGameplay));
        foreach (var size in new[] { new Vector2(640f, 360f), new Vector2(1280f, 720f), new Vector2(1920f, 1080f) })
        {
            var viewportSize = size;
            registry.Add($"UI.Text containment menu {size.X}x{size.Y}", () => WithViewport(viewportSize, MenuRowsAndPreview));
            registry.Add($"UI.Text containment inventory {size.X}x{size.Y}", () => WithViewport(viewportSize, InventoryRegions));
            registry.Add($"UI.Text containment shop {size.X}x{size.Y}", () => WithViewport(viewportSize, ShopRows));
            registry.Add($"UI.Text containment HUD {size.X}x{size.Y}", () => WithViewport(viewportSize, HudLabelsAndIcons));
        }
    }

    private static void SingleLinePolicy()
    {
        var label = new Label { Text = new string('W', 200), Size = new Vector2(100f, 22f) };
        UiStyle.ConfigureSingleLineLabel(label);
        AssertPolicy(label);
        Expect.Equal(new string('W', 200), label.Text, "Engine ellipsis should retain the full source string.");
        UiStyle.ConfigureSingleLineLabel(label, 18);
        Expect.Equal(18, label.GetThemeFontSize("font_size"), "Caller-selected font size should be explicit.");
    }

    private static void ModalSuppressesHud()
    {
        var context = CreateContext();
        context.Manager.LoadWorld(context.Manager.World!);
        var root = new UIRoot();
        root.BindServices(context.Manager, context.Bus, context.Content);
        Expect.True(root.HUD.Visible, "Gameplay should show the HUD.");
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.I });
        Expect.False(root.HUD.Visible, "Inventory must not compete with gameplay chrome.");
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.True(root.HUD.Visible, "Closing inventory should restore gameplay chrome.");
    }

    private static void CompactGameplay(Control parent)
    {
        var context = CreateContext();
        context.Manager.LoadWorld(context.Manager.World!);
        var root = new UIRoot();
        parent.AddChild(root);
        root._Ready();
        root.BindServices(context.Manager, context.Bus, context.Content);
        var top = Child<Panel>(root.HUD, "Panel");
        var bottom = Child<Panel>(root.HUD, "BottomStatusPanel");
        var log = Child<Panel>(root.CombatLog, "Panel");
        var feedback = Child<Label>(root.HUD, "ActionFeedbackLabel");
        Expect.False(top.Visible, "Compact play should collapse duplicate upper HUD stats.");
        Expect.True(bottom.Visible, "Compact play must retain HP and XP.");
        Expect.True(log.Position.Y + log.Size.Y <= feedback.Position.Y, "Log should end before action feedback.");
        Expect.True(log.Position.Y + log.Size.Y <= bottom.Position.Y, "Log should not cover bottom status.");
    }

    private static void MenuRowsAndPreview(Control root)
    {
        var menu = new MainMenu();
        root.AddChild(menu);
        menu.Bind(null, null);
        menu.Open();
        var panel = Child<Panel>(menu, "Panel");
        for (var index = 0; index < menu.Options.Count * 2; index++)
        {
            var card = Child<ColorRect>(panel, "OptionsCard");
            var rows = card.GetChildren().OfType<Panel>().ToArray();
            Expect.True(rows.Any(row => Child<Label>(row, "RowLabel").Text.StartsWith("▶ ", StringComparison.Ordinal)),
                "Every keyboard selection must have a rendered row.");
            foreach (var row in rows)
            {
                AssertInside(row, card);
                var label = Child<Label>(row, "RowLabel");
                AssertInside(label, row);
                Expect.True(label.ClipText, "Menu labels must constrain their content minimum width.");
            }
            menu.HandleKey(Key.Down);
        }

        var preview = Child<Panel>(panel, "PreviewPanel");
        var subtitle = Child<Label>(preview, "PreviewSubtitle");
        var details = Child<RichTextLabel>(preview, "PreviewDetails");
        var variant = Child<Label>(preview, "PreviewVariantId");
        AssertInside(details, preview);
        Expect.True(details.Position.Y >= subtitle.Position.Y + subtitle.Size.Y, "Preview prose must start below the identity heading.");
        Expect.True(details.Position.Y + details.Size.Y <= variant.Position.Y, "Preview prose must end before variant metadata.");
        Expect.False(details.FitContent, "Scrolling preview must not expand to content height.");
        Expect.True(details.ScrollActive, "All preview sections must remain accessible.");
        Expect.Equal(TextServer.AutowrapMode.WordSmart, details.AutowrapMode, "Preview wrapping should use Godot shaping.");
        Expect.True(details.Text.Contains("READY KIT") && details.Text.Contains("PROJECTED"), "Kit and projected stats should share one non-overlapping text flow.");
    }

    private static void InventoryRegions(Control root)
    {
        var context = CreateContext();
        var tooltip = new Tooltip();
        root.AddChild(tooltip);
        tooltip.ShowShortcutTooltip("Stale", "Previous selection", Vector2.Zero);
        var inventory = new InventoryUI();
        root.AddChild(inventory);
        inventory.Bind(context.Manager, context.Bus, context.Content, tooltip);
        inventory.Open();
        var panel = Child<Panel>(inventory, "Panel");
        var header = Child<ColorRect>(panel, "HeaderBar");
        var footer = Child<ColorRect>(panel, "FooterBar");
        var detail = Child<RichTextLabel>(panel, "DescriptionLabel");
        AssertInside(detail, panel);
        Expect.True(detail.Position.Y >= header.Position.Y + header.Size.Y, "Inventory detail must clear the header.");
        Expect.True(detail.Position.Y + detail.Size.Y <= footer.Position.Y, "Inventory detail must clear the footer.");
        Expect.False(detail.FitContent, "Detail height must remain bounded.");
        Expect.True(detail.ScrollActive, "Full item details should remain scrollable.");
        foreach (var slot in panel.GetChildren().OfType<ColorRect>().Where(node => node.Name.StartsWith("Slot") && node.Name.EndsWith("_Background")))
        {
            Expect.True(slot.Position.X + slot.Size.X <= detail.Position.X, "Slots must not overlap the detail sidebar.");
            Expect.True(slot.Position.Y + slot.Size.Y <= footer.Position.Y, "Slots must not overlap footer actions.");
        }
        foreach (var label in panel.GetChildren().OfType<Label>().Where(label => label.Visible))
        {
            AssertInside(label, panel);
            Expect.True(label.ClipText, "Inventory text must have a bounded width policy.");
        }
        Expect.False(tooltip.Visible, "Inventory selection should dismiss stale tooltips.");
        inventory.Close();
        context.Bus.EmitInventoryChanged(context.Player.Id);
        Expect.False(tooltip.Visible, "Closed inventory refresh must not reopen a tooltip.");
    }

    private static void ShopRows(Control root)
    {
        var context = CreateContext();
        var merchant = new Entity(new string('W', 120), new Position(1, 0), new Stats { HP = 1, MaxHP = 1, Speed = 100 }, Faction.Neutral);
        merchant.SetComponent(new MerchantComponent(Enumerable.Range(0, 20).Select(index =>
            new MerchantOfferState { ItemTemplateId = "potion_health", Price = index + 1, Quantity = 2 }).ToArray()));
        context.Manager.World!.AddEntity(merchant);
        var shop = new ShopUI();
        root.AddChild(shop);
        shop.Bind(context.Manager, context.Bus, context.Content);
        shop.Open(merchant.Id);
        var panel = Child<Panel>(shop, "Panel");
        var list = Child<Control>(panel, "Entries");
        var footer = Child<Label>(panel, "Footer");
        Expect.Equal(1f, Child<ColorRect>(panel, "Background").Color.A, "Shop needs an opaque reading surface.");
        Expect.True(list.Position.Y + list.Size.Y <= footer.Position.Y, "Shop list must not consume the footer.");
        for (var mode = 0; mode < 2; mode++)
        {
            for (var selected = 0; selected < 20; selected++)
            {
                var row = Child<ColorRect>(list, $"Entry_{selected}");
                AssertInside(row, list);
                var text = Child<Label>(row, "EntryText");
                AssertInside(text, row);
                AssertPolicy(text);
                var price = Child<Label>(row, "PriceText");
                AssertInside(price, row);
                Expect.True(text.Position.X + text.Size.X <= price.Position.X, "Shop names must leave room for prices.");
                Expect.True(text.Text.StartsWith(">", StringComparison.Ordinal), "Selected shop entry must be rendered.");
                shop.HandleKey(Key.Down);
            }
            shop.HandleKey(Key.Tab);
        }
    }

    private static void HudLabelsAndIcons(Control root)
    {
        var context = CreateContext();
        StatusEffectProcessor.ApplyEffect(context.Player, StatusEffectType.Poisoned, 123);
        var hud = new HUD();
        root.AddChild(hud);
        hud.Bind(context.Manager, context.Bus);
        var panel = Child<Panel>(hud, "Panel");
        foreach (var label in panel.GetChildren().OfType<Label>().Where(label => label.Visible))
        {
            AssertPolicy(label);
            AssertInside(label, panel);
        }
        var statuses = Child<HBoxContainer>(panel, "StatusIconsContainer");
        var icons = statuses.GetChildren().OfType<TextureRect>().ToArray();
        Expect.True(icons.Length > 0, "Status fixture should render an authored icon.");
        foreach (var icon in icons)
        {
            Expect.Equal(TextureRect.ExpandModeEnum.IgnoreSize, icon.ExpandMode, "Native texture size must not enlarge the status row.");
            Expect.True(icon.CustomMinimumSize.Y <= statuses.Size.Y, "Status icon minimum must fit the row.");
        }
        var map = Child<Label>(panel, "MapLabel");
        Expect.True(statuses.Position.Y + statuses.Size.Y <= map.Position.Y, "Status row must clear map text.");
    }

    private static (GameManager Manager, EventBus Bus, StubContentDatabase Content, StubEntity Player) CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(3, 3);
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }
        var player = new StubEntity("Player", new Position(0, 0), Faction.Player,
            stats: new Stats { HP = 999, MaxHP = 9999, Speed = 100 });
        var inventory = new InventoryComponent(30);
        for (var index = 0; index < 20; index++)
        {
            inventory.Add(new ItemInstance { TemplateId = "potion_health", IsIdentified = true });
        }
        player.SetComponent(inventory);
        player.SetComponent(new WalletComponent { Gold = 100 });
        world.Player = player;
        world.AddEntity(player);
        var manager = new GameManager();
        var bus = new EventBus();
        var content = new StubContentDatabase();
        manager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        return (manager, bus, content, player);
    }

    private static T Child<T>(Node parent, string name) where T : Node
        => parent.GetChildren().OfType<T>().Single(child => child.Name == name);

    private static void AssertInside(Control child, Control parent)
    {
        Expect.True(child.Position.X >= 0f && child.Position.Y >= 0f, $"{child.Name} must start inside {parent.Name}.");
        Expect.True(child.Position.X + child.Size.X <= parent.Size.X + 0.1f
            && child.Position.Y + child.Size.Y <= parent.Size.Y + 0.1f, $"{child.Name} must fit inside {parent.Name}.");
    }

    private static void AssertPolicy(Label label)
    {
        Expect.True(label.ClipText, "Single-line text must constrain minimum width.");
        Expect.Equal(TextServer.OverrunBehavior.TrimEllipsis, label.TextOverrunBehavior, "Godot must handle ellipsis.");
        Expect.Equal(14, label.GetThemeFontSize("font_size"), "Rows must use an explicit readable font size.");
    }

    private static void WithViewport(Vector2 size, Action<Control> test)
    {
        var root = new Control();
        var viewport = root.GetViewport();
        var original = viewport.Size;
        viewport.Size = size;
        try
        {
            test(root);
        }
        finally
        {
            viewport.Size = original;
        }
    }
}
