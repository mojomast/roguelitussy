using System.Reflection;
using Godotussy;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class PauseMenuTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.PauseMenu constructs before scene tree", ConstructsBeforeSceneTree);
        registry.Add("UI.UIRoot constructs pause menu before scene tree", UIRootConstructsBeforeSceneTree);
        registry.Add("UI.PauseMenu displays default run stats", DisplaysDefaultRunStats);
        registry.Add("UI.PauseMenu displays floor reached", DisplaysFloorReached);
        registry.Add("UI.PauseMenu always displays seed", AlwaysDisplaysSeed);
    }

    private static void ConstructsBeforeSceneTree()
    {
        var menu = new PauseMenu();

        Expect.False(menu.Visible, "Pause menu should start hidden when constructed before entering the scene tree.");
        Expect.True(menu.MenuText.Contains("CURRENT RUN"), "Pause menu should render safe default run stats before binding services.");
    }

    private static void UIRootConstructsBeforeSceneTree()
    {
        var root = new UIRoot();

        Expect.NotNull(root.PauseMenu, "UIRoot should construct its pause menu before entering the scene tree without resolving autoloads.");
        Expect.False(root.PauseMenu.Visible, "Nested pause menu should start hidden.");
    }

    private static void DisplaysDefaultRunStats()
    {
        var gameManager = new GameManager();
        var menu = new PauseMenu();
        menu.Bind(gameManager, new EventBus());

        menu.Open();

        Expect.True(menu.MenuText.Contains("CURRENT RUN"), "Pause menu should include the current-run section.");
        Expect.True(menu.MenuText.Contains("Floor"), "Default stats should render without a null reference.");
        Expect.True(menu.MenuText.Contains("0"), "Default zero values should be visible in the stats section.");
    }

    private static void DisplaysFloorReached()
    {
        var gameManager = new GameManager();
        SetRunStats(gameManager, SampleStats(floorReached: 5));
        var menu = new PauseMenu();
        menu.Bind(gameManager, new EventBus());

        menu.Open();

        Expect.True(menu.MenuText.Contains("Floor        5"), "Pause menu should show the current run floor reached.");
    }

    private static void AlwaysDisplaysSeed()
    {
        var gameManager = new GameManager();
        SetRunStats(gameManager, SampleStats(seed: 1337));
        var menu = new PauseMenu();
        menu.Bind(gameManager, new EventBus());

        menu.Open();

        Expect.True(menu.MenuText.Contains("Seed     1,337"), "Pause menu should show the seed even when other stats are zero.");
    }

    private static RunStats SampleStats(int floorReached = 0, int seed = 0) => new(
        "Rook",
        floorReached,
        0,
        0,
        0,
        0,
        0,
        seed,
        "Unknown",
        string.Empty,
        0);

    private static void SetRunStats(GameManager gameManager, RunStats stats)
    {
        var field = typeof(GameManager).GetField("_runStats", BindingFlags.Instance | BindingFlags.NonPublic);
        Expect.NotNull(field, "GameManager should keep run stats in a private backing field.");
        field!.SetValue(gameManager, stats);
    }
}
