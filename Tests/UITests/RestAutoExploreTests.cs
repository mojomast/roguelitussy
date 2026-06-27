using Godot;
using Godotussy;
using Roguelike.Core;
using Roguelike.Tests.Stubs;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class RestAutoExploreTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.Rest Z waits through normal gameplay processing", RestKeyWaitsDuringGameplay);
        registry.Add("UI.Autoexplore O handles no-target gameplay state", AutoExploreKeyHandlesNoTargetDuringGameplay);
        registry.Add("UI.Rest and autoexplore keys are gated by game over", RestAndAutoExploreDoNotProcessBehindGameOver);
        registry.Add("UI.Rest and autoexplore keys are gated by pause menu", RestAndAutoExploreDoNotProcessBehindPauseMenu);
        registry.Add("UI.Rest and autoexplore keys are gated by examine panel", RestAndAutoExploreDoNotProcessBehindExaminePanel);
        registry.Add("UI.Rest key does not replace run prefix", RStillEntersRunPrefixMode);
    }

    private static void RestKeyWaitsDuringGameplay()
    {
        var context = CreateContext();
        context.Player.Stats.HP = 30;
        context.GameManager.LoadWorld(context.World);
        var root = CreateRoot(context);
        var turnsStarted = CountTurns(context.Bus);

        root._UnhandledInput(KeyEvent(Key.Z));

        Expect.Equal(64, turnsStarted(), "Z should rest by processing normal wait turns until the safety cap when no passive healing exists.");
        Expect.Equal(new Position(3, 2), context.Player.Position, "Rest should wait in place.");
    }

    private static void AutoExploreKeyHandlesNoTargetDuringGameplay()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        MarkAllWalkableExplored(context.World);
        context.World.ClearVisibility();
        context.World.SetVisible(context.Player.Position, true);
        var root = CreateRoot(context);
        var turnsStarted = CountTurns(context.Bus);

        root._UnhandledInput(KeyEvent(Key.O));

        Expect.Equal(0, turnsStarted(), "O should be handled without submitting movement when autoexplore has no target.");
        Expect.Equal(new Position(3, 2), context.Player.Position, "Autoexplore should not move when no reachable frontier or point of interest exists.");
    }

    private static void RestAndAutoExploreDoNotProcessBehindGameOver()
    {
        AssertModalBlocksKey(root => root.GameOverScreen.Open(SampleStats()), Key.Z, "Z should not rest while game over is visible.");
        AssertModalBlocksKey(root => root.GameOverScreen.Open(SampleStats()), Key.O, "O should not autoexplore while game over is visible.");
    }

    private static void RestAndAutoExploreDoNotProcessBehindPauseMenu()
    {
        AssertModalBlocksKey(root => root.PauseMenu.Open(), Key.Z, "Z should not rest while the pause menu is visible.");
        AssertModalBlocksKey(root => root.PauseMenu.Open(), Key.O, "O should not autoexplore while the pause menu is visible.");
    }

    private static void RestAndAutoExploreDoNotProcessBehindExaminePanel()
    {
        AssertModalBlocksKey(root => root.ExaminePanel.Open(), Key.Z, "Z should not rest while examine mode is active.");
        AssertModalBlocksKey(root => root.ExaminePanel.Open(), Key.O, "O should not autoexplore while examine mode is active.");
    }

    private static void RStillEntersRunPrefixMode()
    {
        var context = CreateContext();
        context.GameManager.LoadWorld(context.World);
        var root = CreateRoot(context);
        var turnsStarted = CountTurns(context.Bus);

        root._UnhandledInput(KeyEvent(Key.R));

        Expect.True(root.InputHandler.IsRunPrefixActive, "R should still enter run-prefix mode rather than resting.");
        Expect.Equal(0, turnsStarted(), "Entering run-prefix mode should not process a turn.");
    }

    private static void AssertModalBlocksKey(System.Action<UIRoot> openModal, Key key, string message)
    {
        var context = CreateContext();
        context.Player.Stats.HP = 30;
        context.GameManager.LoadWorld(context.World);
        MarkAllWalkableExplored(context.World);
        context.World.ClearVisibility();
        context.World.SetVisible(context.Player.Position, true);
        var root = CreateRoot(context);
        var turnsStarted = CountTurns(context.Bus);

        openModal(root);
        root._UnhandledInput(KeyEvent(key));

        Expect.Equal(0, turnsStarted(), message);
        Expect.Equal(new Position(3, 2), context.Player.Position, "Modal input should not move the player.");
    }

    private static UIRoot CreateRoot(UIContext context)
    {
        var root = new UIRoot();
        root.BindServices(context.GameManager, context.Bus, context.Content);
        return root;
    }

    private static System.Func<int> CountTurns(EventBus bus)
    {
        var turnsStarted = 0;
        bus.TurnStarted += _ => turnsStarted++;
        return () => turnsStarted;
    }

    private static InputEventKey KeyEvent(Key key) => new() { Pressed = true, PhysicalKeycode = key };

    private static UIContext CreateContext()
    {
        var world = new WorldState();
        world.InitGrid(7, 5);
        world.Depth = 1;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTile(new Position(x, y), x == 0 || y == 0 || x == world.Width - 1 || y == world.Height - 1 ? TileType.Wall : TileType.Floor);
            }
        }

        var player = new StubEntity(
            "Player",
            new Position(3, 2),
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
                ViewRadius = 0,
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

        return new UIContext(world, player, bus, gameManager, content);
    }

    private static void MarkAllWalkableExplored(WorldState world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new Position(x, y);
                if (world.IsWalkable(position))
                {
                    world.SetVisible(position, true);
                }
            }
        }
    }

    private static RunStats SampleStats() => new("Player", 1, 1, 0, 0, 0, 0, 0, "Unknown", string.Empty, 0);

    private sealed record UIContext(WorldState World, StubEntity Player, EventBus Bus, GameManager GameManager, StubContentDatabase Content);
}
