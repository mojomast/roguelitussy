using System;
using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class ChestUITests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.ChestUI initializes title and actions", InitializesTitleAndActions);
        registry.Add("UI.ChestUI toggles selected loot rows", TogglesSelectedLootRows);
        registry.Add("UI.ChestUI take all hotkey emits loot action", TakeAllHotkeyEmitsLootAction);
        registry.Add("UI.ChestUI title click emits loot action", TitleClickEmitsLootAction);
        registry.Add("UI.ChestUI close option and hotkeys close", CloseOptionAndHotkeysClose);
        registry.Add("UI.ChestUI opens and closes safely", OpensAndClosesSafely);
    }

    private static void InitializesTitleAndActions()
    {
        var context = CreateContext();
        var ui = CreateUi(context);

        ui.Open(context.Chest.Id);

        Expect.True(ui.Visible, "Chest UI should be visible after opening.");
        Expect.Equal("ANCIENT CHEST", ui.Title, "Chest UI title should use the opened chest name.");
        Expect.Equal(5, ui.Options.Count, "Chest UI should expose two loot rows plus Take Selected, Take All, and Close.");
        Expect.True(ui.Options[0].Contains("Health Potion"), "First option should show rolled loot.");
        Expect.Equal("Take Selected", ui.Options[2], "Chooser should expose selected-loot pickup.");
        Expect.Equal("Take All", ui.Options[3], "Chooser should expose take-all pickup.");
        Expect.Equal("Close", ui.Options[4], "Last option should close the modal.");
    }

    private static void TogglesSelectedLootRows()
    {
        var context = CreateContext();
        var ui = CreateUi(context);

        ui.Open(context.Chest.Id);
        var handled = ui.HandleKey(Key.Enter);

        Expect.True(handled, "Enter should toggle the selected loot row.");
        Expect.True(ui.Visible, "Toggling loot should keep the chooser open.");
        Expect.True(ui.Options[0].StartsWith("[x]", StringComparison.Ordinal), "Toggled loot should show a checked marker.");
    }

    private static void TakeAllHotkeyEmitsLootAction()
    {
        var context = CreateContext();
        var ui = CreateUi(context);
        TakeChestLootAction? submitted = null;
        context.Bus.PlayerActionSubmitted += action => submitted = action as TakeChestLootAction;

        ui.Open(context.Chest.Id);
        var handled = ui.HandleKey(Key.T);

        Expect.True(handled, "T should activate Take All.");
        Expect.NotNull(submitted, "Take All should submit a TakeChestLootAction through EventBus.");
        Expect.Equal(context.Player.Id, submitted!.ActorId, "Submitted action should use the player as actor.");
        Expect.Equal(context.Chest.Id, submitted.ChestId, "Submitted action should target the opened chest.");
        Expect.True(submitted.TakeAll, "Take-all hotkey should request all chest contents.");
        Expect.False(ui.Visible, "Taking all loot should close the emptied chest UI.");
    }

    private static void TitleClickEmitsLootAction()
    {
        var context = CreateContext();
        var ui = CreateUi(context);
        TakeChestLootAction? submitted = null;
        context.Bus.PlayerActionSubmitted += action => submitted = action as TakeChestLootAction;

        ui.Open(context.Chest.Id);
        var titleClickTarget = FindChild<UiMouseColorRect>(ui, "TitleClickTarget");
        Expect.NotNull(titleClickTarget, "Chest UI should expose a local title click target.");
        titleClickTarget!._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });

        Expect.NotNull(submitted, "Clicking the chest title should submit a TakeChestLootAction.");
        Expect.Equal(context.Player.Id, submitted!.ActorId, "Title click action should use the player as actor.");
        Expect.Equal(context.Chest.Id, submitted.ChestId, "Title click action should target the opened chest.");
        Expect.True(submitted.TakeAll, "Title click should request all chest contents.");
        Expect.False(ui.Visible, "Title click Take All should close the chest UI.");
    }

    private static void CloseOptionAndHotkeysClose()
    {
        var context = CreateContext();
        var ui = CreateUi(context);

        ui.Open(context.Chest.Id);
        for (var i = 0; i < 4; i++)
        {
            ui.HandleKey(Key.Down);
        }

        var handled = ui.HandleKey(Key.Enter);

        Expect.True(handled, "Enter should activate the selected Close option.");
        Expect.False(ui.Visible, "Close option should hide the chest UI.");
        Expect.Equal(EntityId.Invalid, ui.ChestId, "Close option should clear the active chest id.");

        ui.Open(context.Chest.Id);
        Expect.True(ui.HandleKey(Key.Escape), "Escape should be handled by ChestUI.");
        Expect.False(ui.Visible, "Escape should close the chest UI.");

        ui.Open(context.Chest.Id);
        Expect.True(ui.HandleKey(Key.F), "F should be handled by ChestUI.");
        Expect.False(ui.Visible, "F should close the chest UI.");
    }

    private static void OpensAndClosesSafely()
    {
        var context = CreateContext();
        var ui = CreateUi(context);

        ui.Open(context.Chest.Id);
        ui.Close();

        Expect.False(ui.Visible, "Close should hide the chest UI.");
        Expect.Equal(EntityId.Invalid, ui.ChestId, "Close should clear the active chest id.");
    }

    private static ChestUI CreateUi(ChestContext context)
    {
        var ui = new ChestUI();
        ui.Bind(context.GameManager, context.Bus, context.Content);
        return ui;
    }

    private static ChestContext CreateContext()
    {
        var bus = new EventBus();
        var content = new StubContentDatabase();
        var world = new WorldState();
        world.InitGrid(5, 5);
        world.Depth = 1;
        world.Seed = 1234;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        var player = new StubEntity("Player", new Position(1, 1), Faction.Player);
        player.SetComponent(new InventoryComponent(10));
        world.Player = player;
        world.AddEntity(player);

        var chest = new StubEntity("Ancient Chest", new Position(2, 1), Faction.Neutral);
        var chestComponent = new ChestComponent { LootTableId = "starter_chest", HasRolled = true };
        chestComponent.Contents.Add(new ItemInstance { TemplateId = "potion_health", StackCount = 1, IsIdentified = true });
        chestComponent.Contents.Add(new ItemInstance { TemplateId = "scroll_blink", StackCount = 1, IsIdentified = true });
        chest.SetComponent(chestComponent);
        world.AddEntity(chest);

        var gameManager = new GameManager();
        gameManager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        return new ChestContext(gameManager, bus, content, player, chest);
    }

    private static T? FindChild<T>(Node node, string name) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match && string.Equals(child.Name, name, StringComparison.Ordinal))
            {
                return match;
            }

            var nested = FindChild<T>(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private sealed record ChestContext(GameManager GameManager, EventBus Bus, IContentDatabase Content, IEntity Player, IEntity Chest);
}
