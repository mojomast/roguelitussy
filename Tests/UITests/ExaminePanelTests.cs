using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class ExaminePanelTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Examine opens at player and closes", OpensAtPlayerAndCloses);
        registry.Add("UI.Examine cursor respects known-cell bounds and descriptions", CursorBoundsAndDescriptions);
        registry.Add("UI.Examine does not reveal hidden traps", DoesNotRevealHiddenTraps);
        registry.Add("UI.Examine routing does not submit gameplay actions", RoutingDoesNotSubmitActions);
    }

    private static void OpensAtPlayerAndCloses()
    {
        var context = CreateContext();
        var panel = new ExaminePanel();
        panel.Bind(context.GameManager, context.Content);

        panel.Open();

        Expect.True(panel.IsActive, "Opening examine should show the panel.");
        Expect.Equal(context.Player.Position, panel.CursorPosition, "The examine cursor should start on the player.");
        Expect.True(panel.CurrentDescription.Contains("Dungeon floor", System.StringComparison.Ordinal), "The starting tile should be described.");

        Expect.True(panel.HandleKey(Key.Escape), "Escape should be handled by examine mode.");
        Expect.False(panel.IsActive, "Escape should close examine mode.");
    }

    private static void CursorBoundsAndDescriptions()
    {
        var context = CreateContext();
        var panel = new ExaminePanel();
        panel.Bind(context.GameManager, context.Content);
        context.World.DropItem(new Position(2, 1), new ItemInstance { TemplateId = "potion_health", StackCount = 2, IsIdentified = true });
        context.World.DropItem(new Position(3, 1), new ItemInstance { TemplateId = "shield_wooden", IsIdentified = true });
        context.World.SetVisible(new Position(3, 1), true);
        context.World.SetVisible(new Position(3, 1), false);

        panel.Open();
        panel.MoveCursor(new Position(-1, 0));
        Expect.Equal(context.Player.Position, panel.CursorPosition, "Cursor should not move onto unexplored cells.");

        panel.MoveCursor(new Position(1, 0));
        Expect.Equal(new Position(2, 1), panel.CursorPosition, "Cursor should move onto visible known cells.");
        Expect.True(panel.CurrentDescription.Contains("2x Health Potion", System.StringComparison.Ordinal), "Visible ground items should be described.");

        panel.MoveCursor(new Position(1, 0));
        Expect.Equal(new Position(3, 1), panel.CursorPosition, "Cursor should move onto explored remembered cells.");
        Expect.True(panel.CurrentDescription.Contains("Explored, but not currently visible", System.StringComparison.Ordinal), "Explored cells outside current sight should be marked as memory.");
        Expect.False(panel.CurrentDescription.Contains("Wooden Shield", System.StringComparison.Ordinal), "Explored but non-visible cells should not reveal current item details.");
    }

    private static void RoutingDoesNotSubmitActions()
    {
        var context = CreateContext();
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);
        var actionCount = 0;
        context.Bus.PlayerActionSubmitted += _ => actionCount++;

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.X });
        Expect.True(root.ExaminePanel.IsActive, "X should open examine during gameplay.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Right });
        Expect.Equal(0, actionCount, "Moving the examine cursor should not submit a gameplay action.");
        Expect.Equal(new Position(1, 1), context.Player.Position, "Moving the examine cursor should not move the player.");

        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.Escape });
        Expect.False(root.ExaminePanel.IsActive, "Escape should close examine through UIRoot routing.");
        Expect.Equal(0, actionCount, "Closing examine should not submit a gameplay action.");
    }

    private static void DoesNotRevealHiddenTraps()
    {
        var context = CreateContext();
        var hiddenTrap = new StubEntity("Spike Trap", new Position(2, 1), Faction.Neutral);
        hiddenTrap.SetComponent(new TrapComponent { TemplateId = "trap_spikes", IsRevealed = false, IsArmed = true });
        context.World.AddEntity(hiddenTrap);

        var panel = new ExaminePanel();
        panel.Bind(context.GameManager, context.Content);
        panel.Open();
        panel.MoveCursor(new Position(1, 0));

        Expect.False(panel.CurrentDescription.Contains("Trap", System.StringComparison.OrdinalIgnoreCase), "Hidden trap entities should not be identified by examine mode.");
        Expect.False(panel.CurrentDescription.Contains("Spike", System.StringComparison.OrdinalIgnoreCase), "Hidden trap names should not leak through examine mode.");
    }

    private static ExamineContext CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(5, 5);
        world.Depth = 1;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), TileType.Floor);
            }
        }

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
                Accuracy = 80,
                Evasion = 0,
                Speed = 100,
                ViewRadius = 2,
            });
        player.SetComponent(new InventoryComponent(20));
        world.Player = player;
        world.AddEntity(player);

        var bus = new EventBus();
        var content = new StubContentDatabase();
        var gameManager = new GameManager();
        gameManager.AttachServices(world, new TurnScheduler(), new StubGenerator(), new FOVCalculator(), content, new StubSaveManager(), bus);
        gameManager.LoadWorld(world);
        world.ClearVisibility();
        System.Array.Clear(world.GetRawExplored(), 0, world.GetRawExplored().Length);
        world.SetVisible(player.Position, true);
        world.SetVisible(new Position(2, 1), true);

        return new ExamineContext(world, player, bus, gameManager, content);
    }

    private sealed record ExamineContext(WorldState World, StubEntity Player, EventBus Bus, GameManager GameManager, StubContentDatabase Content);
}
