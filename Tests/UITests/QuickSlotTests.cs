using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class QuickSlotTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.QuickSlotHotbar initializes empty slots", InitializesEmptySlots);
        registry.Add("UI.QuickSlotHotbar shows consumables and empty slots", ShowsConsumablesAndEmptySlots);
        registry.Add("UI.QuickSlot key 1 submits UseItemAction", KeyOneSubmitsUseItemAction);
        registry.Add("UI.QuickSlot empty key does not submit action", EmptyKeyDoesNotSubmitAction);
        registry.Add("UI.QuickSlotHotbar suppression toggles visibility", SuppressionTogglesVisibility);
    }

    private static void InitializesEmptySlots()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        var hotbar = new QuickSlotHotbar();
        hotbar.Bind(context.GameManager, context.Bus, context.Content);

        Expect.Equal(5, hotbar.SlotTexts.Count, "Hotbar should always expose five slots.");
        for (var i = 0; i < 5; i++)
        {
            Expect.Equal($"{i + 1}: empty", hotbar.SlotTexts[i], $"Slot {i + 1} should initialize empty.");
        }
    }

    private static void ShowsConsumablesAndEmptySlots()
    {
        var context = CreateContext(
            new ItemInstance { TemplateId = "potion_health", IsIdentified = true },
            new ItemInstance { TemplateId = "potion_haste", IsIdentified = true },
            new ItemInstance { TemplateId = "scroll_fireball", IsIdentified = true });
        context.GameManager.LoadWorld(context.World);
        var hotbar = new QuickSlotHotbar();
        hotbar.Bind(context.GameManager, context.Bus, context.Content);

        Expect.True(hotbar.SlotTexts[0].Contains("Health Potion", System.StringComparison.Ordinal), "Slot 1 should show the first usable item.");
        Expect.True(hotbar.SlotTexts[1].Contains("Haste Potion", System.StringComparison.Ordinal), "Slot 2 should show the second usable item.");
        Expect.True(hotbar.SlotTexts[2].Contains("Scroll of Fireball", System.StringComparison.Ordinal), "Slot 3 should show the third usable item.");
        Expect.Equal("4: empty", hotbar.SlotTexts[3], "Slot 4 should remain empty.");
        Expect.Equal("5: empty", hotbar.SlotTexts[4], "Slot 5 should remain empty.");
    }

    private static void KeyOneSubmitsUseItemAction()
    {
        var context = CreateContext(new ItemInstance { TemplateId = "potion_health", IsIdentified = true });
        context.GameManager.LoadWorld(context.World);
        var input = new InputHandler();
        input.Bind(context.GameManager, context.Bus);
        IAction? submitted = null;
        context.Bus.PlayerActionSubmitted += action => submitted = action;

        Expect.True(input.HandleKey(Key.Key1), "Key 1 should handle the first quick slot.");
        Expect.True(submitted is UseItemAction, "Valid quick-use should submit the existing UseItemAction path.");
    }

    private static void EmptyKeyDoesNotSubmitAction()
    {
        var context = CreateContext(new ItemInstance { TemplateId = "potion_health", IsIdentified = true });
        context.GameManager.LoadWorld(context.World);
        var input = new InputHandler();
        input.Bind(context.GameManager, context.Bus);
        IAction? submitted = null;
        context.Bus.PlayerActionSubmitted += action => submitted = action;

        Expect.True(input.HandleKey(Key.Key5), "Empty quick slot key should be handled safely.");
        Expect.True(submitted is null, "Empty quick slot key should not submit a player action.");
    }

    private static void SuppressionTogglesVisibility()
    {
        var context = CreateContext(new ItemInstance { TemplateId = "potion_health", IsIdentified = true });
        context.GameManager.LoadWorld(context.World);
        var hotbar = new QuickSlotHotbar();
        hotbar.Bind(context.GameManager, context.Bus, context.Content);

        Expect.True(hotbar.Visible, "Hotbar should show during gameplay when unsuppressed.");
        hotbar.SetSuppressed(true);
        Expect.False(hotbar.Visible, "Hotbar should hide when suppressed.");
        hotbar.SetSuppressed(false);
        Expect.True(hotbar.Visible, "Hotbar should show again when gameplay is active and suppression is cleared.");
    }

    private static UIContext CreateContext(params ItemInstance[] items)
    {
        var world = new WorldState();
        world.InitGrid(6, 6);
        world.Depth = 1;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
                world.SetVisible(new Position(x, y), true);
            }
        }

        var player = new StubEntity(
            "Player",
            new Position(2, 2),
            Faction.Player,
            stats: new Stats
            {
                HP = 30,
                MaxHP = 40,
                Attack = 8,
                Defense = 4,
                Accuracy = 80,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 6,
            });
        var inventory = new InventoryComponent(20);
        foreach (var item in items)
        {
            inventory.Add(item);
        }

        player.SetComponent(inventory);
        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);
        var gameManager = new GameManager();
        var content = new StubContentDatabase();
        gameManager.AttachServices(world, scheduler, new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);

        return new UIContext(world, bus, gameManager, content);
    }

    private sealed record UIContext(WorldState World, EventBus Bus, GameManager GameManager, StubContentDatabase Content);
}
