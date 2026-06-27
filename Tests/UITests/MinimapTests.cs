using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class MinimapTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Minimap legend starts hidden and toggles independently", LegendStartsHiddenAndToggles);
        registry.Add("UI.Minimap legend stays hidden while minimap is hidden", LegendStaysHiddenWhenMinimapHidden);
        registry.Add("UI.Minimap legend key routes through gameplay input", LegendKeyRoutesThroughInput);
    }

    private static void LegendStartsHiddenAndToggles()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        var minimap = new Minimap();
        minimap.Bind(context.GameManager, context.Bus);

        Expect.True(minimap.Visible, "Minimap should be visible during gameplay setup.");
        Expect.False(minimap.LegendVisible, "Legend state should start hidden by default.");
        Expect.False(FindLegend(minimap).Visible, "Legend label should start hidden by default.");

        minimap.ToggleLegend();
        Expect.True(minimap.LegendVisible, "ToggleLegend should enable the independent legend state.");
        Expect.True(FindLegend(minimap).Visible, "Legend label should show when the minimap is visible.");

        minimap.ToggleLegend();
        Expect.False(minimap.LegendVisible, "Toggling again should disable the legend state.");
        Expect.False(FindLegend(minimap).Visible, "Legend label should hide after toggling off.");
    }

    private static void LegendStaysHiddenWhenMinimapHidden()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        var minimap = new Minimap();
        minimap.Bind(context.GameManager, context.Bus);

        minimap.ToggleLegend();
        minimap.Toggle();

        Expect.True(minimap.LegendVisible, "Hiding the minimap should preserve the user legend preference.");
        Expect.False(minimap.Visible, "Minimap toggle should hide the overlay.");
        Expect.False(FindLegend(minimap).Visible, "Legend label should not show while the minimap is hidden.");

        minimap.Toggle();
        Expect.True(minimap.Visible, "Re-enabling the minimap should restore the overlay.");
        Expect.True(FindLegend(minimap).Visible, "Legend label should reappear when the preserved preference is on.");
    }

    private static void LegendKeyRoutesThroughInput()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);

        Expect.False(root.Minimap.LegendVisible, "Legend should start hidden through UIRoot wiring.");
        root._UnhandledInput(new InputEventKey { Pressed = true, PhysicalKeycode = Key.U });

        Expect.True(root.Minimap.LegendVisible, "Gameplay legend key should toggle the minimap legend state.");
        Expect.True(FindLegend(root.Minimap).Visible, "Gameplay legend key should show the legend label while minimap is visible.");
    }

    private static Label FindLegend(Minimap minimap)
    {
        foreach (var child in minimap.Children)
        {
            if (child is Label label && label.Name == "MinimapLegend")
            {
                return label;
            }
        }

        throw new System.InvalidOperationException("MinimapLegend label was not found.");
    }

    private static UIContext CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(6, 6);
        world.Depth = 1;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                world.SetTile(position, TileType.Floor);
                world.SetVisible(position, true);
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
                ViewRadius = 6,
            });
        player.SetComponent(new InventoryComponent(20));

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
