using Godot;
using Godotussy;
using Roguelike.Tests.TestFramework;

namespace Roguelike.Tests.UITests;

public sealed class GameOverScreenTests : ITestSuite
{
    public void Register(TestRegistry registry)
    {
        registry.Add("UI.GameOverScreen opens populated run stats", OpensPopulatedRunStats);
        registry.Add("UI.GameOverScreen body includes key run stats", BodyIncludesKeyRunStats);
        registry.Add("UI.GameOverScreen body renders BBCode in rich text", BodyRendersBbcodeInRichText);
        registry.Add("UI.GameOverScreen zero damage flavor", ZeroDamageFlavor);
        registry.Add("UI.GameOverScreen no kills flavor", NoKillsFlavor);
        registry.Add("UI.GameOverScreen omits empty best item", OmitsEmptyBestItem);
        registry.Add("UI.GameOverScreen enter requests new run once", EnterRequestsNewRunOnce);
        registry.Add("UI.GameOverScreen escape requests main menu once", EscapeRequestsMainMenuOnce);
    }

    private static RunStats SampleStats(string bestItemName = "Iron Sword", int damageTaken = 12, int enemiesKilled = 4) => new(
        "Rook",
        4,
        1306,
        enemiesKilled,
        520,
        damageTaken,
        8,
        1337,
        "Goblin Archer",
        bestItemName,
        string.IsNullOrWhiteSpace(bestItemName) ? 0 : 50);

    private static void OpensPopulatedRunStats()
    {
        var screen = new GameOverScreen();
        screen.Open(SampleStats());

        Expect.True(screen.Visible, "Opening with full run stats should show the game-over screen.");
        Expect.True(screen.BuildBodyMarkup().Contains("Rook"), "Game-over body should include the character name.");
    }

    private static void BodyIncludesKeyRunStats()
    {
        var screen = new GameOverScreen();
        screen.Open(SampleStats());
        var markup = screen.BuildBodyMarkup();

        Expect.True(markup.Contains("Rook"), "Markup should contain character name.");
        Expect.True(markup.Contains("Floor 4"), "Markup should contain floor reached.");
        Expect.True(markup.Contains("Turn 1,306"), "Markup should format turn count.");
        Expect.True(markup.Contains("SEED:1337"), "Markup should contain seed.");
        Expect.False(markup.Contains("[SEED:", System.StringComparison.Ordinal), "Seed marker should be BBCode-safe when the body parses rich text.");
    }

    private static void BodyRendersBbcodeInRichText()
    {
        var screen = new GameOverScreen();
        screen.Open(SampleStats());

        var body = FindChild<RichTextLabel>(screen, "Label");

        Expect.NotNull(body, "Game-over body should use a RichTextLabel.");
        Expect.True(body!.BbcodeEnabled, "Game-over body should parse BBCode instead of displaying color tags literally.");
        Expect.True(body.Text.Contains("[color=", System.StringComparison.Ordinal), "Game-over body text should retain authored BBCode markup for RichTextLabel parsing.");
    }

    private static void ZeroDamageFlavor()
    {
        var screen = new GameOverScreen();
        screen.Open(SampleStats(damageTaken: 0));

        Expect.True(screen.BuildBodyMarkup().Contains("A flawless descent"), "Zero damage run should use flawless flavor.");
    }

    private static void NoKillsFlavor()
    {
        var screen = new GameOverScreen();
        screen.Open(SampleStats(damageTaken: 7, enemiesKilled: 0));

        Expect.True(screen.BuildBodyMarkup().Contains("Fell without drawing blood"), "No-kill run should use no-blood flavor.");
    }

    private static void OmitsEmptyBestItem()
    {
        var screen = new GameOverScreen();
        screen.Open(SampleStats(bestItemName: string.Empty));

        Expect.False(screen.BuildBodyMarkup().Contains("Best find:"), "Empty best item should omit the best-find row.");
    }

    private static void EnterRequestsNewRunOnce()
    {
        var screen = new GameOverScreen();
        var count = 0;
        screen.RetryRequested += () => count++;
        screen.Open(SampleStats());

        screen.HandleKey(Key.Enter);

        Expect.Equal(1, count, "Enter should request exactly one new run.");
    }

    private static void EscapeRequestsMainMenuOnce()
    {
        var screen = new GameOverScreen();
        var count = 0;
        screen.MainMenuRequested += () => count++;
        screen.Open(SampleStats());

        screen.HandleKey(Key.Escape);

        Expect.Equal(1, count, "Escape should request exactly one main-menu transition.");
    }

    private static T? FindChild<T>(Node node, string name) where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T match && string.Equals(child.Name, name, System.StringComparison.Ordinal))
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
}
