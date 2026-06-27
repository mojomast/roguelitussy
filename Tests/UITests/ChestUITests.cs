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
        registry.Add("UI.ChestUI selected take all emits chest action", SelectedTakeAllEmitsChestAction);
        registry.Add("UI.ChestUI title click emits chest action", TitleClickEmitsChestAction);
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
        Expect.Equal(2, ui.Options.Count, "Chest UI should expose Take All and Close menu options.");
        Expect.Equal("Take All", ui.Options[0], "First option should take all loot.");
        Expect.Equal("Close", ui.Options[1], "Second option should close the modal.");
    }

    private static void SelectedTakeAllEmitsChestAction()
    {
        var context = CreateContext();
        var ui = CreateUi(context);
        OpenChestAction? submitted = null;
        context.Bus.PlayerActionSubmitted += action => submitted = action as OpenChestAction;

        ui.Open(context.Chest.Id);
        var handled = ui.HandleKey(Key.Enter);

        Expect.True(handled, "Enter should activate the selected Take All option.");
        Expect.NotNull(submitted, "Take All should submit an OpenChestAction through EventBus.");
        Expect.Equal(context.Player.Id, submitted!.ActorId, "Submitted action should use the player as actor.");
        Expect.Equal(context.Chest.Id, submitted.ChestId, "Submitted action should target the opened chest.");
        Expect.False(ui.Visible, "Submitting Take All should close the chest UI.");
    }

    private static void TitleClickEmitsChestAction()
    {
        var context = CreateContext();
        var ui = CreateUi(context);
        OpenChestAction? submitted = null;
        context.Bus.PlayerActionSubmitted += action => submitted = action as OpenChestAction;

        ui.Open(context.Chest.Id);
        var titleClickTarget = FindChild<UiMouseColorRect>(ui, "TitleClickTarget");
        Expect.NotNull(titleClickTarget, "Chest UI should expose a local title click target.");
        titleClickTarget!._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });

        Expect.NotNull(submitted, "Clicking the chest title should submit an OpenChestAction.");
        Expect.Equal(context.Player.Id, submitted!.ActorId, "Title click action should use the player as actor.");
        Expect.Equal(context.Chest.Id, submitted.ChestId, "Title click action should target the opened chest.");
        Expect.False(ui.Visible, "Title click Take All should close the chest UI.");
    }

    private static void CloseOptionAndHotkeysClose()
    {
        var context = CreateContext();
        var ui = CreateUi(context);

        ui.Open(context.Chest.Id);
        ui.HandleKey(Key.Down);
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
        chest.SetComponent(new ChestComponent { LootTableId = "starter_chest" });
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
