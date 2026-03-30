using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class UISmokeTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.HUD updates from bus and exposes keyboard toggles", HudUpdatesFromBus);
        registry.Add("UI.Inventory keyboard navigation emits concrete actions", InventoryEmitsConcreteActions);
        registry.Add("UI.CombatLog escapes BBCode and reacts to combat events", CombatLogReactsToEvents);
        registry.Add("UI.UIRoot routes keyboard between title, overlays, and gameplay", UIRootRoutesKeyboard);
    }

    private static void HudUpdatesFromBus()
    {
        var context = CreateContext();
        var hud = new HUD();
        hud.Bind(context.GameManager, context.Bus);

        context.World.Depth = 3;
        context.World.TurnNumber = 17;
        context.Player.Stats.HP = 25;
        context.Player.Stats.MaxHP = 100;

        context.Bus.EmitFloorChanged(3);
        context.Bus.EmitHPChanged(context.Player.Id, 25, 100);
        context.Bus.EmitTurnCompleted();

        Expect.Equal("HP: 25/100", hud.HPText, "HUD should render player HP from bus events");
        Expect.Equal("Floor: 3", hud.FloorText, "HUD should render the current floor");
        Expect.Equal("Turn: 17", hud.TurnText, "HUD should render the current turn number");
        Expect.Equal(Colors.Red.R, hud.HPColor.R, "Low HP should switch the HUD into the red threshold");

        var before = hud.MinimapText;
        hud.ToggleMinimap();
        Expect.False(before == hud.MinimapText, "Toggling the minimap should change the HUD summary");
    }

    private static void InventoryEmitsConcreteActions()
    {
        var useContext = CreateContext();
        var tooltip = new Tooltip();
        var inventory = new InventoryUI();
        inventory.Bind(useContext.GameManager, useContext.Bus, useContext.Content, tooltip);

        IAction? submitted = null;
        useContext.Bus.PlayerActionSubmitted += action => submitted = action;

        inventory.Open();
        Expect.Equal(0, inventory.SelectedIndex, "Inventory should start on the first slot");
        inventory.HandleKey(Key.Right);
        Expect.Equal(1, inventory.SelectedIndex, "Right arrow should move selection across the grid");
        inventory.HandleKey(Key.Left);
        inventory.HandleKey(Key.Enter);

        Expect.True(submitted is UseItemAction, "Using an inventory item should emit a concrete UseItemAction");

        var dropContext = CreateContext();
        var dropInventory = new InventoryUI();
        dropInventory.Bind(dropContext.GameManager, dropContext.Bus, dropContext.Content, tooltip);
        IAction? dropped = null;
        dropContext.Bus.PlayerActionSubmitted += action => dropped = action;

        dropInventory.Open();
        dropInventory.HandleKey(Key.D);

        Expect.True(dropped is DropItemAction, "Dropping an inventory item should emit a concrete DropItemAction");
    }

    private static void CombatLogReactsToEvents()
    {
        var context = CreateContext();
        var enemy = new StubEntity("Skeleton", new Position(3, 1), Faction.Enemy);
        context.World.AddEntity(enemy);

        var log = new CombatLog();
        log.Bind(context.GameManager, context.Bus);

        context.Bus.EmitLogMessage("A [trap] snaps.");
        context.Bus.EmitDamageDealt(new DamageResult(context.Player.Id, enemy.Id, 5, 4, DamageType.Physical, false, false, false));

        Expect.True(log.RenderedText.Contains("[lb]trap[rb]"), "Combat log should escape BBCode brackets");
        Expect.True(log.RenderedText.Contains("hits Skeleton for 4 damage"), "Combat log should append derived combat messages");
    }

    private static void UIRootRoutesKeyboard()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        Expect.True(root.MainMenu.Visible, "Main menu should start visible while the game manager is in MainMenu state");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Enter });

        Expect.Equal(GameManager.GameState.Playing, context.GameManager.CurrentState, "Selecting New Game should transition into playing state");
        Expect.False(root.MainMenu.Visible, "Main menu should close once a new game starts");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.I });
        Expect.True(root.Inventory.Visible, "Gameplay input should open the inventory overlay");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.False(root.Inventory.Visible, "Escape should close the inventory before reaching pause handling");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.True(root.PauseMenu.Visible, "Escape from gameplay should open the pause menu");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.False(root.PauseMenu.Visible, "Escape from the pause menu should close the overlay");

        context.Player.Stats.HP = 0;
        context.Bus.EmitTurnCompleted();
        Expect.True(root.GameOverScreen.Visible, "A dead player should open the game over overlay on the next turn update");
    }

    private static UIContext CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(8, 8);
        world.Depth = 1;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

        world.SetTile(new Position(6, 6), TileType.StairsDown);

        var player = new StubEntity(
            "Player",
            new Position(1, 1),
            Faction.Player,
            stats: new Stats
            {
                HP = 40,
                MaxHP = 40,
                Attack = 8,
                Defense = 4,
                Accuracy = 0,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 8,
            });
        var inventory = new InventoryComponent(20);
        inventory.Add(new ItemInstance { TemplateId = "potion_health" });
        inventory.Add(new ItemInstance { TemplateId = "potion_health" });
        player.SetComponent(inventory);

        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var scheduler = new TurnScheduler();
        scheduler.BeginRound(world);

        var gameManager = new GameManager();
        var content = new StubContentDatabase();
        gameManager.AttachServices(world, scheduler, new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);

        return new UIContext(world, player, bus, gameManager, content);
    }

    private sealed record UIContext(WorldState World, StubEntity Player, EventBus Bus, GameManager GameManager, StubContentDatabase Content);
}